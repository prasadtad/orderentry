﻿using Microsoft.EntityFrameworkCore;
using OrderEntry.Apis;
using OrderEntry.Brokerages;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class DatabaseService(OrderEntryDbContext context) : IDatabaseService
    {
        private readonly OrderEntryDbContext context = context;

        private List<MarketDate>? marketDatesCache = null;

        private List<OptionContract>? optionContractCache = null;

        private List<StockDayData>? stockDayDataCache = null;

        private List<InsiderRecommendation>? insiderRecommendationsCache = null;

        public async Task<List<ParseSetting>> GetParseSettings()
        {
            return await context.ParseSettings.Where(o => o.Active).ToListAsync();
        }

        public async Task Save(List<ParseSetting> parseSettings)
        {
            context.UpdateRange(parseSettings);

            if (parseSettings.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> HasStockOrders(string parseSettingKey, DateOnly watchDate)
        {
            return await context.StockOrders.AnyAsync(o => o.ParseSettingKey == parseSettingKey && o.WatchDate == watchDate);
        }

        public async Task<List<StockOrder>> GetStockOrders()
        {
            return await context.StockOrders.ToListAsync();
        }

        public async Task<List<StockOrder>> GetStockOrders(string parseSettingKey, DateOnly watchDate)
        {
            return await context.StockOrders.Where(o => o.ParseSettingKey == parseSettingKey && o.WatchDate == watchDate).ToListAsync();
        }

        public async Task<bool> HasOptionOrders(string parseSettingKey, DateOnly watchDate)
        {
            return await context.OptionOrders.AnyAsync(o => o.ParseSettingKey == parseSettingKey && o.WatchDate == watchDate);
        }

        public async Task<List<OptionOrder>> GetOptionOrders(string parseSettingKey, DateOnly watchDate)
        {
            return await context.OptionOrders.Where(o => o.ParseSettingKey == parseSettingKey && o.WatchDate == watchDate).ToListAsync();
        }

        public async Task<int> DeleteStockOrders(DateOnly earliestDate)
        {
            return await context.StockOrders.Where(o => o.WatchDate < earliestDate).ExecuteDeleteAsync();
        }

        public async Task<int> DeleteOptionOrders(DateOnly earliestDate)
        {
            return await context.OptionOrders.Where(o => o.WatchDate < earliestDate).ExecuteDeleteAsync();
        }

        public async Task Save(List<StockOrder> stockOrders)
        {
            var newStockOrders = stockOrders.Where(so => so.Id == Guid.Empty).ToList();
            var existingStockOrders = stockOrders.Where(so => so.Id != Guid.Empty).ToList();

            if (newStockOrders.Count > 0) context.AddRange(newStockOrders);
            if (existingStockOrders.Count > 0) context.UpdateRange(existingStockOrders);

            if (newStockOrders.Count > 0 || existingStockOrders.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task Save(List<OptionOrder> optionOrders)
        {
            var newOptionOrders = optionOrders.Where(so => so.Id == Guid.Empty).ToList();
            var existingOptionOrders = optionOrders.Where(so => so.Id != Guid.Empty).ToList();

            if (newOptionOrders.Count > 0) context.AddRange(newOptionOrders);
            if (existingOptionOrders.Count > 0) context.UpdateRange(existingOptionOrders);

            if (newOptionOrders.Count > 0 || existingOptionOrders.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<StockPosition>> GetStockPositions()
        {
            return await context.StockPositions.AsNoTracking().ToListAsync();
        }

        public async Task<List<StockPosition>> GetStockPositions(Brokers broker)
        {
            return await context.StockPositions.Where(o => o.Broker == broker).AsNoTracking().ToListAsync();
        }

        public async Task<int> Delete(List<StockPosition> stockPositions)
        {
            if (stockPositions.Count == 0) return 0;

            foreach (var stockPosition in stockPositions)
            {
                var entity = context.StockPositions.Attach(stockPosition);
                entity.State = EntityState.Deleted;
            }
            return await context.SaveChangesAsync();
        }

        public async Task Insert(List<StockPosition> stockPositions)
        {
            if (stockPositions.Count > 0) context.AddRange(stockPositions);

            if (stockPositions.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task Update(List<StockPosition> stockPositions)
        {
            if (stockPositions.Count > 0) context.UpdateRange(stockPositions);

            if (stockPositions.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> HasStockDayData(DateOnly date, string ticker)
        {
            if (stockDayDataCache == null) stockDayDataCache = await context.StockDayDatas.AsNoTracking().ToListAsync();

            return stockDayDataCache.Any(s => s.From == date && s.Symbol == ticker);
        }

        public async Task<List<StockDayData>> GetStockDayData(DateOnly fromInclusive, DateOnly toExclusive, string ticker)
        {
            if (stockDayDataCache == null) stockDayDataCache = await context.StockDayDatas.AsNoTracking().ToListAsync();

            return stockDayDataCache.Where(s => s.From >= fromInclusive && s.From < toExclusive && s.Symbol == ticker).ToList();
        }

        public async Task<StockDayData?> GetStockDayData(DateOnly date, string ticker)
        {
            if (stockDayDataCache == null) stockDayDataCache = await context.StockDayDatas.AsNoTracking().ToListAsync();

            return stockDayDataCache.Where(s => s.From == date && s.Symbol == ticker).SingleOrDefault();
        }

        public async Task Insert(List<StockDayData> stockDayDatas)
        {
            if (stockDayDataCache == null) stockDayDataCache = await context.StockDayDatas.AsNoTracking().ToListAsync();

            if (stockDayDatas.Count > 0) context.AddRange(stockDayDatas);

            if (stockDayDatas.Count > 0)
            {
                if (await context.SaveChangesAsync() == stockDayDatas.Count)
                    stockDayDataCache.AddRange(stockDayDatas);
                else
                    stockDayDataCache = await context.StockDayDatas.AsNoTracking().ToListAsync();
            }
        }

        public async Task<List<OptionContract>> GetOptionContracts(DateOnly expirationDate, string type, string ticker)
        {
            if (optionContractCache == null) optionContractCache = await context.OptionContracts.AsNoTracking().ToListAsync();

            return optionContractCache.Where(s => s.ExpirationDate == expirationDate && s.ContractType == type && s.UnderlyingTicker == ticker).ToList();
        }

        public async Task Insert(List<OptionContract> optionContracts)
        {
            if (optionContractCache == null) optionContractCache = await context.OptionContracts.AsNoTracking().ToListAsync();

            if (optionContracts.Count > 0) context.AddRange(optionContracts);

            if (optionContracts.Count > 0)
            {
                if (await context.SaveChangesAsync() == optionContracts.Count)
                    optionContractCache.AddRange(optionContracts);
                else
                    optionContractCache = await context.OptionContracts.AsNoTracking().ToListAsync();
            }
        }

        public async Task<List<DateOnly>> GetMarketHolidays()
        {
            marketDatesCache ??= await context.MarketDates.AsNoTracking().ToListAsync();
            return marketDatesCache.Where(m => m.MarketDateType == MarketDateTypes.Holiday).Select(m => m.Date).ToList();
        }

        public async Task<List<DateOnly>> GetMonthlyExpirations()
        {
            marketDatesCache ??= await context.MarketDates.AsNoTracking().ToListAsync();
            return marketDatesCache.Where(m => m.MarketDateType == MarketDateTypes.MonthlyExpiration).Select(m => m.Date).ToList();
        }

        public async Task<List<InsiderRecommendation>> GetInsiderRecommendations()
        {
            insiderRecommendationsCache ??= await context.InsiderRecommendations.AsNoTracking().ToListAsync();
            return insiderRecommendationsCache;
        }

        public async Task Insert(List<InsiderRecommendation> insiderRecommendations)
        {
             if (insiderRecommendationsCache == null) insiderRecommendationsCache = await context.InsiderRecommendations.AsNoTracking().ToListAsync();

            if (insiderRecommendations.Count > 0) context.AddRange(insiderRecommendations);

            if (insiderRecommendations.Count > 0)
            {
                if (await context.SaveChangesAsync() == insiderRecommendations.Count)
                    insiderRecommendationsCache.AddRange(insiderRecommendations);
                else
                    insiderRecommendationsCache = await context.InsiderRecommendations.AsNoTracking().ToListAsync();
            }
        }
    }

    public interface IDatabaseService
    {
        Task<List<ParseSetting>> GetParseSettings();

        Task Save(List<ParseSetting> parseSettings);

        Task<List<StockOrder>> GetStockOrders();

        Task<bool> HasStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<StockOrder>> GetStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<bool> HasOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<OptionOrder>> GetOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task<int> DeleteStockOrders(DateOnly earliestDate);

        Task<int> DeleteOptionOrders(DateOnly earliestDate);

        Task Save(List<StockOrder> stockOrders);

        Task Save(List<OptionOrder> optionOrders);

        Task<List<StockPosition>> GetStockPositions();

        Task<List<StockPosition>> GetStockPositions(Brokers broker);

        Task<int> Delete(List<StockPosition> stockPositions);

        Task Insert(List<StockPosition> stockPositions);

        Task Update(List<StockPosition> stockPositions);

        Task<bool> HasStockDayData(DateOnly date, string ticker);

        Task<List<StockDayData>> GetStockDayData(DateOnly fromInclusive, DateOnly toExclusive, string ticker);

        Task<StockDayData?> GetStockDayData(DateOnly date, string ticker);

        Task Insert(List<StockDayData> stockDayDatas);

        Task<List<OptionContract>> GetOptionContracts(DateOnly expirationDate, string type, string ticker);

        Task Insert(List<OptionContract> optionContracts);

        Task<List<DateOnly>> GetMarketHolidays();

        Task<List<DateOnly>> GetMonthlyExpirations();

        Task<List<InsiderRecommendation>> GetInsiderRecommendations();

        Task Insert(List<InsiderRecommendation> insiderRecommendations);
    }
}

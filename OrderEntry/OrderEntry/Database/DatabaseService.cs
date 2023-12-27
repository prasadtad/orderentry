﻿using Microsoft.EntityFrameworkCore;
using OrderEntry.Brokerages;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class DatabaseService(OrderEntryDbContext context) : IDatabaseService
    {
        private readonly OrderEntryDbContext context = context;

        public async Task<List<ParseSetting>> GetParseSettings()
        {
            return await context.ParseSettings.Where(o => o.Active).ToListAsync();
        }

        public async Task<bool> HasStockOrders(string parseSettingKey, DateOnly watchDate)
        {
            return await context.StockOrders.AnyAsync(o => o.ParseSettingKey == parseSettingKey && o.WatchDate == watchDate);
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

        public async Task<List<InteractiveBrokersStock>> GetInteractiveBrokersStocks()
        {
            return await context.InteractiveBrokersStocks.AsNoTracking().ToListAsync();
        }

        public async Task<int> Delete(List<InteractiveBrokersStock> stockPositions, InteractiveBrokersStockComparer comparer)
        {
            if (stockPositions.Count == 0) return 0;

            return await context.InteractiveBrokersStocks.Where(s => stockPositions.Any(p => p.AccountId == s.AccountId && p.Ticker == s.Ticker)).ExecuteDeleteAsync();
        }

        public async Task Insert(List<InteractiveBrokersStock> stockPositions)
        {
            if (stockPositions.Count > 0) context.AddRange(stockPositions);

            if (stockPositions.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        public async Task Update(List<InteractiveBrokersStock> stockPositions)
        {
            if (stockPositions.Count > 0) context.UpdateRange(stockPositions);

            if (stockPositions.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }
    }

    public interface IDatabaseService
    {
        Task<List<ParseSetting>> GetParseSettings();

        Task<bool> HasStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<StockOrder>> GetStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<bool> HasOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<OptionOrder>> GetOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task<int> DeleteStockOrders(DateOnly earliestDate);

        Task<int> DeleteOptionOrders(DateOnly earliestDate);

        Task Save(List<StockOrder> stockOrders);

        Task Save(List<OptionOrder> optionOrders);

        Task<List<InteractiveBrokersStock>> GetInteractiveBrokersStocks();

        Task<int> Delete(List<InteractiveBrokersStock> stockPositions, InteractiveBrokersStockComparer comparer);

        Task Insert(List<InteractiveBrokersStock> stockPositions);

        Task Update(List<InteractiveBrokersStock> stockPositions);
    }
}

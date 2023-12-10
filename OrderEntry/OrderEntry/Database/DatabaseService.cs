using Microsoft.EntityFrameworkCore;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class DatabaseService(OrderEntryDbContext context) : IDatabaseService
    {
        private readonly OrderEntryDbContext context = context;

        public async Task<List<ParseSetting>> GetParseSettings()
        {
            return await context.ParseSettings.ToListAsync();
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
    }

    public interface IDatabaseService
    {
        Task<List<ParseSetting>> GetParseSettings();

        Task<bool> HasStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<StockOrder>> GetStockOrders(string parseSettingKey, DateOnly watchDate);

        Task<bool> HasOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task<List<OptionOrder>> GetOptionOrders(string parseSettingKey, DateOnly watchDate);

        Task Save(List<StockOrder> stockOrders);

        Task Save(List<OptionOrder> optionOrders);
    }
}

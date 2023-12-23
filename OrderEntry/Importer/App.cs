using Microsoft.Extensions.Logging;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;
using OrderEntry.Utils;

namespace Importer
{
    public class App
    {
        private readonly ILogger<App> logger;
        private readonly IMindfulTraderService mindfulTraderService;
        private readonly IDatabaseService databaseService;

        public App(ILogger<App> logger, IMindfulTraderService mindfulTraderService, IDatabaseService databaseService)
        {
            this.logger = logger;
            this.mindfulTraderService = mindfulTraderService;
            this.databaseService = databaseService;
        }

        public async Task Run()
        {
            using var session = await mindfulTraderService.GetSession();
            var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(1));
            DateOnly? previousWatchDate = null;
            while (await periodicTimer.WaitForNextTickAsync())
            {
                var watchDate = DateUtils.TodayEST;
                if (previousWatchDate != null && previousWatchDate == watchDate)
                {
                    logger.LogInformation("Watch Date hasn't changed from {watchDate}, skipping", watchDate);
                    continue;
                }
                else
                {
                    previousWatchDate = watchDate;
                    logger.LogInformation("Watch Date is {watchDate}", watchDate);
                }

                var earliestDeleteDate = watchDate.AddDays(-14);
                
                var deletedStockOrderCount = await databaseService.DeleteStockOrders(earliestDeleteDate);
                logger.LogInformation("Deleted {count} stock orders before {watchDate}", deletedStockOrderCount, earliestDeleteDate);

                var deletedOptionOrderCount = await databaseService.DeleteOptionOrders(earliestDeleteDate);
                logger.LogInformation("Deleted {count} option orders before {watchDate}", deletedOptionOrderCount, earliestDeleteDate);

                var parseSettings = await databaseService.GetParseSettings();
                if (parseSettings.Count == 0)
                    logger.LogWarning("No active parse settings found in database");
                foreach (var parseSetting in parseSettings)
                {
                    if (parseSetting.ParseType != ParseTypes.Watchlist)
                    {
                        logger.LogWarning("Only watchlist import supported, skipping {parseSetting}", parseSetting);
                        continue;
                    }
                    if (parseSetting.Mode == Modes.LowPricedStock)
                    {
                        logger.LogWarning("Low priced stock import is not supported, skipping {parseSetting}", parseSetting);
                        continue;
                    }
                    logger.LogInformation("Getting orders for {parseSetting}", parseSetting);
                    if (parseSetting.Mode == Modes.Stock)
                    {
                        if (await databaseService.HasStockOrders(parseSetting.Key, watchDate))
                        {
                            logger.LogInformation("The database already has stock orders for the watched date");
                            continue;
                        }

                        var orders = await session.GetStockOrders(parseSetting.Key, parseSetting.Strategy, parseSetting.AccountBalance);
                        var validOrders = orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).ToList();
                        logger.LogInformation("Got {count} stock orders, saving {validCount} valid orders", orders.Count, validOrders.Count);
                        await databaseService.Save(validOrders);
                    }
                    else if (parseSetting.Mode == Modes.Option)
                    {
                        if (await databaseService.HasOptionOrders(parseSetting.Key, watchDate))
                        {
                            logger.LogInformation("The database already has option orders for the watched date");
                            continue;
                        }

                        var orders = await session.GetOptionOrders(parseSetting.Key, parseSetting.Strategy, parseSetting.AccountBalance);
                        var validOrders = orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).ToList();
                        logger.LogInformation("Got {count} option orders, saving {validCount} valid orders", orders.Count, validOrders.Count);
                        await databaseService.Save(validOrders);
                    }
                }
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;
using OrderEntry.Utils;

namespace Importer
{
    public class App(ILogger<App> logger, IMindfulTraderService mindfulTraderService, IDatabaseService databaseService)
    {
        private readonly ILogger<App> logger = logger;
        private readonly IMindfulTraderService mindfulTraderService = mindfulTraderService;
        private readonly IDatabaseService databaseService = databaseService;

        public async Task Run()
        {
            var watchDate = DateUtils.TodayEST;
            var earliestDeleteDate = watchDate.AddDays(-14);

            logger.LogInformation("Opening mindful trader session");
            using var session = await mindfulTraderService.GetSession();

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

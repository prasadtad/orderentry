using Microsoft.Extensions.Logging;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;
using OrderEntry.Utils;

namespace Importer
{
    public class App(ILogger<App> logger, IMindfulTraderService mindfulTraderService, IDatabaseService databaseService, IInteractiveBrokersService interactiveBrokersService, ICharlesSchwabService charlesSchwabService)
    {
        private const decimal RiskFactor = 0.75m;

        public async Task Run()
        {
            var parseSettings = await databaseService.GetParseSettings();
            if (parseSettings.Count == 0)
                logger.LogWarning("No active parse settings found in database");

            await SyncStockPositions(parseSettings);
            await SyncOrders(parseSettings);
        }

        public async Task SyncStockPositions(List<ParseSetting> parseSettings)
        {
            await SyncStockPositions(Brokers.CharlesSchwab, parseSettings.Where(p => p.Broker == Brokers.CharlesSchwab).ToList());
            await SyncStockPositions(Brokers.InteractiveBrokers, parseSettings.Where(p => p.Broker == Brokers.InteractiveBrokers).ToList());
        }

        private async Task SyncStockPositions(Brokers broker, List<ParseSetting> parseSettings)
        {
            var dbPositions = await databaseService.GetStockPositions(broker);

            logger.LogInformation("{count} {broker} positions in database", dbPositions.Count, broker);

            List<StockPosition> positions;
            decimal? charlesSchwabAccountBalance = null;
            if (broker == Brokers.CharlesSchwab)
            {
                logger.LogInformation("Opening {broker} session", broker);
                using var session = await charlesSchwabService.GetSession();
                (charlesSchwabAccountBalance, positions) = await session.GetStockPositions((ticker) => dbPositions.SingleOrDefault(p => p.Ticker == ticker)?.ActivelyTrade ?? true);                
                if (charlesSchwabAccountBalance != null) {
                    parseSettings.Single().AccountBalance = charlesSchwabAccountBalance.Value * RiskFactor;
                    logger.LogInformation("Charles Schwab account balance is ${balance}, using {adjustedBalance} for mindful trader", charlesSchwabAccountBalance, parseSettings.Single().AccountBalance);
                }
            }
            else 
            {
                positions = await interactiveBrokersService.GetStockPositions((accountId, ticker) => dbPositions.SingleOrDefault(p => p.AccountId == accountId && p.Ticker == ticker)?.ActivelyTrade ?? true);
                foreach (var parseSetting in parseSettings)
                {
                    var interactiveBrokersAccountBalance = await interactiveBrokersService.GetAccountValue(parseSetting.AccountId);                    
                    if (interactiveBrokersAccountBalance != null) {
                        parseSetting.AccountBalance = interactiveBrokersAccountBalance.Value * RiskFactor;
                        logger.LogInformation("{parseSetting} account balance is ${balance}, using {adjustedBalance} for mindful trader", parseSetting, interactiveBrokersAccountBalance, parseSetting.AccountBalance);
                    }
                }
            }
            await databaseService.Save(parseSettings);

            logger.LogInformation("{count} {broker} positions", positions.Count, broker);

            var deletes = new List<StockPosition>();
            var inserts = new List<StockPosition>();
            var updates = new List<StockPosition>();
            foreach (var dbPosition in dbPositions)
            {
                if (!positions.Any(p => p.AccountId == dbPosition.AccountId && p.Ticker == dbPosition.Ticker))
                    deletes.Add(dbPosition);
            }
            foreach (var position in positions)
            {
                var dbPosition = dbPositions.SingleOrDefault(p => p.AccountId == position.AccountId && p.Ticker == position.Ticker);                
                if (dbPosition == null)
                    inserts.Add(position);
                else if (dbPosition.Count != position.Count || dbPosition.AverageCost != position.AverageCost)
                    updates.Add(position);
            }

            var deletedCount = await databaseService.Delete(deletes);
            logger.LogInformation("Deleted {count} {broker} {positions}", deletedCount, broker, deletes);

            await databaseService.Insert(inserts);
            logger.LogInformation("Inserted {count} {broker} {positions}", inserts.Count, broker, inserts);

            await databaseService.Update(updates);
            logger.LogInformation("Updated {count} {broker} {positions}", updates.Count, broker, updates);
        }

        private async Task SyncOrders(List<ParseSetting> parseSettings)
        {
            var watchDate = DateUtils.TodayEST;
            var earliestDeleteDate = watchDate.AddDays(-7);

            var deletedStockOrderCount = await databaseService.DeleteStockOrders(earliestDeleteDate);
            logger.LogInformation("Deleted {count} stock orders before {watchDate}", deletedStockOrderCount, earliestDeleteDate);

            var deletedOptionOrderCount = await databaseService.DeleteOptionOrders(earliestDeleteDate);
            logger.LogInformation("Deleted {count} option orders before {watchDate}", deletedOptionOrderCount, earliestDeleteDate);

            logger.LogInformation("Opening mindful trader session");
            using var session = await mindfulTraderService.GetSession();

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
                    await SyncStockOrders(session, parseSetting);
                }
                else if (parseSetting.Mode == Modes.Option)
                {
                    await SyncOptionOrders(session, parseSetting);
                }
            }
        }

        private async Task SyncStockOrders(MindfulTraderSession session, ParseSetting parseSetting)
        {
            var watchDate = DateUtils.TodayEST;

            if (await databaseService.HasStockOrders(parseSetting.Key, watchDate))
            {
                logger.LogInformation("The database already has stock orders for the watched date");
                return;
            }

            var orders = await session.GetStockOrders(parseSetting.Key, parseSetting.Strategy, parseSetting.AccountBalance);
            var validOrders = orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).ToList();
            logger.LogInformation("Got {count} stock orders, saving {validCount} valid orders", orders.Count, validOrders.Count);
            await databaseService.Save(validOrders);
        }

        private async Task SyncOptionOrders(MindfulTraderSession session, ParseSetting parseSetting)
        {
            var watchDate = DateUtils.TodayEST;

            if (await databaseService.HasOptionOrders(parseSetting.Key, watchDate))
            {
                logger.LogInformation("The database already has option orders for the watched date");
                return;
            }

            var orders = await session.GetOptionOrders(parseSetting.Key, parseSetting.Strategy, parseSetting.AccountBalance);
            var validOrders = orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).ToList();
            logger.LogInformation("Got {count} option orders, saving {validCount} valid orders", orders.Count, validOrders.Count);
            await databaseService.Save(validOrders);
        }
    }
}

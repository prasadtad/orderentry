using Microsoft.Extensions.Logging;
using OrderEntry.Brokerages;
using OrderEntry.Database;

namespace AutoTrader
{
    public class App(ILogger<App> logger, IInteractiveBrokersService interactiveBrokersService, ICharlesSchwabService charlesSchwabService, IDatabaseService databaseService)
    {
        private readonly ILogger<App> logger = logger;
        private readonly IInteractiveBrokersService interactiveBrokersService = interactiveBrokersService;
        private readonly ICharlesSchwabService charlesSchwabService = charlesSchwabService;
        private readonly IDatabaseService databaseService = databaseService;

        public async Task Run()
        {
            var dbPositions = await databaseService.GetStockPositions();
            //await SyncInteractiveBrokersStock(dbPositions.Where(p => p.Broker == OrderEntry.MindfulTrader.Brokers.InteractiveBrokers).ToList());
            await SyncCharlesSchwabStock(dbPositions.Where(p => p.Broker == OrderEntry.MindfulTrader.Brokers.CharlesSchwab).ToList());
        }

        private async Task SyncInteractiveBrokersStock(List<StockPosition> interactiveBrokersDbPositions)
        {
            var comparer = new StockPositionStockComparer();

            logger.LogInformation("{count} interactive brokers {positions} in database", interactiveBrokersDbPositions.Count, interactiveBrokersDbPositions);

            var interactiveBrokersPositions = await interactiveBrokersService.GetStockPositions((accountId, ticker) => interactiveBrokersDbPositions
                .SingleOrDefault(p => p.AccountId == accountId && p.Ticker == ticker)
                    ?.ActivelyTrade ?? true);
            logger.LogInformation("{count} interactive brokers {positions}", interactiveBrokersPositions.Count, interactiveBrokersPositions);

            var deletes = interactiveBrokersDbPositions.Except(interactiveBrokersPositions, comparer).ToList();

            var deletedCount = await databaseService.Delete(deletes);
            logger.LogInformation("Deleted {count} interactive brokers positions", deletedCount);

            var inserts = interactiveBrokersPositions.Except(interactiveBrokersDbPositions, comparer).ToList();
            await databaseService.Insert(inserts);
            logger.LogInformation("Inserted {count} interactive brokers positions", inserts.Count);

            var updates = interactiveBrokersPositions.Intersect(interactiveBrokersDbPositions, comparer).ToList();
            await databaseService.Update(updates);
            logger.LogInformation("Updated {count} interactive brokers positions", updates.Count);
        }

        private async Task SyncCharlesSchwabStock(List<StockPosition> charlesSchwabDbPositions)
        {
            var comparer = new StockPositionStockComparer();           
            
            logger.LogInformation("{count} charles schwab {positions} in database", charlesSchwabDbPositions.Count, charlesSchwabDbPositions);

            var charlesSchwabPositions = await charlesSchwabService.GetStockPositions((ticker) => charlesSchwabDbPositions
                .SingleOrDefault(p => p.Ticker == ticker)
                    ?.ActivelyTrade ?? true);
            logger.LogInformation("{count} charles schwab {positions}", charlesSchwabPositions.Count, charlesSchwabPositions);

            var deletes = charlesSchwabDbPositions.Except(charlesSchwabPositions, comparer).ToList();

            var deletedCount = await databaseService.Delete(deletes);
            logger.LogInformation("Deleted {count} charles schwab positions", deletedCount);

            var inserts = charlesSchwabPositions.Except(charlesSchwabDbPositions, comparer).ToList();
            await databaseService.Insert(inserts);
            logger.LogInformation("Inserted {count} charles schwab positions", inserts.Count);

            var updates = charlesSchwabPositions.Intersect(charlesSchwabDbPositions, comparer).ToList();
            await databaseService.Update(updates);
            logger.LogInformation("Updated {count} charles schwab positions", updates.Count);
        }
    }
}

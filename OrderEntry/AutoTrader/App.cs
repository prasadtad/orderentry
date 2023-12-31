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
            await SyncInteractiveBrokersStock();
        }

        private async Task SyncInteractiveBrokersStock()
        {
            var comparer = new InteractiveBrokersStockComparer();

            var dbPositions = await databaseService.GetInteractiveBrokersStocks();
            logger.LogInformation("{count} {positions} in database", dbPositions.Count, dbPositions);

            var positions = await interactiveBrokersService.GetStockPositions((accountId, ticker) => dbPositions
                .SingleOrDefault(p => p.AccountId == accountId && p.Ticker == ticker)
                    ?.ActivelyTrade ?? true);
            logger.LogInformation("{count} interactive brokers {positions}", positions.Count, positions);

            var deletes = dbPositions.Except(positions, comparer).ToList();

            var deletedCount = await databaseService.Delete(deletes);
            logger.LogInformation("Deleted {count} positions", deletedCount);

            var inserts = positions.Except(dbPositions, comparer).ToList();
            await databaseService.Insert(inserts);
            logger.LogInformation("Inserted {count} positions", inserts.Count);

            var updates = positions.Intersect(dbPositions, comparer).ToList();
            await databaseService.Update(updates);
            logger.LogInformation("Updated {count} positions", updates.Count);
        }
    }
}

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

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

                if (parseSetting.Broker == Brokers.CharlesSchwab)
                {
                    var session = await charlesSchwabService.GetSession();
                    try
                    {
                        var p = await session.GetStockPositions(x => true);
                        Console.WriteLine(p);
                    }
                    finally
                    {
                        await session.DisposeAsync();
                    }
                }
            }
        }       
    }
}

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
            
        }       
    }
}

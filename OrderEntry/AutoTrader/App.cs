using Microsoft.Extensions.Logging;
using OrderEntry.Apis;
using OrderEntry.Brokerages;
using OrderEntry.Database;

namespace AutoTrader
{
    public class App(ILogger<App> logger, IInteractiveBrokersService interactiveBrokersService, ICharlesSchwabService charlesSchwabService, IDatabaseService databaseService, IPolygonApiService polygonApiService)
    {
        private readonly ILogger<App> logger = logger;
        private readonly IInteractiveBrokersService interactiveBrokersService = interactiveBrokersService;
        private readonly ICharlesSchwabService charlesSchwabService = charlesSchwabService;
        private readonly IDatabaseService databaseService = databaseService;
        private readonly IPolygonApiService polygonApiService = polygonApiService;

        public async Task Run()
        {
            await polygonApiService.FillEndOfDayData(90, "SPY");
        }
    }
}

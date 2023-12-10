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
            var parseSettings = await databaseService.GetParseSettings();
            List<IOrder>? orders = null;
            var watchDate = DateUtils.TodayEST;
            foreach (var parseSetting in parseSettings)
            {                
                if (parseSetting.Mode == Modes.Stock &&
                    !await databaseService.HasStockOrders(parseSetting.Key, watchDate))
                {
                    orders ??= await mindfulTraderService.GetOrders(parseSettings);
                    await databaseService.Save(orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).Cast<StockOrder>().ToList());
                }
                if (parseSetting.Mode == Modes.Option &&
                    !await databaseService.HasOptionOrders(parseSetting.Key, watchDate))
                {
                    orders ??= await mindfulTraderService.GetOrders(parseSettings);
                    await databaseService.Save(orders.Where(o => o.ParseSettingKey == parseSetting.Key && o.WatchDate == watchDate && o.Count > 0).Cast<OptionOrder>().ToList());
                }
            }
        }
    }
}

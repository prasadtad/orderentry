using OrderEntry.Brokers;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

namespace OrderEntry
{
    public class App
    {
        private readonly IInteractiveBrokersService interactiveBrokersService;
        private readonly ICharlesSchwabService charlesSchwabService;
        private readonly IMindfulTraderService mindfulTraderService;
        private readonly IDatabaseService databaseService;

        public App(IInteractiveBrokersService interactiveBrokersService, ICharlesSchwabService charlesSchwabService, IMindfulTraderService mindfulTraderService, IDatabaseService databaseService)
        {
            this.interactiveBrokersService = interactiveBrokersService;
            this.charlesSchwabService = charlesSchwabService;
            this.mindfulTraderService = mindfulTraderService;
            this.databaseService = databaseService;
        }

        public async Task Run()
        {
            DateOnly currentDateInEst = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")));

            var tradesWithOrders = new List<(ParseSetting parseSetting, List<IOrder> orders)>();
            foreach (var parseSetting in await databaseService.GetParseSettings())
                tradesWithOrders.Add((parseSetting, (await mindfulTraderService.GetWatchlist(parseSetting))
                    .Where(s => s.WatchDate == currentDateInEst && s.Count > 0).ToList()));

            var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));            
            while (await periodicTimer.WaitForNextTickAsync())
            {
                foreach (var (parseSetting, orders) in tradesWithOrders)
                {
                    await ReSubmitOrders(parseSetting, orders);
                }
            }
        }

        private async Task ReSubmitOrders(ParseSetting parseSetting, List<IOrder> orders)
        {
            var ordersWithoutPositions = await interactiveBrokersService.GetOrdersWithoutPositions(orders);
            var ordersWithPositions = orders.Except(ordersWithoutPositions).ToList();
            var ordersWithPrices = new List<(IOrder order, decimal price)>();
            foreach (var order in ordersWithoutPositions)
            {
                if (order is OptionOrder) throw new NotImplementedException();

                var price = await interactiveBrokersService.GetCurrentPrice(order.Ticker);
                if (price != null) ordersWithPrices.Add((order, price.Value));
            }

            var balance = ordersWithPositions.Sum(o => o.PositionValue);
            if (balance >= parseSetting.AccountBalance)
            {
                Console.WriteLine($"IB orders with positions balance {balance} >= allowed {parseSetting.AccountBalance}");
                return;
            }

            foreach (var (order, price) in ordersWithPrices.OrderByDescending(o => o.price / o.order.PotentialEntry))
            {
                if (await interactiveBrokersService.Submit((StockOrder)order))
                {
                    balance += order.PositionValue;
                    Console.WriteLine($"Submitted {order}");
                }

                if (balance > parseSetting.AccountBalance) break;
            }
        }
    }
}

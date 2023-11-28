using OrderEntry.Brokers;
using OrderEntry.MindfulTrader;

namespace OrderEntry
{
    public class App
    {
        private readonly IInteractiveBrokersService interactiveBrokersService;
        private readonly ITDAmeritradeService ameritradeService;
        private readonly IParserService parserService;

        public App(IInteractiveBrokersService interactiveBrokersService, ITDAmeritradeService ameritradeService, IParserService parserService)
        {
            this.interactiveBrokersService = interactiveBrokersService;
            this.ameritradeService = ameritradeService;
            this.parserService = parserService;
        }

        public async Task Run()
        {
            DateOnly currentDateInEst = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")));

            var tradesWithOrders = new List<(TradeSettings tradeSettings, List<IOrder> orders)>();
            foreach (var tradeSetting in interactiveBrokersService.Trades)
                tradesWithOrders.Add((tradeSetting, (await parserService.GetWatchlist(tradeSetting))
                    .Where(s => s.WatchDate == currentDateInEst && s.Count > 0).ToList()));

            var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));            
            while (await periodicTimer.WaitForNextTickAsync())
            {
                foreach (var (tradeSettings, orders) in tradesWithOrders)
                {
                    await ReSubmitOrders(tradeSettings, orders);
                }
            }
        }

        private async Task ReSubmitOrders(TradeSettings tradeSettings, List<IOrder> orders)
        {
            var ordersWithoutPositions = await interactiveBrokersService.GetOrdersWithoutPositions(orders);
            var ordersWithPositions = orders.Except(ordersWithoutPositions).ToList();
            var ordersWithPrices = new List<(IOrder order, double price)>();
            foreach (var order in ordersWithoutPositions)
            {
                if (order is OptionOrder) throw new NotImplementedException();

                var price = await interactiveBrokersService.GetCurrentPrice(order.Ticker);
                if (price != null) ordersWithPrices.Add((order, price.Value));
            }

            double balance = ordersWithPositions.Sum(o => o.PositionValue);
            if (balance >= tradeSettings.AccountBalance)
            {
                Console.WriteLine($"IB orders with positions balance {balance} >= allowed {tradeSettings.AccountBalance}");
                return;
            }

            foreach (var (order, price) in ordersWithPrices.OrderByDescending(o => o.price / o.order.PotentialEntry))
            {
                if (await interactiveBrokersService.Submit((StockOrder)order))
                {
                    balance += order.PositionValue;
                    Console.WriteLine($"Submitted {order}");
                }

                if (balance > tradeSettings.AccountBalance) break;
            }
        }
    }
}

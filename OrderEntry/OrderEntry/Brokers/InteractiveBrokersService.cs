using AutoFinance.Broker.InteractiveBrokers;
using AutoFinance.Broker.InteractiveBrokers.Constants;
using IBApi;
using Microsoft.Extensions.Options;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokers
{
    public class InteractiveBrokersService : IInteractiveBrokersService
    {
        private readonly IOptions<InteractiveBrokersSettings> options;
        private readonly TwsObjectFactory twsObjectFactory;

        public InteractiveBrokersService(IOptions<InteractiveBrokersSettings> options)
        {
            this.options = options;
            this.twsObjectFactory = new TwsObjectFactory("localhost", options.Value.Port, 1);
        }

        public async Task Display()
        {
            Console.WriteLine($"IB: Connecting...");

            var twsController = twsObjectFactory.TwsControllerBase;
            await twsController.EnsureConnectedAsync();

            Console.WriteLine($"IB: Getting {options.Value.AccountId} account details...");
            var account = await twsController.GetAccountDetailsAsync(options.Value.AccountId);
            foreach (var key in account.Keys.OrderBy(k => k))
                Console.WriteLine($"{key}={account[key]}");
        }

        public async Task<double?> GetCurrentPrice(string ticker)
        {
            var twsController = twsObjectFactory.TwsControllerBase;
            await twsController.EnsureConnectedAsync();
            
            var contract = new Contract
            {
                SecType = TwsContractSecType.Stock,
                Symbol = ticker,
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd
            };

            Dictionary<int, double> askPrices = new Dictionary<int, double>();
            twsObjectFactory.TwsCallbackHandler.TickPriceEvent += (sender, args) =>
            {
                if (args.Field == 2)  // Ask price
                    askPrices.Add(args.TickerId, args.Price);
            };

            twsController.RequestMarketDataType(2);   // Frozen
            
            var result = await twsController.RequestMarketDataAsync(contract, "233", true, false, null);
            await Task.Delay(10000);
            twsController.CancelMarketData(result.TickerId);

            if (askPrices.Count == 0 || !askPrices.TryGetValue(result.TickerId, out var askPrice))
                return null;

            return askPrice;
        }

        public Task<(double price, string tradingClass)?> GetCurrentPrice(string ticker, double strikePrice, DateOnly strikeDate, OptionType type)
        {
            throw new NotImplementedException();
        }

        public async Task<List<IOrder>> GetOrdersWithoutPositions(IEnumerable<IOrder> orders)
        {
            var twsController = twsObjectFactory.TwsControllerBase;

            await twsController.EnsureConnectedAsync();

            var positions = await twsController.RequestPositions();
            var ordersWithoutPositions = new List<IOrder>();
            foreach (var order in orders)
            {
                if (positions.Any(p => p.Account == options.Value.AccountId && p.Contract.Symbol == order.Ticker))
                    continue;

                ordersWithoutPositions.Add(order);
            }
            return ordersWithoutPositions;
        }

        public async Task<bool> Submit(StockOrder stockOrder)
        {
            var twsController = twsObjectFactory.TwsControllerBase;

            Console.WriteLine($"IB: Submitting stock order to {options.Value.AccountId}");
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Stock,
                Symbol = stockOrder.Ticker,
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd
            };

            int entryOrderId = await twsController.GetNextValidIdAsync();
            var takeProfitOrderId = await twsController.GetNextValidIdAsync();
            var stopOrderId = await twsController.GetNextValidIdAsync();

            Order entryOrder = new Order()
            {
                Account = options.Value.AccountId,
                Action = TwsOrderActions.Buy,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = stockOrder.Count,
                LmtPrice = stockOrder.PotentialEntry,
                Tif = "Day",
                Transmit = false,
            };

            Order takeProfit = new Order()
            {
                Account = options.Value.AccountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = stockOrder.Count,
                LmtPrice = stockOrder.PotentialProfit,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = false,
            };

            Order stopLoss = new Order()
            {
                Account = options.Value.AccountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.StopLoss,
                TotalQuantity = stockOrder.Count,
                AuxPrice = stockOrder.PotentialStop,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = true,
            };

            var entryOrderAckTask = twsController.PlaceOrderAsync(entryOrderId, contract, entryOrder);
            var takeProfitOrderAckTask = twsController.PlaceOrderAsync(takeProfitOrderId, contract, takeProfit);
            var stopOrderAckTask = twsController.PlaceOrderAsync(stopOrderId, contract, stopLoss);
            return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask, stopOrderAckTask)).All(result => result);
        }

        public async Task<bool> Submit(OptionOrder optionOrder, string tradingClass)
        {
            var twsController = twsObjectFactory.TwsControllerBase;

            Console.WriteLine($"IB: Submitting option order to {options.Value.AccountId}");
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Option,
                Symbol = optionOrder.Ticker,
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd,
                LastTradeDateOrContractMonth = optionOrder.StrikeDate.ToString("yyyyMMdd"),
                Strike = optionOrder.StrikePrice,
                Right = optionOrder.Type == OptionType.Call ? "C" : throw new NotImplementedException(),
                Multiplier = "100",
                TradingClass = tradingClass
            };

            int entryOrderId = await twsController.GetNextValidIdAsync();
            var takeProfitOrderId = await twsController.GetNextValidIdAsync();

            Order entryOrder = new Order()
            {
                Account = options.Value.AccountId,
                Action = TwsOrderActions.Buy,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = optionOrder.Count,
                LmtPrice = optionOrder.PotentialEntry,
                Tif = "Day",
                Transmit = false,
            };

            Order takeProfit = new Order()
            {
                Account = options.Value.AccountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = optionOrder.Count,
                LmtPrice = optionOrder.PotentialProfit,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = false,
            };            

            var entryOrderAckTask = twsController.PlaceOrderAsync(entryOrderId, contract, entryOrder);
            var takeProfitOrderAckTask = twsController.PlaceOrderAsync(takeProfitOrderId, contract, takeProfit);
            return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask)).All(result => result);
        }
    }

    public interface IInteractiveBrokersService
    {
        Task Display();

        Task<double?> GetCurrentPrice(string ticker);

        Task<(double price, string tradingClass)?> GetCurrentPrice(string ticker, double strikePrice, DateOnly strikeDate, OptionType type);

        Task<List<IOrder>> GetOrdersWithoutPositions(IEnumerable<IOrder> orders);

        Task<bool> Submit(StockOrder order);

        Task<bool> Submit(OptionOrder optionOrder, string tradingClass);
    }

    public class InteractiveBrokersSettings
    {
        public required string AccountId { get; set; }

        public required int Port { get; set; }
    }
}

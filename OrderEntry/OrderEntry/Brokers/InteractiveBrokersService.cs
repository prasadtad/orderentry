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

        public TradeSettings[] Trades => options.Value.Trades;

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

            var askPrices = new Dictionary<int, double>();
            twsObjectFactory.TwsCallbackHandler.TickPriceEvent += (sender, args) =>
            {
                if (args.Field == 2)  // Ask price
                    askPrices.Add(args.TickerId, args.Price);
            };

            twsController.RequestMarketDataType(1);   // Live
            
            var result = await twsController.RequestMarketDataAsync(contract, string.Empty, true, false, null);
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(1000);
                if (askPrices.Count > 0) break;
            }
            twsController.CancelMarketData(result.TickerId);

            if (askPrices.Count == 0 || !askPrices.TryGetValue(result.TickerId, out var askPrice))
                return null;

            return askPrice;
        }

        public async Task<(double price, string tradingClass)?> GetCurrentPrice(string ticker, double strikePrice, DateOnly strikeDate, OptionType type)
        {
            var twsController = twsObjectFactory.TwsControllerBase;
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Option,
                Symbol = ticker,
                Strike = strikePrice,
                LastTradeDateOrContractMonth = strikeDate.ToString("yyyyMMdd"),
                Right = type == OptionType.Call ? "C" : throw new NotImplementedException(),
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

            return (askPrice, "");
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

            if (stockOrder.EntryOrderId < 0)
                stockOrder.EntryOrderId = await twsController.GetNextValidIdAsync();

            if (stockOrder.ProfitOrderId < 0)
                stockOrder.ProfitOrderId = await twsController.GetNextValidIdAsync();

            if (stockOrder.StopOrderId < 0)
                stockOrder.StopOrderId = await twsController.GetNextValidIdAsync();

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
                ParentId = stockOrder.EntryOrderId,
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
                ParentId = stockOrder.EntryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = true,
            };

            var entryOrderAckTask = twsController.PlaceOrderAsync(stockOrder.EntryOrderId, contract, entryOrder);
            var takeProfitOrderAckTask = twsController.PlaceOrderAsync(stockOrder.ProfitOrderId, contract, takeProfit);
            var stopOrderAckTask = twsController.PlaceOrderAsync(stockOrder.StopOrderId, contract, stopLoss);
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
                LocalSymbol = $"{(optionOrder.Type == OptionType.Call ? "C" : throw new NotImplementedException())} {optionOrder.Ticker}  {optionOrder.StrikeDate:yyyyMMdd} 72 M",
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


            if (optionOrder.EntryOrderId < 0)
                optionOrder.EntryOrderId = await twsController.GetNextValidIdAsync();

            if (optionOrder.ProfitOrderId < 0)
                optionOrder.ProfitOrderId = await twsController.GetNextValidIdAsync();

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
                ParentId = optionOrder.EntryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = false,
            };            

            var entryOrderAckTask = twsController.PlaceOrderAsync(optionOrder.EntryOrderId, contract, entryOrder);
            var takeProfitOrderAckTask = twsController.PlaceOrderAsync(optionOrder.ProfitOrderId, contract, takeProfit);
            return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask)).All(result => result);
        }
    }

    public interface IInteractiveBrokersService
    {
        TradeSettings[] Trades { get; }

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

        public required TradeSettings[] Trades { get; set; }
    }
}

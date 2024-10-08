﻿using AutoFinance.Broker.InteractiveBrokers;
using AutoFinance.Broker.InteractiveBrokers.Constants;
using AutoFinance.Broker.InteractiveBrokers.EventArgs;
using IBApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokerages
{
    public class InteractiveBrokersService : IInteractiveBrokersService
    {
        private readonly TwsObjectFactory twsObjectFactory;
        private readonly ILogger<InteractiveBrokersService> logger;

        public InteractiveBrokersService(IOptions<InteractiveBrokersSettings> options, ILogger<InteractiveBrokersService> logger)
        {
            this.twsObjectFactory = new TwsObjectFactory("localhost", options.Value.Port, 1);
            this.logger = logger;
        }

        public async Task<decimal?> GetAccountValue(string account)
        {            
            var twsController = twsObjectFactory.TwsControllerBase;
            await twsController.EnsureConnectedAsync();

            logger.LogDebug("Getting {account} details...", account);
            var ad = await twsController.GetAccountDetailsAsync(account);
            var details = ad.ToDictionary();
            if (details["Currency"] != "USD") {
                logger.LogError("Currency should be USD in {details}", details);
                return null;
            }
            if (!decimal.TryParse(details["NetLiquidation"], out var netLiquidation)) return null;
            if (!decimal.TryParse(details["ExchangeRate"], out var exchangeRate)) return null;
            return netLiquidation / exchangeRate;
        }

        public async Task<decimal?> GetCurrentPrice(string account, string ticker)
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

            var askPrices = new Dictionary<int, decimal>();
            void tickPriceHandler(object? sender, TickPriceEventArgs args)
            {
                if (args.Field == 2)  // Ask price
                    askPrices.Add(args.TickerId, Convert.ToDecimal(args.Price));
            }

            int requestId;
            try
            {
                twsObjectFactory.TwsCallbackHandler.TickPriceEvent += tickPriceHandler;

                twsController.RequestMarketDataType(4);   // Frozen

                requestId = (await twsController.RequestMarketDataAsync(contract, string.Empty, true, false, null))
                                .TickerId;
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(1000);
                    if (askPrices.Count > 0) break;
                }
            }
            finally
            {
                twsObjectFactory.TwsCallbackHandler.TickPriceEvent -= tickPriceHandler;
            }

            twsController.CancelMarketData(requestId);

            if (askPrices.Count == 0 || !askPrices.TryGetValue(requestId, out var askPrice))
                return null;

            return askPrice;
        }

        public async Task<(decimal price, string tradingClass)?> GetCurrentPrice(string account, string ticker, decimal strikePrice, DateOnly strikeDate, OptionTypes type)
        {
            var twsController = twsObjectFactory.TwsControllerBase;
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Option,
                Symbol = ticker,
                Strike = Convert.ToDouble(strikePrice),
                LastTradeDateOrContractMonth = strikeDate.ToString("yyyyMMdd"),
                Right = type == OptionTypes.Call ? "C" : throw new NotImplementedException(),
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd
            };

            Dictionary<int, decimal> askPrices = new Dictionary<int, decimal>();
            twsObjectFactory.TwsCallbackHandler.TickPriceEvent += (sender, args) =>
            {
                if (args.Field == 2)  // Ask price
                    askPrices.Add(args.TickerId, Convert.ToDecimal(args.Price));
            };

            twsController.RequestMarketDataType(2);   // Frozen

            var result = await twsController.RequestMarketDataAsync(contract, "233", true, false, null);
            await Task.Delay(10000);
            twsController.CancelMarketData(result.TickerId);

            if (askPrices.Count == 0 || !askPrices.TryGetValue(result.TickerId, out var askPrice))
                return null;

            return (askPrice, "");
        }

        public async Task<List<IOrder>> GetOrdersWithoutPositions(string account, IEnumerable<IOrder> orders)
        {
            var twsController = twsObjectFactory.TwsControllerBase;

            await twsController.EnsureConnectedAsync();

            var positions = await twsController.RequestPositions();
            var ordersWithoutPositions = new List<IOrder>();
            foreach (var order in orders)
            {
                if (positions.Any(p => p.Account == account && p.Contract.Symbol == order.Ticker))
                    continue;

                ordersWithoutPositions.Add(order);
            }
            return ordersWithoutPositions;
        }

        public async Task<List<StockPosition>> GetStockPositions(Func<string, string, bool> isActivelyTrade)
        {
            var twsController = twsObjectFactory.TwsControllerBase;

            await twsController.EnsureConnectedAsync();

            var positions = await twsController.RequestPositions();

            return positions.Select(p => new StockPosition
            {
                Broker = Brokers.InteractiveBrokers,
                AccountId = p.Account,
                AverageCost = Convert.ToDecimal(p.AverageCost),
                Count = Convert.ToDecimal(p.Position),
                Ticker = p.Contract.Symbol,
                ActivelyTrade = isActivelyTrade(p.Account, p.Contract.Symbol)
            }).ToList();
        }

        public async Task<bool> Submit(string account, StockOrder stockOrder)
        {
            try
            {
                var twsController = twsObjectFactory.TwsControllerBase;

                logger.LogInformation("Submitting stock {order} to {account}", stockOrder, account);
                await twsController.EnsureConnectedAsync();

                var contract = new Contract
                {
                    SecType = TwsContractSecType.Stock,
                    Symbol = stockOrder.Ticker,
                    Exchange = TwsExchange.Smart,
                    PrimaryExch = TwsExchange.Island,
                    Currency = TwsCurrency.Usd
                };

                if (stockOrder.IBEntryOrderId == null)
                    stockOrder.IBEntryOrderId = await twsController.GetNextValidIdAsync();

                if (stockOrder.IBProfitOrderId == null)
                    stockOrder.IBProfitOrderId = await twsController.GetNextValidIdAsync();

                if (stockOrder.IBStopOrderId == null)
                    stockOrder.IBStopOrderId = await twsController.GetNextValidIdAsync();

                Order entryOrder = new Order()
                {
                    Account = account,
                    Action = TwsOrderActions.Buy,
                    OrderType = TwsOrderType.Limit,
                    TotalQuantity = stockOrder.Count,
                    LmtPrice = Convert.ToDouble(stockOrder.PotentialEntry),
                    Tif = "Day",
                    Transmit = false,
                };

                Order takeProfit = new Order()
                {
                    Account = account,
                    Action = TwsOrderActions.Sell,
                    OrderType = TwsOrderType.Limit,
                    TotalQuantity = stockOrder.Count,
                    LmtPrice = Convert.ToDouble(stockOrder.PotentialProfit),
                    ParentId = stockOrder.IBEntryOrderId.Value,
                    Tif = TwsTimeInForce.GoodTillClose,
                    Transmit = false,
                };

                Order stopLoss = new Order()
                {
                    Account = account,
                    Action = TwsOrderActions.Sell,
                    OrderType = TwsOrderType.StopLoss,
                    TotalQuantity = stockOrder.Count,
                    AuxPrice = Convert.ToDouble(stockOrder.PotentialStop),
                    ParentId = stockOrder.IBEntryOrderId.Value,
                    Tif = TwsTimeInForce.GoodTillClose,
                    Transmit = true,
                };

                var entryOrderAckTask = twsController.PlaceOrderAsync(stockOrder.IBEntryOrderId.Value, contract, entryOrder);
                var takeProfitOrderAckTask = twsController.PlaceOrderAsync(stockOrder.IBProfitOrderId.Value, contract, takeProfit);
                var stopOrderAckTask = twsController.PlaceOrderAsync(stockOrder.IBStopOrderId.Value, contract, stopLoss);
                return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask, stopOrderAckTask)).All(result => result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Couldn't submit stock {order} to {account}", stockOrder, account);
                return false;
            }
        }

        public async Task<bool> Submit(string account, OptionOrder optionOrder, string tradingClass)
        {
            try
            {
                var twsController = twsObjectFactory.TwsControllerBase;

                logger.LogInformation("Submitting option {order} to {account}", optionOrder, account);
                await twsController.EnsureConnectedAsync();

                var contract = new Contract
                {
                    SecType = TwsContractSecType.Option,
                    LocalSymbol = $"{(optionOrder.Type == OptionTypes.Call ? "C" : throw new NotImplementedException())} {optionOrder.Ticker}  {optionOrder.StrikeDate:yyyyMMdd} 72 M",
                    Symbol = optionOrder.Ticker,
                    Exchange = TwsExchange.Smart,
                    PrimaryExch = TwsExchange.Island,
                    Currency = TwsCurrency.Usd,
                    LastTradeDateOrContractMonth = optionOrder.StrikeDate.ToString("yyyyMMdd"),
                    Strike = Convert.ToDouble(optionOrder.StrikePrice),
                    Right = optionOrder.Type == OptionTypes.Call ? "C" : throw new NotImplementedException(),
                    Multiplier = "100",
                    TradingClass = tradingClass
                };


                if (optionOrder.IBEntryOrderId == null)
                    optionOrder.IBEntryOrderId = await twsController.GetNextValidIdAsync();

                if (optionOrder.IBProfitOrderId == null)
                    optionOrder.IBProfitOrderId = await twsController.GetNextValidIdAsync();

                Order entryOrder = new Order()
                {
                    Account = account,
                    Action = TwsOrderActions.Buy,
                    OrderType = TwsOrderType.Limit,
                    TotalQuantity = optionOrder.Count,
                    LmtPrice = Convert.ToDouble(optionOrder.PotentialEntry),
                    Tif = "Day",
                    Transmit = false,
                };

                Order takeProfit = new Order()
                {
                    Account = account,
                    Action = TwsOrderActions.Sell,
                    OrderType = TwsOrderType.Limit,
                    TotalQuantity = optionOrder.Count,
                    LmtPrice = Convert.ToDouble(optionOrder.PotentialProfit),
                    ParentId = optionOrder.IBEntryOrderId.Value,
                    Tif = TwsTimeInForce.GoodTillClose,
                    Transmit = false,
                };

                var entryOrderAckTask = twsController.PlaceOrderAsync(optionOrder.IBEntryOrderId.Value, contract, entryOrder);
                var takeProfitOrderAckTask = twsController.PlaceOrderAsync(optionOrder.IBProfitOrderId.Value, contract, takeProfit);
                return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask)).All(result => result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Couldn't submit option {order} to {account}", optionOrder, account);
                return false;
            }
        }
    }

    public interface IInteractiveBrokersService
    {
        Task<decimal?> GetAccountValue(string account);

        Task<decimal?> GetCurrentPrice(string account, string ticker);

        Task<(decimal price, string tradingClass)?> GetCurrentPrice(string account, string ticker, decimal strikePrice, DateOnly strikeDate, OptionTypes type);

        Task<List<IOrder>> GetOrdersWithoutPositions(string account, IEnumerable<IOrder> orders);

        Task<List<StockPosition>> GetStockPositions(Func<string, string, bool> isActivelyTrade);

        Task<bool> Submit(string account, StockOrder order);

        Task<bool> Submit(string account, OptionOrder optionOrder, string tradingClass);
    }
}

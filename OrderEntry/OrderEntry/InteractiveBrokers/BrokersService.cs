using System.Collections.Concurrent;
using AutoFinance.Broker.InteractiveBrokers;
using AutoFinance.Broker.InteractiveBrokers.Constants;
using AutoFinance.Broker.InteractiveBrokers.Controllers;
using IBApi;
using OrderEntry.MindfulTrader;

namespace OrderEntry.IB
{
    public class BrokersService : IBrokersService
    {
        public string AccountId { get; private set; }

        private readonly ITwsController twsController;

        public BrokersService(string accountId, TwsObjectFactory twsObjectFactory)
        {
            AccountId = accountId;
            twsController = twsObjectFactory.TwsController;
        }

        public async Task<ConcurrentDictionary<string, string>> GetAccountDetails()
        {
            await twsController.EnsureConnectedAsync();

            return await twsController.GetAccountDetailsAsync(AccountId);
        }

        public async Task<bool> Submit(WatchlistStock watchlistStock)
        {
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Stock,
                Symbol = watchlistStock.Ticker,
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd                
            };

            int entryOrderId = await twsController.GetNextValidIdAsync();
            var takeProfitOrderId = await twsController.GetNextValidIdAsync();
            var stopOrderId = await twsController.GetNextValidIdAsync();

            Order entryOrder = new Order()
            {
                Account = AccountId,
                Action = TwsOrderActions.Buy,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = watchlistStock.ShareCount,
                LmtPrice = watchlistStock.PotentialEntry,
                Tif = "Day",
                Transmit = false,
            };

            Order takeProfit = new Order()
            {
                Account = AccountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = watchlistStock.ShareCount,
                LmtPrice = watchlistStock.PotentialProfit,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = false,
            };

            Order stopLoss = new Order()
            {
                Account = AccountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.StopLoss,
                TotalQuantity = watchlistStock.ShareCount,
                AuxPrice = watchlistStock.PotentialStop,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = true,
            };

            var entryOrderAckTask = twsController.PlaceOrderAsync(entryOrderId, contract, entryOrder);
            var takeProfitOrderAckTask = twsController.PlaceOrderAsync(takeProfitOrderId, contract, takeProfit);
            var stopOrderAckTask = twsController.PlaceOrderAsync(stopOrderId, contract, stopLoss);
            return (await Task.WhenAll(entryOrderAckTask, takeProfitOrderAckTask, stopOrderAckTask)).All(result => result);            
        }
    }

    public interface IBrokersService
    {
        string AccountId { get; }

        Task<ConcurrentDictionary<string, string>> GetAccountDetails();

        Task<bool> Submit(WatchlistStock watchlistStock);
    }
}


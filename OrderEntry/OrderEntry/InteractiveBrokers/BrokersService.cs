using AutoFinance.Broker.InteractiveBrokers;
using AutoFinance.Broker.InteractiveBrokers.Constants;
using AutoFinance.Broker.InteractiveBrokers.Controllers;
using IBApi;
using OrderEntry.MindfulTrader;

namespace OrderEntry.IB
{
    public class BrokersService : IBrokersService
    {
        private readonly ITwsController twsController;

        public BrokersService(TwsObjectFactory twsObjectFactory)
        {
            twsController = twsObjectFactory.TwsController;
        }

        public async Task Display(string accountId)
        {
            Console.WriteLine($"IB: Connecting...");
            await twsController.EnsureConnectedAsync();

            Console.WriteLine($"IB: Getting {accountId} account details...");
            var account = await twsController.GetAccountDetailsAsync(accountId);
            foreach (var key in account.Keys.OrderBy(k => k))
                Console.WriteLine($"{key}={account[key]}");
        }

        public async Task<bool> Submit(string accountId, IOrder order)
        {
            if (order is not StockOrder) throw new NotImplementedException("IB: Only StockOrder supported");

            Console.WriteLine($"IB: Submitting order to {accountId}");
            await twsController.EnsureConnectedAsync();

            var contract = new Contract
            {
                SecType = TwsContractSecType.Stock,
                Symbol = order.Ticker,
                Exchange = TwsExchange.Smart,
                PrimaryExch = TwsExchange.Island,
                Currency = TwsCurrency.Usd                
            };

            int entryOrderId = await twsController.GetNextValidIdAsync();
            var takeProfitOrderId = await twsController.GetNextValidIdAsync();
            var stopOrderId = await twsController.GetNextValidIdAsync();

            Order entryOrder = new Order()
            {
                Account = accountId,
                Action = TwsOrderActions.Buy,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = order.Count,
                LmtPrice = order.PotentialEntry,
                Tif = "Day",
                Transmit = false,
            };

            Order takeProfit = new Order()
            {
                Account = accountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.Limit,
                TotalQuantity = order.Count,
                LmtPrice = order.PotentialProfit,
                ParentId = entryOrderId,
                Tif = TwsTimeInForce.GoodTillClose,
                Transmit = false,
            };

            Order stopLoss = new Order()
            {
                Account = accountId,
                Action = TwsOrderActions.Sell,
                OrderType = TwsOrderType.StopLoss,
                TotalQuantity = order.Count,
                AuxPrice = order.PotentialStop,
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
        Task Display(string accountId);

        Task<bool> Submit(string accountId, IOrder order);
    }
}


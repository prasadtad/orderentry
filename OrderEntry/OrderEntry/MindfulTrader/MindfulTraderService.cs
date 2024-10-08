﻿using Microsoft.Extensions.Options;
using OrderEntry.Database;

namespace OrderEntry.MindfulTrader
{
    public class MindfulTraderService(IOptions<MindfulTraderSettings> options) : IMindfulTraderService
    {
        private readonly IOptions<MindfulTraderSettings> options = options;

        public Task<MindfulTraderSession> GetSession()
        {
            return MindfulTraderSession.Create(options);
        }

        public async Task<List<T>> GetOrders<T>(ParseSetting parseSetting) where T : IOrder
        {
            using var session = await MindfulTraderSession.Create(options);
            var accountBalance = parseSetting.GetMindfulTraderAccountBalance();
            if (parseSetting.ParseType == ParseTypes.Watchlist)
            {
                return parseSetting.Mode switch
                {
                    Modes.Stock => [.. (await session.GetStockOrders(parseSetting.Key, parseSetting.Strategy, accountBalance)).Cast<T>()],
                    Modes.Option => [.. (await session.GetOptionOrders(parseSetting.Key, parseSetting.Strategy, accountBalance)).Cast<T>()],
                    Modes.LowPricedStock => [.. (await session.GetLowPricedStockOrders(parseSetting.Key, parseSetting.Strategy, accountBalance)).Cast<T>()],
                    Modes.None => [],
                    _ => [],
                };
            }
            else
                return [];
        }

        public async Task<List<IOrder>> GetOrders(IEnumerable<ParseSetting> parseSettings)
        {
            using var session = await MindfulTraderSession.Create(options);
            var orders = new List<IOrder>();
            foreach (var parseSetting in parseSettings)
            {
                if (parseSetting.ParseType != ParseTypes.Watchlist)
                    continue;
                var accountBalance = parseSetting.GetMindfulTraderAccountBalance();                    
                switch (parseSetting.Mode)
                {
                    case Modes.Stock:
                        orders.AddRange(await session.GetStockOrders(parseSetting.Key, parseSetting.Strategy, accountBalance));
                        break;
                    case Modes.Option:
                        orders.AddRange(await session.GetOptionOrders(parseSetting.Key, parseSetting.Strategy, accountBalance));
                        break;
                    case Modes.LowPricedStock:
                        orders.AddRange(await session.GetLowPricedStockOrders(parseSetting.Key, parseSetting.Strategy, accountBalance));
                        break;
                }
            }

            return orders;
        }
    }

    public interface IMindfulTraderService
    {
        Task<MindfulTraderSession> GetSession();
        
        Task<List<IOrder>> GetOrders(IEnumerable<ParseSetting> parseSettings);

        Task<List<T>> GetOrders<T>(ParseSetting parseSetting) where T : IOrder;
    }
}

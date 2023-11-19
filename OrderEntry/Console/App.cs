﻿using IBApi;
using OrderEntry.Brokers;
using OrderEntry.MindfulTrader;
using TextCopy;

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

        public async Task RunPrasadIBDoubleDownStocks()
        {
            const string OrdersFile = "IBPrasadOrders.txt";
            const double AccountBalance = 15000;

            var orders = parserService.ParseWatchlist(await File.ReadAllTextAsync(OrdersFile), Mode.Stocks, Strategies.DoubleDown, AccountBalance)
                    .Where(s => s.WatchDate == DateOnly.FromDateTime(DateTime.Now) && s.Count > 0)
                    .OrderBy(s => ((StockOrder)s).DistanceInATRs)
                    .ToList();

            await interactiveBrokersService.Display();
            
            orders = await interactiveBrokersService.GetOrdersWithoutPositions(orders);
            orders = TakeTop(orders, AccountBalance); 
            
            await SubmitOrders(orders, order => interactiveBrokersService.Submit((StockOrder) order));
        }

        public async Task RunPrasadIBDoubleDownOptions()
        {
            const string OrdersFile = "IBPrasadOrders.txt";
            const double AccountBalance = 15000;

            var orders = parserService.ParseWatchlist(await File.ReadAllTextAsync(OrdersFile), Mode.Options, Strategies.DoubleDown, AccountBalance)
                    .Where(s => s.WatchDate == DateOnly.FromDateTime(DateTime.Now) && s.Count > 0)                    
                    .ToList();

            await interactiveBrokersService.Display();

            var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            int count = 0;
            while (count < 12 && await periodicTimer.WaitForNextTickAsync())
            {
                count++;
                var ordersWithoutPositions = await interactiveBrokersService.GetOrdersWithoutPositions(orders);

            }
        }   

        public async Task RunPrasadCharlesSchwab()
        {
            const string OrdersFile = "IRAOrders.txt";
            const double AccountBalance = 50000;

            var orders = parserService.ParseWatchlist(await File.ReadAllTextAsync(OrdersFile), Mode.Options, Strategies.MainPullback, AccountBalance)
                    .Where(s => s.WatchDate == DateOnly.FromDateTime(DateTime.Now) && s.Count > 0)
                    .Cast<OptionOrder>()
                    .OrderBy(s => s.PositionValue)
                    .ToList();
            foreach (var order in orders)
                Console.WriteLine(order);
        }

        public async Task RunPrasadTDAmeritrade()
        {
            const string OrdersFile = "IRAOrders.txt";
            const double AccountBalance = 50000;

            var orders = parserService.ParseWatchlist(await File.ReadAllTextAsync(OrdersFile), Mode.Stocks, Strategies.MainPullback, AccountBalance)
                    .Where(s => s.WatchDate == DateOnly.FromDateTime(DateTime.Now) && s.Count > 0)
                    .Cast<StockOrder>()
                    .OrderBy(s => s.DistanceInATRs)
                    .ToList();

            foreach (var order in orders)
                Console.WriteLine(order);
        }

        private static async Task SubmitOrders(List<IOrder> orders, Func<IOrder, Task<bool>> submitFunc)
        {
            var count = 0;
            foreach (var order in orders)
            {
                char c = '0';
                while (c != 'A' && c != 'S' && c != 'C' && c != 'a' && c != 's' && c != 'c')
                {
                    Console.WriteLine(order);
                    Console.Write("Accept (A), Skip (S), Cancel (C): ");
                    var key = Console.ReadKey();
                    c = key.KeyChar;
                }
                if (c == 'c' || c == 'C') return;
                if (c == 's' || c == 'S') continue;
                if (c == 'a' || c == 'A')
                {
                    var result = await submitFunc(order);
                    if (result)
                    {
                        count++;
                        Console.WriteLine("Submitted successfully");
                    }
                    else
                        Console.WriteLine("Failed to submit");
                }
            }

            Console.WriteLine($"{count} Orders submitted");
        }

        private List<IOrder> TakeTop(List<IOrder> orders, double accountBalance)
        {
            var balance = 0.0;
            var topOrders = new List<IOrder>();
            foreach (var order in orders)
            {
                balance += order.PositionValue;
                if (balance > accountBalance)
                    break;
                topOrders.Add(order);
            }

            return topOrders;
        }
    }
}

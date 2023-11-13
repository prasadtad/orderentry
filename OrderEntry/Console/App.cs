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

        public async Task RunPrasadInteractiveBrokers()
        {
            const string OrdersFile = "IBPrasadOrders.txt";
            const string AccountId = "prasad";
            const double AccountBalance = 15000;

            var orders = (await parserService.ParseWatchlist(OrdersFile, Mode.Stocks, AccountBalance))
                    .Where(s => s.Strategy == Strategies.DoubleDown && s.Count > 0)
                    .Cast<StockOrder>()
                    .OrderBy(s => s.DistanceInATRs)
                    .ToList();

            await interactiveBrokersService.Display(AccountId);
            await SubmitOrders(orders, order => interactiveBrokersService.Submit(AccountId, order));            
        }

        public async Task RunPrasadCharlesSchwab()
        {
            const string OrdersFile = "IRAOrders.txt";
            const double AccountBalance = 50000;

            var orders = (await parserService.ParseWatchlist(OrdersFile, Mode.Options, AccountBalance))
                    .Where(s => s.Strategy == Strategies.MainPullback && s.Count > 0)
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

            var orders = (await parserService.ParseWatchlist(OrdersFile, Mode.Stocks, AccountBalance))
                    .Where(s => s.Count > 0)
                    .Cast<StockOrder>()
                    .OrderBy(s => s.DistanceInATRs)
                    .ToList();

            foreach (var order in orders)
                Console.WriteLine(order);
        }

        private static async Task SubmitOrders(IEnumerable<IOrder> orders, Func<IOrder, Task<bool>> submitFunc)
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
    }
}

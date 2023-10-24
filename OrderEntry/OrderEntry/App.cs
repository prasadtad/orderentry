using Microsoft.Extensions.Logging;
using OrderEntry.IB;
using OrderEntry.MindfulTrader;
namespace OrderEntry
{
    public class App
    {
        private readonly IBrokersService interactiveBrokersService;
        private readonly IParserService parserService;

        public App(IBrokersService interactiveBrokersService, IParserService parserService)
        {
            this.interactiveBrokersService = interactiveBrokersService;
            this.parserService = parserService;
        }

        public async Task Run()
        {
            Console.WriteLine($"Getting {interactiveBrokersService.AccountId} account details");
            var account = await interactiveBrokersService.GetAccountDetails();
            foreach (var key in account.Keys.OrderBy(k => k))
                Console.WriteLine($"{key}={account[key]}");

            Console.WriteLine("Parsing StockWatchlist.txt");
            var count = 0;
            foreach (var watchlistStock in (await parserService.ParseWatchlist("StockWatchlist.txt"))
                    .Where(s => s.Strategy == Strategies.DoubleDown)
                    .OrderBy(s => s.StockPositionValue))
            {
                char c = '0';
                while (c != 'A' && c != 'S' && c != 'C' && c != 'a' && c != 's' && c != 'c')
                {
                    Console.Write($"{watchlistStock} - Accept (A), Skip (S), Cancel (C): ");
                    c = (char)Console.Read();
                }
                if (c == 'c' || c == 'C') return;
                if (c == 's' || c == 'S') continue;
                if (c == 'a' || c == 'A')
                {
                    Console.WriteLine("Submitting order");
                    var result = await interactiveBrokersService.Submit(watchlistStock);
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


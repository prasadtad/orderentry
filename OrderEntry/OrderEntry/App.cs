using Microsoft.Extensions.Logging;
using OrderEntry.IB;
using OrderEntry.MindfulTrader;
namespace OrderEntry
{
    public class App
    {
        private readonly IBrokersService interactiveBrokersService;
        private readonly IParserService parserService;
        private readonly ILogger<App> logger;

        public App(IBrokersService interactiveBrokersService, IParserService parserService, ILogger<App> logger)
        {
            this.interactiveBrokersService = interactiveBrokersService;
            this.parserService = parserService;
            this.logger = logger;
        }

        public async Task Run()
        {
            if (!await interactiveBrokersService.IsAuthenticated()) return;
            logger.LogInformation("Authenticated");

            var account = await SelectTradingAccount();            
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
                    logger.LogInformation("Order to submit");
                }
            }

            logger.LogInformation("Orders submitted");
        }

        private async Task<(string accountId, string displayName)> SelectTradingAccount()
        {
            var accounts = await interactiveBrokersService.GetAccounts();
            int a;
            do
            {
                var i = 1;
                foreach (var account in accounts)
                {
                    Console.WriteLine($"{i}: {account.accountId} - {account.displayName}");
                    i++;
                }
                Console.Write("Select account: ");
                a = Console.Read();
            }
            while (a < 1 || a > accounts.Count);
            Console.WriteLine($"Account Selected: {accounts[a-1].accountId} - {accounts[a-1].displayName}{Environment.NewLine}");

            return accounts[a-1];
        }
    }
}


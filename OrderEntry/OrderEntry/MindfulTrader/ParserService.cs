using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace OrderEntry.MindfulTrader
{
    public class ParserService : IParserService
	{
		private readonly ILogger<ParserService> logger;
        private readonly IOptions<MindfulTraderSettings> options;

		public ParserService(ILogger<ParserService> logger, IOptions<MindfulTraderSettings> options)
		{
			this.logger = logger;
            this.options = options;
		}

        public async Task<List<IOrder>> GetWatchlist(TradeSettings tradeSettings, string? screenshotPath = null)
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 2560, Height = 4096 }
            });
            if (File.Exists("Content/mindfultradercookies.json"))
            {
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(await File.ReadAllTextAsync("Content/mindfultradercookies.json"));
                await context.AddCookiesAsync(cookies!);
            }

            var page = await context.NewPageAsync();

            var watchlistUrl = "https://www.mindfultrader.com/watch_list_v2.html";
            await page.GotoAsync(watchlistUrl);
            if (page.Url != watchlistUrl)
            {
                await page.Locator("[name='username']").FillAsync(options.Value.Username);
                await page.Locator("[name='password']").FillAsync(options.Value.Password);
                await page.Locator("[name='submit']").ClickAsync();                
                await page.GotoAsync(watchlistUrl);
                await File.WriteAllTextAsync("Content/mindfultradercookies.json", JsonSerializer.Serialize(context.CookiesAsync()));
            }

            if (page.Url != watchlistUrl)
                throw new Exception("Login Failed");

            await page.Locator("#account_balance").FillAsync(tradeSettings.AccountBalance.ToString());
            await page.Locator("#buying_powerW").SetCheckedAsync(false);
            await page.Locator("#filter_strategy").SelectOptionAsync(new[] { tradeSettings.Strategy == Strategies.MainPullback ? "Main Pullback" : tradeSettings.Strategy == Strategies.DoubleDown ? "Double Down" : throw new NotImplementedException() });

            var viewMoreClass = tradeSettings.Mode == Mode.Stocks ? "load_more_1" :
                                tradeSettings.Mode == Mode.Options ? "load_more_2" :
                                tradeSettings.Mode == Mode.LowPricedStocks ? "load_more_3" :
                                throw new NotImplementedException();
            await page.Locator($"[class='{viewMoreClass}']").ClickAsync();

            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {                
                await page.ScreenshotAsync(new() { Path = screenshotPath });
            }

            var list = new List<IOrder>();
            switch (tradeSettings.Mode)
            {
                case Mode.Stocks:
                    foreach (var row in await page.Locator("#favorite_stocks")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadStockOrder(rowText.ReplaceLineEndings(string.Empty), false);
                        if (order != null) list.Add(order);
                    }
                    break;
                case Mode.Options:
                    foreach (var row in await page.Locator("#options")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadOptionOrder(rowText.ReplaceLineEndings(string.Empty));
                        if (order != null) list.Add(order);
                    }
                    break;
                case Mode.LowPricedStocks:
                    foreach (var row in await page.Locator("#low_price_stocks")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadStockOrder(rowText.ReplaceLineEndings(string.Empty), true);
                        if (order != null) list.Add(order);
                    }
                    break;
            }

            return list;
        }

		public List<IOrder> ParseWatchlist(string watchlistText, TradeSettings tradeSettings)
		{
            var readOrders = false;

            bool? readStrategy = null;
            bool? readAccountBalance = null;

			var list = new List<IOrder>();
            using (var sr = new StringReader(watchlistText))
            {                
                while (true)
                {
                    var line = sr.ReadLine()?.Trim();
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line == "Enter Account Balance $")
                    {
                        readAccountBalance = true;
                        continue;
                    }
                    if (readAccountBalance != null && readAccountBalance.Value)
                    {
                        if (double.Parse(line) != tradeSettings.AccountBalance)
                            throw new Exception($"Acccount balance {line} doesn't match expected {tradeSettings.AccountBalance}");
                        readAccountBalance = false;
                        continue;
                    }

                    if (line == "Strategy: ")
                    {
                        readStrategy = true;
                        continue;
                    }
                    if (readStrategy != null && readStrategy.Value)
                    {
                        if (line != tradeSettings.Strategy.ToString())
                            throw new Exception($"Strategy {line} doesn't match expected {tradeSettings.Strategy}");
                        readStrategy = false;
                        continue;
                    }

                    if (line == tradeSettings.Mode.ToString())
                    {
                        readOrders = true;
                        continue;
                    }
                    if (line.Equals("view more", StringComparison.OrdinalIgnoreCase))
                    {
                        if (readOrders)
                            throw new Exception("Detected 'View More', click to expand list.");
                        else
                            continue;
                    }
                    if (line.Equals("view less", StringComparison.OrdinalIgnoreCase))
                    {
                        readOrders = false;
                        continue;
                    }

                    if (!readOrders || line.StartsWith("Watch Date")) continue;

                    var order = tradeSettings.Mode == Mode.Options ? ReadOptionOrder(line) :
                                tradeSettings.Mode == Mode.Stocks ? ReadStockOrder(line, false) :
                                tradeSettings.Mode == Mode.LowPricedStocks ? ReadStockOrder(line, true) :
                                throw new NotImplementedException($"Unsupported mode {tradeSettings.Mode}");
                    if (order != null && order.Strategy == tradeSettings.Strategy)
                    {
                        list.Add(order);
                    }
                }
            }

            if (readAccountBalance == null) throw new Exception("Didn't find the account balance");

			return list;
		}

        private IOrder? ReadOptionOrder(string line)
        {
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new OptionOrder
                {
                    Id = Guid.NewGuid(),
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Unsupported strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    StrikeDate = DateOnly.ParseExact($"{tokens[4]}/{tokens[5]}/{tokens[6]}", "MMM/dd/yyyy"),
                    StrikePrice = double.TryParse(tokens[7].TrimStart('$'), out var strikePrice) ? strikePrice : 0,
                    Type = tokens[8] == "Call" ? OptionType.Call : throw new NotImplementedException($"Unsupported option type {tokens[8]}"),
                    Count = int.TryParse(tokens[9], out var count) ? count : 0,
                    PotentialEntry = double.Parse(tokens[10]),
                    PotentialProfit = double.Parse(tokens[11]),
                    PotentialStop = double.Parse(tokens[12]),
                    PositionValue = double.TryParse(tokens[13].TrimStart('$'), out var positionValue) ? positionValue : 0,
                    EarningsDate = tokens[14],
                    DividendsDate = tokens[15],
                    EntryOrderId = -1,
                    ProfitOrderId = -1,
                    StopOrderId = -1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse option order {line} from watchlist", line);
                return null;
            }
        }

        private IOrder? ReadStockOrder(string line, bool lowPriced)
		{
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new StockOrder
                {
                    Id = Guid.NewGuid(),
                    LowPriced = lowPriced,
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Invalid strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    Count = int.TryParse(tokens[4], out var count) ? count : 0,
                    PotentialEntry = double.Parse(tokens[5]),
                    PotentialProfit = double.Parse(tokens[6]),
                    PotentialStop = double.Parse(tokens[7]),
                    CurrentPrice = double.Parse(tokens[8]),
                    DistanceInATRs = double.Parse(tokens[9]),
                    PositionValue = double.TryParse(tokens[10].TrimStart('$'), out var positionValue) ? positionValue : 0,
                    EarningsDate = tokens[11],
                    DividendsDate = tokens[12],
                    EntryOrderId = -1,
                    ProfitOrderId = -1,
                    StopOrderId = -1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse stock order {line} from watchlist", line);
				return null;
            }
        }
	}

	public interface IParserService
	{
        Task<List<IOrder>> GetWatchlist(TradeSettings tradeSettings, string? screenshotPath = null);

        List<IOrder> ParseWatchlist(string watchlist, TradeSettings tradeSettings);
    }

    public class TradeSettings
    {
        public readonly Guid Id = Guid.NewGuid();

        public required double AccountBalance { get; set; }

        public required Strategies Strategy { get; set; }

        public required Mode Mode { get; set; }

        public override string ToString()
        {
            return $"{Mode} {Strategy} {AccountBalance}";
        }
    }

    public class MindfulTraderSettings
    {
        public required string Username { get; set; }

        public required string Password { get; set; }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using OrderEntry.Database;

namespace OrderEntry.MindfulTrader
{
    public class MindfulTraderService : IMindfulTraderService
    {
        private readonly ILogger<MindfulTraderService> logger;
        private readonly IOptions<MindfulTraderSettings> options;

        public MindfulTraderService(ILogger<MindfulTraderService> logger, IOptions<MindfulTraderSettings> options)
        {
            this.logger = logger;
            this.options = options;
        }

        public DateOnly CurrentWatchDate
        {
            get
            {
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")));
            }
        }

        public async Task<List<IOrder>> GetWatchlist(ParseSetting parseSetting, string? screenshotPath = null)
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

            var url = GetUrl(parseSetting);
            await page.GotoAsync(url);
            if (page.Url != url)
            {
                await page.Locator("[name='username']").FillAsync(options.Value.Username);
                await page.Locator("[name='password']").FillAsync(options.Value.Password);
                await page.Locator("[name='submit']").ClickAsync();
                await page.GotoAsync(url);
                await File.WriteAllTextAsync("Content/mindfultradercookies.json", JsonSerializer.Serialize(context.CookiesAsync()));
            }

            if (page.Url != url)
                throw new Exception("Login Failed");

            await page.Locator("#account_balance").FillAsync(parseSetting.AccountBalance.ToString());
            await page.Locator("#buying_powerW").SetCheckedAsync(false);
            await page.Locator("#filter_strategy").SelectOptionAsync(new[] { parseSetting.Strategy == Strategies.MainPullback ? "Main Pullback" : parseSetting.Strategy == Strategies.DoubleDown ? "Double Down" : throw new NotImplementedException() });

            var viewMoreClass = parseSetting.Mode == Modes.Stock ? "load_more_1" :
                                parseSetting.Mode == Modes.Option ? "load_more_2" :
                                parseSetting.Mode == Modes.LowPricedStock ? "load_more_3" :
                                throw new NotImplementedException();
            await page.Locator($"[class='{viewMoreClass}']").ClickAsync();

            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                await page.ScreenshotAsync(new() { Path = screenshotPath });
            }

            var list = new List<IOrder>();
            switch (parseSetting.Mode)
            {
                case Modes.Stock:
                    foreach (var row in await page.Locator("#favorite_stocks")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadStockOrder(parseSetting, rowText.ReplaceLineEndings(string.Empty), false);
                        if (order != null) list.Add(order);
                    }
                    break;
                case Modes.Option:
                    foreach (var row in await page.Locator("#options")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadOptionOrder(parseSetting, rowText.ReplaceLineEndings(string.Empty));
                        if (order != null) list.Add(order);
                    }
                    break;
                case Modes.LowPricedStock:
                    foreach (var row in await page.Locator("#low_price_stocks")
                                     .Locator("tr").AllAsync())
                    {
                        if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                            continue;
                        var rowText = await row.InnerTextAsync();
                        if (rowText.Contains("Watch Date")) continue;
                        var order = ReadStockOrder(parseSetting, rowText.ReplaceLineEndings(string.Empty), true);
                        if (order != null) list.Add(order);
                    }
                    break;
            }

            return list;
        }

        public List<IOrder> ParseWatchlist(string watchlistText, ParseSetting parseSetting)
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
                        if (decimal.Parse(line) != parseSetting.AccountBalance)
                            throw new Exception($"Acccount balance {line} doesn't match expected {parseSetting.AccountBalance}");
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
                        if (line != parseSetting.Strategy.ToString())
                            throw new Exception($"Strategy {line} doesn't match expected {parseSetting.Strategy}");
                        readStrategy = false;
                        continue;
                    }

                    if (line == parseSetting.Mode.ToString())
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

                    var order = parseSetting.Mode == Modes.Option ? ReadOptionOrder(parseSetting, line) :
                                parseSetting.Mode == Modes.Stock ? ReadStockOrder(parseSetting, line, false) :
                                parseSetting.Mode == Modes.LowPricedStock ? ReadStockOrder(parseSetting, line, true) :
                                throw new NotImplementedException($"Unsupported mode {parseSetting.Mode}");
                    if (order != null && order.Strategy == parseSetting.Strategy)
                    {
                        list.Add(order);
                    }
                }
            }

            if (readAccountBalance == null) throw new Exception("Didn't find the account balance");

            return list;
        }

        private IOrder? ReadOptionOrder(ParseSetting parseSetting, string line)
        {
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new OptionOrder
                {
                    Id = Guid.Empty,
                    ParseSettingKey = parseSetting.Key,
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Unsupported strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    StrikeDate = DateOnly.ParseExact($"{tokens[4]}/{tokens[5]}/{tokens[6]}", "MMM/d/yyyy"),
                    StrikePrice = decimal.TryParse(tokens[7].TrimStart('$'), out var strikePrice) ? strikePrice : 0,
                    Type = tokens[8] == "Call" ? OptionTypes.Call : throw new NotImplementedException($"Unsupported option type {tokens[8]}"),
                    Count = int.TryParse(tokens[9], out var count) ? count : 0,
                    PotentialEntry = decimal.Parse(tokens[10]),
                    PotentialProfit = decimal.Parse(tokens[11]),
                    PotentialStop = decimal.Parse(tokens[12]),
                    PositionValue = decimal.TryParse(tokens[13].TrimStart('$'), out var positionValue) ? positionValue : 0
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse option order {line} from watchlist", line);
                return null;
            }
        }

        private IOrder? ReadStockOrder(ParseSetting parseSetting, string line, bool lowPriced)
        {
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new StockOrder
                {
                    Id = Guid.Empty,
                    ParseSettingKey = parseSetting.Key,
                    LowPriced = lowPriced,
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Invalid strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    Count = int.TryParse(tokens[4], out var count) ? count : 0,
                    PotentialEntry = decimal.Parse(tokens[5]),
                    PotentialProfit = decimal.Parse(tokens[6]),
                    PotentialStop = decimal.Parse(tokens[7]),
                    CurrentPrice = decimal.Parse(tokens[8]),
                    DistanceInATRs = decimal.Parse(tokens[9]),
                    PositionValue = decimal.TryParse(tokens[10].TrimStart('$'), out var positionValue) ? positionValue : 0
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse stock order {line} from watchlist", line);
                return null;
            }
        }

        private static string GetUrl(ParseSetting parseSetting)
        {
            switch (parseSetting.ParseType)
            {
                case ParseTypes.Live:
                    return "https://www.mindfultrader.com/live_positions.html";
                case ParseTypes.Watchlist:
                    return "https://www.mindfultrader.com/watch_list_v2.html";
                case ParseTypes.Options:
                    return "https://www.mindfultrader.com/extra_options_account.html";
                case ParseTypes.DoubleDown:
                    return "https://www.mindfultrader.com/double_down_account.html";
                case ParseTypes.TriggeredList:
                    return "https://www.mindfultrader.com/triggered_list.html";
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public interface IMindfulTraderService
    {
        DateOnly CurrentWatchDate { get; }

        Task<List<IOrder>> GetWatchlist(ParseSetting tradeSettings, string? screenshotPath = null);

        List<IOrder> ParseWatchlist(string watchlist, ParseSetting tradeSettings);
    }
}

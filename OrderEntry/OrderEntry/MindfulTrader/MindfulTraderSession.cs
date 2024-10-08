using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace OrderEntry.MindfulTrader
{
    public sealed class MindfulTraderSession(IOptions<MindfulTraderSettings> options, IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page) : IDisposable, IAsyncDisposable
    {
        private static readonly char[] separator = [' ', '\t'];

        private const string CookieFileName = "mindfultradercookies.json";

        private readonly IOptions<MindfulTraderSettings> options = options;
        private readonly IPlaywright playwright = playwright;
        private readonly IBrowser browser = browser;
        private readonly IBrowserContext context = context;
        private readonly IPage page = page;

        private bool disposed, asyncDisposed;

        public static async Task<MindfulTraderSession> Create(IOptions<MindfulTraderSettings> options)
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 2560, Height = 4096 }
            });
            if (File.Exists(Path.Combine(options.Value.DataPath, CookieFileName)))
            {
                var cookieJson = await File.ReadAllTextAsync(Path.Combine(options.Value.DataPath, CookieFileName));
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(cookieJson);
                await context.AddCookiesAsync(cookies!);
            }
            var page = await context.NewPageAsync();
            return new MindfulTraderSession(options, playwright, browser, context, page);
        }

        public async Task<byte[]> Screenshot(string screenshotPath)
        {
            return await page.ScreenshotAsync(new() { Path = screenshotPath });
        }

        public async Task<List<StockOrder>> GetStockOrders(string parseSettingKey, Strategies strategy, decimal accountBalance)
        {
            try
            {
                await GotoPage(ParseTypes.Watchlist);

                await page.Locator("#account_balance").FillAsync(accountBalance.ToString());
                await page.Locator("#buying_powerW").SetCheckedAsync(false);
                await page.Locator("#filter_strategy").SelectOptionAsync(new[] { strategy == Strategies.MainPullback ? "Main Pullback" : strategy == Strategies.DoubleDown ? "Double Down" : throw new NotImplementedException() });

                var viewMoreClass = "load_more_1";
                await page.Locator($"[class='{viewMoreClass}']").ClickAsync();

                var list = new List<StockOrder>();
                foreach (var row in await page.Locator("#favorite_stocks").Locator("tr").AllAsync())
                {
                    if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                        continue;
                    var rowText = await row.InnerTextAsync();
                    if (rowText.Contains("Watch Date")) continue;
                    var order = ReadStockOrder(parseSettingKey, rowText.ReplaceLineEndings(string.Empty), false);
                    if (order != null) list.Add(order);
                }

                return list;
            }
            catch
            {
                await CaptureContent("mindfultradererror");
                throw;
            }
        }

        public async Task<List<OptionOrder>> GetOptionOrders(string parseSettingKey, Strategies strategy, decimal accountBalance)
        {
            try
            {
                await GotoPage(ParseTypes.Watchlist);

                await page.Locator("#account_balance").FillAsync(accountBalance.ToString());
                await page.Locator("#buying_powerW").SetCheckedAsync(false);
                await page.Locator("#filter_strategy").SelectOptionAsync(new[] { strategy == Strategies.MainPullback ? "Main Pullback" : strategy == Strategies.DoubleDown ? "Double Down" : throw new NotImplementedException() });

                var viewMoreClass = "load_more_2";
                await page.Locator($"[class='{viewMoreClass}']").ClickAsync();

                var list = new List<OptionOrder>();

                foreach (var row in await page.Locator("#options").Locator("tr").AllAsync())
                {
                    if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                        continue;
                    var rowText = await row.InnerTextAsync();
                    if (rowText.Contains("Watch Date")) continue;
                    var order = ReadOptionOrder(parseSettingKey, rowText.ReplaceLineEndings(string.Empty));
                    if (order != null) list.Add(order);
                }

                return list;
            }
            catch
            {
                await CaptureContent("mindfultradererror");
                throw;
            }
        }

        public async Task<List<StockOrder>> GetLowPricedStockOrders(string parseSettingKey, Strategies strategy, decimal accountBalance)
        {
            try
            {
                await GotoPage(ParseTypes.Watchlist);

                await page.Locator("#account_balance").FillAsync(accountBalance.ToString());
                await page.Locator("#buying_powerW").SetCheckedAsync(false);
                await page.Locator("#filter_strategy").SelectOptionAsync(new[] { strategy == Strategies.MainPullback ? "Main Pullback" : strategy == Strategies.DoubleDown ? "Double Down" : throw new NotImplementedException() });

                var viewMoreClass = "load_more_3";
                await page.Locator($"[class='{viewMoreClass}']").ClickAsync();

                var list = new List<StockOrder>();

                foreach (var row in await page.Locator("#low_price_stocks").Locator("tr").AllAsync())
                {
                    if (((await row.GetAttributeAsync("class")) ?? string.Empty).Contains("filtered-out"))
                        continue;
                    var rowText = await row.InnerTextAsync();
                    if (rowText.Contains("Watch Date")) continue;
                    var order = ReadStockOrder(parseSettingKey, rowText.ReplaceLineEndings(string.Empty), true);
                    if (order != null) list.Add(order);
                }

                return list;
            }
            catch
            {
                await CaptureContent("mindfultradererror");
                throw;
            }
        }

        private async Task GotoPage(ParseTypes parseTypes)
        {
            var url = GetUrl(parseTypes);
            await page.GotoAsync(url);

            if (page.Url != url)
            {
                await page.Locator("[name='username']").FillAsync(options.Value.Username);
                await page.Locator("[name='password']").FillAsync(options.Value.Password);
                await page.Locator("[name='submit']").ClickAsync();
                await page.GotoAsync(url);
                await File.WriteAllTextAsync(Path.Combine(options.Value.DataPath, CookieFileName), JsonSerializer.Serialize(await context.CookiesAsync()));
            }

            if (page.Url != url)
                throw new Exception("Login Failed");
        }

        private static OptionOrder ReadOptionOrder(string parseSettingKey, string line)
        {
            try
            {
                var tokens = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                return new OptionOrder
                {
                    Id = Guid.Empty,
                    ParseSettingKey = parseSettingKey,
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
                throw new Exception($"Unable to parse option order {line}", ex);
            }
        }

        private static StockOrder ReadStockOrder(string parseSettingKey, string line, bool lowPriced)
        {
            try
            {
                var tokens = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                return new StockOrder
                {
                    Id = Guid.Empty,
                    ParseSettingKey = parseSettingKey,
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
                throw new Exception($"Unable to parse stock order {line}", ex);
            }
        }

        private static string GetUrl(ParseTypes parseTypes)
        {
            return parseTypes switch
            {
                ParseTypes.Live => "https://www.mindfultrader.com/live_positions.html",
                ParseTypes.Watchlist => "https://www.mindfultrader.com/watch_list_v2.html",
                ParseTypes.Options => "https://www.mindfultrader.com/extra_options_account.html",
                ParseTypes.DoubleDown => "https://www.mindfultrader.com/double_down_account.html",
                ParseTypes.TriggeredList => "https://www.mindfultrader.com/triggered_list.html",
                _ => throw new NotImplementedException()
            };
        }

        private async Task CaptureContent(string filenameWithoutExtension)
        {
            await File.WriteAllTextAsync(Path.Combine(options.Value.DataPath, $"{filenameWithoutExtension}.html"), await page.InnerHTMLAsync("html"));
            await Screenshot(Path.Combine(options.Value.DataPath, $"{filenameWithoutExtension}.jpg"));
        }

        public void Dispose()
        {
            if (disposed) return;

            playwright.Dispose();
            disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (asyncDisposed) return;

            await context.DisposeAsync();
            await browser.DisposeAsync();
            asyncDisposed = true;
        }
    }
}
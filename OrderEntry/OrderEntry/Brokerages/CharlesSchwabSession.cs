using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokerages
{
    public sealed class CharlesSchwabSession(ILogger logger, IOptions<CharlesSchwabSettings> options, IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page) : IDisposable, IAsyncDisposable
    {
        private const string LoginUrl = "https://client.schwab.com/Login/SignOn/CustomerCenterLogin.aspx";
        private const string AuthenticatorUrl = "https://sws-gateway-nr.schwab.com/ui/host/#/authenticators";
        private const string ApprovalUrl = "https://sws-gateway-nr.schwab.com/ui/host/#/mobile_approve";
        private const string RememberUrl = "https://sws-gateway-nr.schwab.com/ui/host/#/devicetag/remember";
        private const string StockOrderUrl = "https://client.schwab.com/app/trade/tom/#/trade";
        private const string PositionsUrl = "https://client.schwab.com/app/accounts/positions/#/";

        private readonly ILogger logger = logger;
        private readonly IOptions<CharlesSchwabSettings> options = options;
        private readonly IPlaywright playwright = playwright;
        private readonly IBrowser browser = browser;
        private readonly IBrowserContext context = context;
        private readonly IPage page = page;

        private bool disposed, asyncDisposed;

        private static readonly string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:{0}) Gecko/20100101 Firefox/{0}";

        public static async Task<CharlesSchwabSession> Create(ILogger logger, IOptions<CharlesSchwabSettings> options)
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Firefox.LaunchAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = string.Format(UserAgent, browser.Version),
                ViewportSize = new ViewportSize { Width = 1920, Height = 2160 }
            });
            if (File.Exists(options.Value.CookieFilePath))
            {
                var cookieJson = await File.ReadAllTextAsync(options.Value.CookieFilePath);
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(cookieJson);
                await context.AddCookiesAsync(cookies!);
            }
            var page = await context.NewPageAsync();
            return new CharlesSchwabSession(logger, options, playwright, browser, context, page);
        }

        public async Task<byte[]> Screenshot(string screenshotPath)
        {
            return await page.ScreenshotAsync(new() { Path = screenshotPath });
        }

        public async Task<List<StockPosition>> GetStockPositions(Func<string, bool> isActivelyTrade)
        {
            await GotoPage(PositionsUrl);

            await page.Locator("#quantity-tableHeader-0").WaitForAsync();
            await page.Locator("#costPerShare-tableHeader-1").WaitForAsync();

            var positions = new List<StockPosition>();

            foreach (var row in await page.Locator("#holdingsAccount_31716198").Locator("tr").AllAsync())
            {
                var ticker = await row.GetAttributeAsync("data-symbol");
                if (string.IsNullOrWhiteSpace(ticker)) continue;

                string? firstRowValue = null, secondRowValue = null;
                foreach (var rowCell in await row.Locator("app-dynamic-column").Locator("span").AllAsync())
                {
                    var rowValue = await rowCell.InnerTextAsync();
                    if (string.IsNullOrWhiteSpace(rowValue)) continue;
                    if (firstRowValue == null)
                    {
                        firstRowValue = rowValue.Trim();
                        continue;
                    }
                    if (secondRowValue == null)
                    {
                        secondRowValue = rowValue.Trim();
                        if (secondRowValue.StartsWith("$"))
                            secondRowValue = secondRowValue[1..];
                        continue;
                    }
                }
                if (firstRowValue == null || secondRowValue == null)
                    throw new Exception($"Unable to determine quantity or position for {ticker}");
                positions.Add(new StockPosition
                {
                    Broker = Brokers.CharlesSchwab,
                    AccountId = "31716198SCHW",
                    Ticker = ticker,
                    Count = decimal.Parse(firstRowValue),
                    AverageCost = decimal.Parse(secondRowValue),
                    ActivelyTrade = isActivelyTrade(ticker)
                });
            }

            return positions;
        }

        public async Task<bool> FillOrder(StockOrder order)
        {
            try
            {
                await GotoPage(StockOrderUrl);

                await page.Locator("#aiott_add_conditional_button").ClickAsync();
                await page.Locator("#mcaio-conditionalDropDown").SelectOptionAsync("conditional_triggerOCO");

                var ticket0OrderId = await OpenTicket(0);
                await EnterTicker(ticket0OrderId, order.Ticker);
                await EnterQuantity(ticket0OrderId, order.Count);
                await EnterDetails(ticket0OrderId, order.PotentialEntry, false, false);

                var ticket1OrderId = await OpenTicket(1);
                await EnterTicker(ticket1OrderId, order.Ticker);
                await EnterQuantity(ticket1OrderId, -order.Count);
                await EnterDetails(ticket1OrderId, order.PotentialProfit, false, true);

                var ticket2OrderId = await OpenTicket(2);
                await EnterTicker(ticket2OrderId, order.Ticker);
                await EnterQuantity(ticket2OrderId, -order.Count);
                await EnterDetails(ticket2OrderId, order.PotentialStop, true, true);

                await page.GetByText("Review Order").ClickAsync();
                await page.Locator("#mtt-place-button").ClickAsync();

                await Task.Delay(1000);
                await page.EvaluateAsync($"{ticket2OrderId}.handlePlaceAnother()");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Couldn't submit order");
                return false;
            }
        }

        private async Task<string> OpenTicket(int ticketId)
        {
            var orderLocator = page.Locator($"[name='mcaio-Order{ticketId}']");
            var orderId = await orderLocator.GetAttributeAsync("id");
            var toggleSpan = orderLocator.Locator("#aiott_toggleticket");
            var toggleClass = await toggleSpan.GetAttributeAsync("class");
            if (toggleClass != null && toggleClass.Contains("sch-chevron-down"))
                await page.EvaluateAsync($"{orderId}.toggleTicket()");
            return orderId!;
        }

        private async Task EnterTicker(string orderId, string ticker)
        {
            var ticketSymLocator = page.Locator($"mc-trade-sym-look[parent-id='{orderId}']");
            var tradeSymbolId = await ticketSymLocator.Locator("mc-trade-symbol").GetAttributeAsync("id");

            var tradeSymInput = ticketSymLocator.Locator("#_txtSymbol");
            await tradeSymInput.FocusAsync();
            await tradeSymInput.PressSequentiallyAsync(ticker, new() { Delay = 50 });

            await page.EvaluateAsync($"{tradeSymbolId}.setSymbol('{ticker}')");
        }

        private async Task EnterQuantity(string orderId, int quantity)
        {
            var orderControlLocator = page.Locator($"mc-trade-order-control[parent-id='{orderId}']");
            await orderControlLocator.Locator("#_action").SelectOptionAsync(new SelectOptionValue { Label = quantity > 0 ? "Buy" : "Sell" });
            quantity = Math.Abs(quantity);

            await orderControlLocator.Locator("#_txtQty").Locator($"input").FillAsync(quantity.ToString());
        }

        private async Task EnterDetails(string orderId, decimal price, bool stopLoss, bool gtc)
        {
            var orderControlLocator = page.Locator($"mc-trade-order-control[parent-id='{orderId}']");
            var type = stopLoss ? "Stop market" : "Limit";
            await orderControlLocator.Locator("#mcaio-orderType-container").Locator("select").SelectOptionAsync(new SelectOptionValue { Label = type });
            await orderControlLocator.Locator(stopLoss ? "#_txtStopPrice" : "#_txtLimitPrice").Locator("input").FillAsync(price.ToString());
            var timingLocator = orderControlLocator.Locator("#_timing");
            if (await timingLocator.IsEnabledAsync())
                await timingLocator.SelectOptionAsync(new SelectOptionValue { Label = gtc ? "GTC (Good till canceled)" : "Day" });
        }

        private async Task GotoPage(string url)
        {
            logger.LogInformation("Going to {page}", url);
            await page.GotoAsync(url);
            if (page.Url == url) return;

            logger.LogInformation("On {page}", page.Url);
            if (page.Url.StartsWith(LoginUrl))
            {
                var frame = page.FrameLocator("#lmsSecondaryLogin");
                await frame.Locator("#loginIdInput").FillAsync(options.Value.Username);
                await frame.GetByPlaceholder("Password").FillAsync(options.Value.Password);
                await frame.GetByRole(AriaRole.Checkbox, new() { Name = "Remember Login ID" }).SetCheckedAsync(true);
                await frame.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
                await WaitUntilNavigatedAwayFrom(LoginUrl);
            }

            logger.LogInformation("On {page}", page.Url);
            if (page.Url.StartsWith(AuthenticatorUrl))
            {
                await page.Locator("#mobile_approve").ClickAsync();
                await WaitUntilNavigatedAwayFrom(AuthenticatorUrl);
            }

            logger.LogInformation("On {page}", page.Url);
            if (page.Url.StartsWith(ApprovalUrl))
            {
                await WaitUntilNavigatedAwayFrom(ApprovalUrl);
            }

            logger.LogInformation("On {page}", page.Url);
            if (page.Url.StartsWith(RememberUrl))
            {
                await page.Locator("#remember-device-yes").ClickAsync();
                await page.Locator("#btnContinue").ClickAsync();
                await WaitUntilNavigatedAwayFrom(RememberUrl);
            }

            logger.LogInformation("On {page}", page.Url);
            if (page.Url != url)
            {
                await File.WriteAllTextAsync("Content/error.html", await page.Locator("html").InnerHTMLAsync());
                await Screenshot("Content/error.jpg");
                throw new Exception($"Login failed, stuck at {page.Url}");
            }
            await File.WriteAllTextAsync(options.Value.CookieFilePath, JsonSerializer.Serialize(await context.CookiesAsync()));
        }

        private async Task WaitUntilNavigatedAwayFrom(string url)
        {
            while (page.Url.StartsWith(url))
            {
                logger.LogInformation("Still on {page}", page.Url);
                await Task.Delay(10000);
            }
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
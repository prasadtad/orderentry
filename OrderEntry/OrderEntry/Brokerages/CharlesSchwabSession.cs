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

        public async Task<bool> FillOrder(StockOrder order, bool clickSubmit)
        {
            var random = new Random();
            await GotoPage(StockOrderUrl);

            await page.Locator("#aiott_add_conditional_button").ClickAsync();            
            await page.Locator("#mcaio-conditionalDropDown").SelectOptionAsync("conditional_triggerOCO");
            
            async Task EnterTicket(int ticketId)
            {
                var orderLocator = page.Locator($"[name='mcaio-Order{ticketId}']");
                var orderId = await orderLocator.GetAttributeAsync("id");
                var toggleSpan = orderLocator.Locator("#aiott_toggleticket");
                var toggleClass = await toggleSpan.GetAttributeAsync("class");
                if (toggleClass != null && toggleClass.Contains("sch-chevron-down"))                                    
                    await page.EvaluateAsync($"{orderId}.toggleTicket()");
                var tradeSymLookup = page.Locator($"mc-trade-sym-look[parent-id='{orderId}']");
                var tradeSymbolId = await tradeSymLookup.Locator("mc-trade-symbol").GetAttributeAsync("id");
                var tradeSymInput = tradeSymLookup.Locator("#_txtSymbol");
                await tradeSymInput.FocusAsync();
                await tradeSymInput.PressSequentiallyAsync(order.Ticker, new() { Delay = random.Next(50, 100)});
                await page.EvaluateAsync($"{tradeSymbolId}.setSymbol('{order.Ticker}')");
                await File.WriteAllTextAsync($"Content/order{ticketId}.html", await page.Locator("html").InnerHTMLAsync());
                await Screenshot($"Content/order{ticketId}.jpg");
            }
            
            await EnterTicket(0);
            await EnterTicket(1);
            await EnterTicket(2);

            if (!clickSubmit)
            {
                await File.WriteAllTextAsync("Content/order.html", await page.Locator("html").InnerHTMLAsync());
                await Screenshot("Content/order.jpg");
                return false;
            }

            return false;
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
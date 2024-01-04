using System.Text.Json;
using AutoFinance.Broker.InteractiveBrokers.EventArgs;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokerages
{
    public sealed class CharlesSchwabSession(IOptions<CharlesSchwabSettings> options, IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page) : IDisposable, IAsyncDisposable
    {
        private const string Login1Url = "https://client.schwab.com/Login/SignOn/CustomerCenterLogin.aspx";
        private const string Login2Url = "https://www.schwab.com/client-home";
        private const string StockOrderUrl = "https://client.schwab.com/app/trade/tom/#/trade";

        private readonly IOptions<CharlesSchwabSettings> options = options;
        private readonly IPlaywright playwright = playwright;
        private readonly IBrowser browser = browser;
        private readonly IBrowserContext context = context;
        private readonly IPage page = page;

        private bool disposed, asyncDisposed;

        public static async Task<CharlesSchwabSession> Create(IOptions<CharlesSchwabSettings> options)
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });
            if (File.Exists(options.Value.CookieFilePath))
            {
                var cookieJson = await File.ReadAllTextAsync(options.Value.CookieFilePath);
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(cookieJson);
                await context.AddCookiesAsync(cookies!);
            }
            var page = await context.NewPageAsync();
            return new CharlesSchwabSession(options, playwright, browser, context, page);
        }

        public async Task<byte[]> Screenshot(string screenshotPath)
        {
            return await page.ScreenshotAsync(new() { Path = screenshotPath });
        }

        public async Task<bool> SubmitOrder(StockOrder order)
        {
            await GotoPage(StockOrderUrl);
            
            await page.Locator("#aiott_add_conditional_button").ClickAsync();
            await page.Locator("#mcaio-conditionalDropDown").SelectOptionAsync("#mcaio-conditional_triggerOCO");
            var order1Group = page.Locator("#aiott_toggleticket");
            if (((await order1Group.GetAttributeAsync("class")) ?? string.Empty).Contains("sch-chevron-down"))
            {
                await order1Group.ClickAsync();
            }
            return true;
        }

        private async Task GotoPage(string url)
        {
            await page.GotoAsync(url);

            if (page.Url != url)
            {
                if (page.Url.StartsWith(Login1Url) || page.Url.StartsWith(Login2Url))
                {
                    await page.Locator("#loginIdInput").FillAsync(options.Value.Username);
                    await page.Locator("#passwordInput").FillAsync(options.Value.Password);                    
                }
                if (page.Url.StartsWith(Login1Url))
                    await page.Locator("#remember-me-checkbox-stack").SetCheckedAsync(true);
                if (page.Url.StartsWith(Login2Url))
                    await page.Locator("#remember-me-checkbox-slim").SetCheckedAsync(true);
                if (page.Url.StartsWith(Login1Url) || page.Url.StartsWith(Login2Url))                    
                    await page.Locator("#btnLogin").ClickAsync();
                
                await page.GotoAsync(url);
                await File.WriteAllTextAsync(options.Value.CookieFilePath, JsonSerializer.Serialize(await context.CookiesAsync()));
            }

            if (page.Url != url) {
                throw new Exception($"Login failed, stuck at {page.Url}");
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
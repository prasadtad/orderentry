using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokers
{
    public class CharlesSchwabService : ICharlesSchwabService
    {
        private readonly HttpClient httpClient;
        private readonly IOptions<CharlesSchwabSettings> options;

        private TDAuthResult? authResult;
        private bool isSignedIn = false;

        public CharlesSchwabService(HttpClient httpClient, IOptions<CharlesSchwabSettings> options)
		{
            this.httpClient = httpClient;
			this.options = options;
        }

        (string entry, string profit, string stop) ICharlesSchwabService.GetPastableFormat(StockOrder order)
        {
            return
            (
                $"BUY +{order.Count} {order.Ticker} @{order.PotentialEntry} LMT",
                $"SELL -{order.Count} {order.Ticker} @{order.PotentialProfit} LMT GTC",
                $"SELL -{order.Count} {order.Ticker} STP {order.PotentialStop} GTC"
            );
        }

        (string entry, string profit) ICharlesSchwabService.GetPastableFormat(OptionOrder order)
        {
            return
            (
                $"BUY +{order.Count} {order.Ticker} 100 {order.StrikeDate:dd MMM yy} {order.StrikePrice} CALL @{order.PotentialEntry} LMT",
                $"SELL -{order.Count} {order.Ticker} 100 {order.StrikeDate:dd MMM yy} {order.StrikePrice} CALL @{order.PotentialProfit} LMT GTC"
            );
        }

        public async Task Authenticate()
        {
            if (File.Exists("TDAmeritradeKey"))
            {
                authResult = JsonConvert.DeserializeObject<TDAuthResult>(await File.ReadAllTextAsync("TDAmeritradeKey"))!;
            }
            if (authResult != null && authResult.refresh_token_expiry > DateTime.Now.Add(TimeSpan.FromMinutes(5)))
            {
                await SignIn();
                return;
            }
            authResult = null;
            Console.WriteLine("Opening Browser. Please sign in.");
            var uri = GetSignInUrl();
            OpenBrowser(uri);
            Console.WriteLine("When complete, please input the code (code={code}) query paramater. Located inside your browser url bar.");
            string? code = null;
            while (code == null)
                code = Console.ReadLine();
            await SignIn(code);
            Console.WriteLine($"IsSignedIn : {isSignedIn}");
        }

        private string GetSignInUrl()
        {
            var encodedKey = HttpUtility.UrlEncode(options.Value.ConsumerKey);
            var encodedUri = HttpUtility.UrlEncode("http://localhost");
            var path = $"https://auth.tdameritrade.com/auth?response_type=code&redirect_uri={encodedUri}&client_id={encodedKey}%40AMER.OAUTHAP";
            return path;
        }

        private async Task SignIn(string code, string redirectUrl = "http://localhost")
        {
            var dict = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "access_type", "offline" },
                    { "client_id", $"{options.Value.ConsumerKey}@AMER.OAUTHAP" },
                    { "redirect_uri", redirectUrl },
                    { "code", HttpUtility.UrlDecode(code) }
                };
            await SignIn(dict);
        }

        private async Task SignIn()
        {            
            var dict = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "access_type", "" },
                    { "client_id", $"{options.Value.ConsumerKey}@AMER.OAUTHAP" },
                    { "redirect_uri", authResult!.redirect_url! },
                    { "refresh_token", authResult.refresh_token! },
                    { "code", HttpUtility.UrlDecode(authResult.security_code!) }
                };
            await SignIn(dict);
        }

        private async Task SignIn(Dictionary<string, string> nameValueCollection)
        {            
            var path = "https://api.tdameritrade.com/v1/oauth2/token";

            var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(nameValueCollection) };
            var res = await httpClient.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            authResult = JsonConvert.DeserializeObject<TDAuthResult>(json)!;
            authResult.security_code = nameValueCollection["code"];
            authResult.redirect_url = nameValueCollection["redirect_uri"];
            authResult.refresh_token_expiry = DateTime.Now.AddSeconds((double) authResult.refresh_token_expires_in!);
            await File.WriteAllTextAsync("TDAmeritradeKey", JsonConvert.SerializeObject(authResult));
            isSignedIn = true;
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private class TDAuthResult
        {
            public string? redirect_url { get; set; }
            public string? security_code { get; set; }
            public string? access_token { get; set; }
            public string? refresh_token { get; set; }
            public string? scope { get; set; }
            public int? expires_in { get; set; }
            public int? refresh_token_expires_in { get; set; }
            public DateTime? refresh_token_expiry { get; set; }
            public string? token_type { get; set; }
        }
    }

	public interface ICharlesSchwabService
    {
        (string entry, string profit, string stop) GetPastableFormat(StockOrder order);

        (string entry, string profit) GetPastableFormat(OptionOrder order);

        Task Authenticate();
    }
}

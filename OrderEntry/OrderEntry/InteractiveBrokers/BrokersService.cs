using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderEntry.MindfulTrader;

namespace OrderEntry.IB
{
    public class BrokersService : IBrokersService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<BrokersService> logger;

        public BrokersService(HttpClient httpClient, ILogger<BrokersService> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<IList<(string accountId, string displayName)>> GetAccounts()
        {
            return (await httpClient.GetFromJsonAsync<List<(string accountId, string displayName)>>("/portfolio/accounts"))!;
        }

        public async Task<bool> IsAuthenticated()
        {
            try
            {
                var response = await httpClient.GetAsync("/v1/api/iserver/auth/status");
                response.EnsureSuccessStatusCode();
                var authenticationStatus = JsonSerializer.Deserialize<AuthenticationStatus>(await response.Content.ReadAsStringAsync())!;
                if (!authenticationStatus.authenticated)
                    logger.LogWarning("Auth Status {failureReason}", authenticationStatus.fail);
                return authenticationStatus.authenticated;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Cannot check authentication");
                return false;
            }
        }

        public async Task<bool> Submit(WatchlistStock watchlistStock)
        {
            return false;
        }

        private class AuthenticationStatus
        {
            public bool authenticated { get; set; }

            public string? fail { get; set; }
        }
    }

    public interface IBrokersService
    {
        Task<bool> IsAuthenticated();

        Task<IList<(string accountId, string displayName)>> GetAccounts();

        Task<bool> Submit(WatchlistStock watchlistStock);
    }
}


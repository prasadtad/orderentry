using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderEntry.Database;
using OrderEntry.Utils;

namespace OrderEntry.Apis
{
    public class PolygonApiService : IPolygonApiService
    {
        private readonly ILogger<PolygonApiService> logger;
        private readonly HttpClient httpClient;
        private readonly IOptions<PolygonApiSettings> options;
        private readonly IDatabaseService databaseService;

        private DateTime _tooManyRequestsTimestamp;

        private const string BaseUrl = "https://api.polygon.io";

        public PolygonApiService(ILogger<PolygonApiService> logger, HttpClient httpClient, IOptions<PolygonApiSettings> options, IDatabaseService databaseService)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            this.options = options;
            this.databaseService = databaseService;
        }

        public async Task<StockDayData?> GetStockData(DateOnly date, string ticker)
        {
            var stockData = await databaseService.GetStockDayData(date, ticker);
            if (stockData == null)
            {
                stockData = await GetStockDayDataFromApi(date, ticker);
                if (stockData != null)
                    await databaseService.Insert(new List<StockDayData> { stockData });
            }

            return stockData;
        }

        public async Task<List<OptionContract>> GetOptionContracts(DateOnly expirationDate, string type,  string ticker)
        {
            var optionContracts = await databaseService.GetOptionContracts(expirationDate, type, ticker);
            if (optionContracts.Count == 0)
            {
                optionContracts = await GetOptionContractsFromApi(expirationDate, type, ticker);
                if (optionContracts.Count > 0)
                    await databaseService.Insert(optionContracts);
            }

            return optionContracts;
        }

        public async Task FillEndOfDayData(int numDays, string ticker)
        {
            var date = DateUtils.TodayEST;
            var earliestDate = date.AddDays(-numDays);
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            var marketHolidays = await databaseService.GetMarketHolidays();
            date = await FillEndOfDayData(date.AddDays(-1), ticker, earliestDate, marketHolidays);
            while (date >= earliestDate && await timer.WaitForNextTickAsync())
            {
                date = await FillEndOfDayData(date, ticker, earliestDate, marketHolidays);
            }
        }

        public async Task FillEndOfDayData(List<(string ticker, DateOnly date)> tickerAndDates)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            var marketHolidays = await databaseService.GetMarketHolidays();
            await FillEndOfDayData(tickerAndDates, marketHolidays);
            while (tickerAndDates.Count == 0 && await timer.WaitForNextTickAsync())
            {
                await FillEndOfDayData(tickerAndDates, marketHolidays);
            }
        }

        private async Task<DateOnly> FillEndOfDayData(DateOnly date, string ticker, DateOnly earliestDate, List<DateOnly> marketHolidays)
        {
            var numCalls = 0;
            while (numCalls < 5 && date >= earliestDate && _tooManyRequestsTimestamp < DateTime.Now.Subtract(TimeSpan.FromMinutes(5)))
            {
                if (date.DayOfWeek == DayOfWeek.Sunday || date.DayOfWeek == DayOfWeek.Saturday || marketHolidays.Contains(date) || await databaseService.HasStockDayData(date, ticker))
                {
                    date = date.AddDays(-1);
                    continue;
                }

                var stockData = await GetStockDayDataFromApi(date, ticker);
                numCalls++;

                if (stockData != null)
                {
                    await databaseService.Insert(new List<StockDayData> { stockData });
                }

                date = date.AddDays(-1);
            }

            return date;
        }

        private async Task FillEndOfDayData(List<(string ticker, DateOnly date)> tickerAndDates, List<DateOnly> marketHolidays)
        {
            var numCalls = 0;
            while (numCalls < 5 && _tooManyRequestsTimestamp < DateTime.Now.Subtract(TimeSpan.FromMinutes(5)))
            {
                var first = tickerAndDates.First();
                tickerAndDates.Remove(first);
                var date = DateUtils.GetLastWorkingDay(first.date, marketHolidays.Contains);
                if (await databaseService.HasStockDayData(date, first.ticker))
                {
                    continue;
                }

                var stockData = await GetStockDayDataFromApi(date, first.ticker);
                numCalls++;

                if (stockData != null)
                {
                    await databaseService.Insert(new List<StockDayData> { stockData });
                }
            }
        }

        private async Task<StockDayData?> GetStockDayDataFromApi(DateOnly date, string ticker)
        {
            var dateString = date.ToString("yyyy-MM-dd");
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{BaseUrl}/v1/open-close/{ticker}/{dateString}?adjusted=true&apiKey={options.Value.ApiKey}"),
                Method = HttpMethod.Get
            };
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StockDayData>();
            }

            logger.LogError("Unable to get stock data for {ticker} on {date}, got {status} {error}", ticker, date, response.StatusCode, response.ReasonPhrase);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                _tooManyRequestsTimestamp = DateTime.Now;

            return null;
        }

        private async Task<List<OptionContract>> GetOptionContractsFromApi(DateOnly expirationDate, string type,  string ticker)
        {            
            var expirationDateString = expirationDate.ToString("yyyy-MM-dd");
            var url = $"{BaseUrl}/v3/reference/options/contracts?underlying_ticker={ticker}&contract_type={type}&expiration_date={expirationDateString}&limit=1000";            
            var list = new List<OptionContract>();
            var sw = Stopwatch.StartNew();
            var numCalls = 0;
            do
            {
                if (numCalls == 5) {
                    var delay = TimeSpan.FromMinutes(5) - sw.Elapsed;
                    logger.LogInformation("5 calls made in last 5 minutes, waiting {delay}", delay);
                    await Task.Delay(delay);
                    sw.Restart();
                    numCalls = 0;
                }
                var results = await GetOptionContractsFromApi($"{url}&apiKey={options.Value.ApiKey}");
                numCalls++;
                if (results == null) {
                    return [];
                }
                list.AddRange(results.Results);
                url = results.NextUrl;                
            } while (!string.IsNullOrEmpty(url));
            
            return list;
        }

        private async Task<OptionContractResults?> GetOptionContractsFromApi(string url)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get
            };
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OptionContractResults>();
            }

            logger.LogError("Unable to get option contracts, got {status} {error}", response.StatusCode, response.ReasonPhrase);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                _tooManyRequestsTimestamp = DateTime.Now;

            return null;
        }

        private class OptionContractResults
        {
            [JsonPropertyName("results")] public required List<OptionContract> Results { get; set; }

            [JsonPropertyName("next_url")] public string? NextUrl {get;set;}
        }
    }

    public interface IPolygonApiService
    {
        Task<StockDayData?> GetStockData(DateOnly date, string ticker);

        Task<List<OptionContract>> GetOptionContracts(DateOnly expirationDate, string type,  string ticker);

        Task FillEndOfDayData(int numDays, string ticker);

        Task FillEndOfDayData(List<(string ticker, DateOnly date)> tickerAndDates);
    }
}

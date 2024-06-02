using System.Diagnostics;
using System.Net.Http.Json;
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

        public async Task FillEndOfDayData(int numDays, string ticker)
        {
            var date = DateUtils.TodayEST;
            var earliestDate = date.AddDays(-numDays);
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            date = await FillEndOfDayData(date.AddDays(-1), ticker, earliestDate);
            while (date >= earliestDate && await timer.WaitForNextTickAsync()) {
                date = await FillEndOfDayData(date, ticker, earliestDate);
            }
        }

        private async Task<DateOnly> FillEndOfDayData(DateOnly date, string ticker, DateOnly earliestDate)
        {
            var numCalls = 0;
            while (numCalls < 5 && date >= earliestDate && _tooManyRequestsTimestamp < DateTime.Now.Subtract(TimeSpan.FromMinutes(5)))
            {
                if (date.DayOfWeek == DayOfWeek.Sunday || date.DayOfWeek == DayOfWeek.Saturday || await databaseService.IsMarketHoliday(date) || await databaseService.HasStockDayData(date, ticker))
                {
                    date = date.AddDays(-1);
                    continue;
                }

                var stockData = await GetStockDayDataFromApi(date, ticker);
                numCalls++;

                if (stockData != null) {
                    await databaseService.Insert(new List<StockDayData> { stockData });                    
                }

                date = date.AddDays(-1);
            }

            return date;
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
    }

    public interface IPolygonApiService
    {
        Task<StockDayData?> GetStockData(DateOnly date, string ticker);

        Task FillEndOfDayData(int numDays, string ticker);
    }
}

using Microsoft.Extensions.Logging;
using OrderEntry.Algorithms;
using OrderEntry.Apis;
using OrderEntry.Database;
using OrderEntry.Utils;

namespace AutoTrader
{
    public class App(ILogger<App> logger, IDatabaseService databaseService, IPolygonApiService polygonApiService, ICoveredCallStrategy coveredCallStrategy)
    {
        private const string Ticker = "BRKB";

        public async Task Run()
        {
            var today = DateUtils.TodayEST;

            logger.LogInformation("Today is {date}", today);
            var marketHolidays = await databaseService.GetMarketHolidays();

            var monthlyExpirations = (await databaseService.GetMonthlyExpirations()).OrderBy(e => e);            

            var expirationThreeMonthsAgo = monthlyExpirations.Where(m => m <= today).SkipLast(3).Last();
            logger.LogInformation("Expiration date three months ago is {date}", expirationThreeMonthsAgo);

            await polygonApiService.FillEndOfDayData(today.DayNumber - expirationThreeMonthsAgo.DayNumber, Ticker);

            var firstWorkingDayAfterExpirationThreeMonthsAgo = DateUtils.GetFirstWorkingDayOfWeek(expirationThreeMonthsAgo.AddDays(7), marketHolidays.Contains);
            logger.LogInformation("First working date after that is {date}", firstWorkingDayAfterExpirationThreeMonthsAgo);

            var lastExpiration = monthlyExpirations.Where(m => m <= today).Last();
            logger.LogInformation("Last expiration date before today is {date}", lastExpiration);
            
            var nextExpiration = monthlyExpirations.Where(m => m > today).First();
            logger.LogInformation("The next expiration date after today is {date}", nextExpiration);

            var historicalPrices = (await databaseService.GetStockDayData(firstWorkingDayAfterExpirationThreeMonthsAgo, lastExpiration.AddDays(1), Ticker)).OrderBy(o => o.From).ToList();
            logger.LogInformation("Getting {count} prices for {ticker} from {date1} to {date2}", historicalPrices.Count, Ticker, firstWorkingDayAfterExpirationThreeMonthsAgo, lastExpiration);

            var standardDeviation = coveredCallStrategy.CalculateStandardDeviation(historicalPrices.Select(p => Convert.ToDouble(p.Close)).ToList());
            logger.LogInformation("The standard deviation is {standardDeviation}", standardDeviation);

            var firstHistoricalPrice = historicalPrices.First();
            var lastHistoricalPrice = historicalPrices.Last();            
            var estimatedPriceNextMonth = coveredCallStrategy.EstimateNextMonthPrice(Convert.ToDouble(lastHistoricalPrice.Close), Convert.ToDouble(firstHistoricalPrice.Close));
            logger.LogInformation("The estimated price for {nextExpiration} is {estimatedPrice} based on price going from {firstPrice} on {firstDate} to {lastPrice} on {lastDate}", nextExpiration, estimatedPriceNextMonth, firstHistoricalPrice.Close, firstHistoricalPrice.From, lastHistoricalPrice.Close, lastHistoricalPrice.From);

            var contracts = await polygonApiService.GetOptionContracts(nextExpiration, "call", Ticker);
            var strikeLimit = 0.7 * estimatedPriceNextMonth;
            logger.LogInformation("Calculating expected premium for prices below {price}", strikeLimit);
            foreach (var contract in contracts.Where(c => Convert.ToDouble(c.StrikePrice) <= strikeLimit).TakeLast(10))
            {
                
                var minPremium = coveredCallStrategy.GetMinimumPremiumRequired(estimatedPriceNextMonth, standardDeviation, Convert.ToDouble(contract.StrikePrice));
                logger.LogInformation("{premium} required for strike {strikePrice} with profit {profit}", minPremium, contract.StrikePrice, estimatedPriceNextMonth - Convert.ToDouble(contract.StrikePrice) - minPremium);
            }
        }
    }
}

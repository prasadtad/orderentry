using Microsoft.Extensions.Logging;
using OrderEntry.Algorithms;
using OrderEntry.Apis;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.Utils;

namespace AutoTrader
{
    public class App(ILogger<App> logger, IDatabaseService databaseService, IPolygonApiService polygonApiService, ICoveredCallStrategy coveredCallStrategy)
    {
        private const string Ticker = "BRKB";

        public async Task Run()
        {
            var parseSettings = await databaseService.GetParseSettings();
            if (parseSettings.Count == 0)
                logger.LogWarning("No active parse settings found in database");

            var recommendations = (await databaseService.GetInsiderRecommendations())
                                        .OrderByDescending(o => o.Date)
                                        .ToList();
            var themes = recommendations.Select(r => r.Theme).Distinct().OrderBy(o => o).ToList();

            var interactiveBrokersPositions = (await databaseService.GetStockPositions(OrderEntry.MindfulTrader.Brokers.InteractiveBrokers))
                                        .Where(p => !p.ActivelyTrade).ToList();
            var charlesSchwabPositions = (await databaseService.GetStockPositions(OrderEntry.MindfulTrader.Brokers.CharlesSchwab))
                                        .Where(p => !p.ActivelyTrade).ToList();                                        

            logger.LogInformation("{count} themes", themes.Count);
            logger.LogInformation("{count} recommendations", recommendations.Count);
            
            foreach (var parseSetting in parseSettings)
            {
                var balance = parseSetting.GetInsiderRecommendationAccountBalance();
                logger.LogInformation("Account: {account}", parseSetting.Account);
                logger.LogInformation("Balance: ${balance} out of ${total}", balance, parseSetting.AccountBalance);
                logger.LogInformation("New position: ${balance}", 0.02m * balance);
                logger.LogInformation("New sector: ${balance}", 0.1m * balance);

                var existingPositions = parseSetting.Broker == OrderEntry.MindfulTrader.Brokers.InteractiveBrokers ? 
                    interactiveBrokersPositions.Where(p => p.AccountId == parseSetting.AccountId).ToList() : charlesSchwabPositions.Where(c => c.AccountId == parseSetting.AccountId).ToList();
                var existingPositionsByTheme = existingPositions.GroupBy(p => recommendations.FirstOrDefault(r => r.Ticker == p.Ticker)?.Theme ?? string.Empty)
                                                                 .ToDictionary(g => g.Key, g => g.ToList());                
                foreach (var theme in themes)
                {
                    if (existingPositionsByTheme.ContainsKey(theme))
                        logger.LogInformation("  {theme} - {count} investments, cost: ${balance}", theme, existingPositionsByTheme[theme].Count, existingPositionsByTheme[theme].Sum(o => o.AverageCost * o.Count));
                }

                var themeLimit = existingPositionsByTheme.ToDictionary(e => e.Key, e => e.Value.Count);
                var totalLimit = existingPositions.Count;
                
                foreach (var recommendation in recommendations)
                {
                    if (totalLimit >= 50)
                    {
                        logger.LogWarning("  {count} investments already, skipping all further recommendations", totalLimit);
                        break;
                    }

                    var theme = recommendation.Theme;
                    themeLimit.TryAdd(theme, 0);

                    if (themeLimit[theme] >= 5)
                    {
                        logger.LogWarning("  {count} investments in {theme} already, skipping {ticker}", themeLimit[theme], theme, recommendation.Ticker);
                        continue;
                    }                        

                    if (existingPositions.Any(e => e.Ticker == recommendation.Ticker))
                    {
                        logger.LogWarning("  Already invested in {theme}, skipping {ticker}", theme, recommendation.Ticker);
                        continue;
                    }

                    logger.LogInformation("    Recommendation: {ticker} in {theme}, value: ${value}", recommendation.Ticker, recommendation.Theme, 0.02m * balance);
                    themeLimit[theme]++;
                    totalLimit++;
                }
            }
        }

        private async Task RunOptions()
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

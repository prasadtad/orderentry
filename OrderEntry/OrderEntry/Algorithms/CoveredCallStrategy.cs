namespace OrderEntry.Algorithms
{
    public class CoveredCallStrategy : ICoveredCallStrategy
    {
        public decimal GetMinimumPremiumRequired(decimal estimatedPriceNextMonth, decimal standardDeviation, decimal strikePrice)
        {
            return 0;
        }

       	public decimal EstimateNextMonthPrice(decimal currentPrice, decimal priceThreeMonthsAgo)
        {
            var threeMonthPerformance = (currentPrice - priceThreeMonthsAgo) * 100 / priceThreeMonthsAgo;		
            return currentPrice + currentPrice * (threeMonthPerformance / 3) / 100;
        }

        public decimal CalculateStandardDeviation()
        {
            return 0;
        }
    }

    public interface ICoveredCallStrategy
    {
        decimal GetMinimumPremiumRequired(decimal estimatedPriceNextMonth, decimal standardDeviation, decimal strikePrice);

        decimal EstimateNextMonthPrice(decimal currentPrice, decimal priceThreeMonthsAgo);

        decimal CalculateStandardDeviation();
    }
}

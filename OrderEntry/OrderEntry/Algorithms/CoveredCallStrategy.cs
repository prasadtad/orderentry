using MathNet.Numerics;
using MathNet.Numerics.Statistics;

namespace OrderEntry.Algorithms
{
    public class CoveredCallStrategy : ICoveredCallStrategy
    {
        public double GetMinimumPremiumRequired(double estimatedPriceNextMonth, double standardDeviation, double strikePrice)
        {
            var sqrt2Pi = Math.Sqrt(2*Math.PI);
            var estimatedStrikeDistance = strikePrice - estimatedPriceNextMonth;
            var estimatedStrikeDistanceSquare = estimatedStrikeDistance * estimatedStrikeDistance;
            var standardDeviationSquare = standardDeviation * standardDeviation;
            return (standardDeviation/sqrt2Pi*Math.Pow(Math.E, -estimatedStrikeDistanceSquare/(2*standardDeviationSquare)))-(estimatedStrikeDistance/2*(1-SpecialFunctions.Erf(estimatedStrikeDistance/Math.Sqrt(2*standardDeviationSquare))));
        }

        public double EstimateNextMonthPrice(double currentPrice, double priceThreeMonthsAgo)
        {
            var threeMonthPerformance = (currentPrice - priceThreeMonthsAgo) * 100 / priceThreeMonthsAgo;
            return currentPrice + currentPrice * (threeMonthPerformance / 3) / 100;
        }

        public double CalculateStandardDeviation(List<double> prices)
        {
            return prices.StandardDeviation(); 
        }
    }

    public interface ICoveredCallStrategy
    {
        double GetMinimumPremiumRequired(double estimatedPriceNextMonth, double standardDeviation, double strikePrice);

        double EstimateNextMonthPrice(double currentPrice, double priceThreeMonthsAgo);

        double CalculateStandardDeviation(List<double> prices);
    }
}

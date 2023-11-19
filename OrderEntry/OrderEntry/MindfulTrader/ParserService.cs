using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;

namespace OrderEntry.MindfulTrader
{
	public class ParserService : IParserService
	{
		private readonly ILogger<ParserService> logger;

		public ParserService(ILogger<ParserService> logger)
		{
			this.logger = logger;
		}

		public List<IOrder> ParseWatchlist(string watchlistText, Mode mode, Strategies strategy, double expectedAccountBalance)
		{
            var readOrders = false;

            bool? readStrategy = null;
            bool? readAccountBalance = null;

			var list = new List<IOrder>();
            using (StringReader sr = new StringReader(watchlistText))
            {                
                while (true)
                {
                    var line = sr.ReadLine()?.Trim();
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line == "Enter Account Balance $")
                    {
                        readAccountBalance = true;
                        continue;
                    }
                    if (readAccountBalance != null && readAccountBalance.Value)
                    {
                        if (double.Parse(line) != expectedAccountBalance)
                            throw new Exception($"Acccount balance {line} doesn't match expected {expectedAccountBalance}");
                        readAccountBalance = false;
                        continue;
                    }

                    if (line == "Strategy: ")
                    {
                        readStrategy = true;
                        continue;
                    }
                    if (readStrategy != null && readStrategy.Value)
                    {
                        if (line != strategy.ToString())
                            throw new Exception($"Strategy {line} doesn't match expected {strategy}");
                        readStrategy = false;
                        continue;
                    }

                    if (line == mode.ToString())
                    {
                        readOrders = true;
                        continue;
                    }
                    if (line.Equals("view more", StringComparison.OrdinalIgnoreCase))
                    {
                        if (readOrders)
                            throw new Exception("Detected 'View More', click to expand list.");
                        else
                            continue;
                    }
                    if (line.Equals("view less", StringComparison.OrdinalIgnoreCase))
                    {
                        readOrders = false;
                        continue;
                    }

                    if (!readOrders || line.StartsWith("Watch Date")) continue;

                    var order = mode == Mode.Options ? ReadOptionOrder(line) :
                                mode == Mode.Stocks ? ReadStockOrder(line, false) :
                                mode == Mode.LowPricedStocks ? ReadStockOrder(line, true) :
                                throw new NotImplementedException($"Unsupported mode {mode}");
                    if (order != null && order.Strategy == strategy)
                    {
                        list.Add(order);
                    }
                }
            }

            if (readAccountBalance == null) throw new Exception("Didn't find the account balance");

			return list;
		}

        private IOrder? ReadOptionOrder(string line)
        {
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new OptionOrder
                {
                    Id = Guid.NewGuid(),
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Unsupported strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    StrikeDate = DateOnly.ParseExact($"{tokens[4]}/{tokens[5]}/{tokens[6]}", "MMM/dd/yyyy"),
                    StrikePrice = double.TryParse(tokens[7].TrimStart('$'), out var strikePrice) ? strikePrice : 0,
                    Type = tokens[8] == "Call" ? OptionType.Call : throw new NotImplementedException($"Unsupported option type {tokens[8]}"),
                    Count = int.TryParse(tokens[9], out var count) ? count : 0,
                    PotentialEntry = double.Parse(tokens[10]),
                    PotentialProfit = double.Parse(tokens[11]),
                    PotentialStop = double.Parse(tokens[12]),
                    PositionValue = double.TryParse(tokens[13].TrimStart('$'), out var positionValue) ? positionValue : 0,
                    EarningsDate = tokens[14],
                    DividendsDate = tokens[15]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse option order {line} from watchlist", line);
                return null;
            }
        }

        private IOrder? ReadStockOrder(string line, bool lowPriced)
		{
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new StockOrder
                {
                    Id = Guid.NewGuid(),
                    LowPriced = lowPriced,
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Invalid strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    Count = int.TryParse(tokens[4], out var count) ? count : 0,
                    PotentialEntry = double.Parse(tokens[5]),
                    PotentialProfit = double.Parse(tokens[6]),
                    PotentialStop = double.Parse(tokens[7]),
                    CurrentPrice = double.Parse(tokens[8]),
                    DistanceInATRs = double.Parse(tokens[9]),
                    PositionValue = double.TryParse(tokens[10].TrimStart('$'), out var positionValue) ? positionValue : 0,
                    EarningsDate = tokens[11],
                    DividendsDate = tokens[12]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to parse stock order {line} from watchlist", line);
				return null;
            }
        }		
	}

	public interface IParserService
	{
        List<IOrder> ParseWatchlist(string watchlist, Mode mode, Strategies strategy, double expectedAccountBalance);
    }
}

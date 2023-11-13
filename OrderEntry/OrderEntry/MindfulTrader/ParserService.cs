using Microsoft.Extensions.Logging;

namespace OrderEntry.MindfulTrader
{
	public class ParserService : IParserService
	{
		private readonly ILogger<ParserService> logger;

		public ParserService(ILogger<ParserService> logger)
		{
			this.logger = logger;
		}

		public async Task<IList<IOrder>> ParseWatchlist(string watchlistFile, Mode mode, double expectedAccountBalance)
		{
            Console.WriteLine($"Parsing {mode} orders from {watchlistFile}");

            var readOrders = false;

            bool? readAccountBalance = null;

			var list = new List<IOrder>();
			foreach (var line in await File.ReadAllLinesAsync(watchlistFile))
			{
                if (line.Trim() == "Enter Account Balance $")
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
                if (line.Trim() == mode.ToString())
                {
                    readOrders = true;
                    continue;
                }
                if (line.Trim().Equals("view more", StringComparison.OrdinalIgnoreCase))
                {
                    if (readOrders)
                        throw new Exception("Detected 'View More', click to expand list.");
                    else
                        continue;
                }
                if (line.Trim().Equals("view less", StringComparison.OrdinalIgnoreCase))
                {
                    readOrders = false;
                    continue;
                }                
                
				if (!readOrders || line.StartsWith("Watch Date")) continue;

                var order = mode == Mode.Options ? ReadOptionOrder(line) :
                            mode == Mode.Stocks ? ReadStockOrder(line, false) :
                            mode == Mode.LowPricedStocks ? ReadStockOrder(line, true) :
                            throw new NotImplementedException($"Unsupported mode {mode}");
                if (order != null)
                {
                    if (order.WatchDate != DateOnly.FromDateTime(DateTime.Now))
                        throw new Exception($"Watch Date {order.WatchDate} is not today's date");
                    list.Add(order);
                }
			}

            if (readAccountBalance == null) throw new Exception("Didn't find the account balance");

			return list;
		}

        public IList<IOrder> ParseWatchlist(string watchlist)
        {
            var list = new List<IOrder>();

            Mode? mode = null;

            foreach (var line in watchlist.Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (mode == null)
                {
                    if (line.Equals("Stocks", StringComparison.OrdinalIgnoreCase))
                        mode = Mode.Stocks;
                    else if (line.Equals("Options", StringComparison.OrdinalIgnoreCase))
                        mode = Mode.Options;
                    if (line.Equals("Low-Priced Stocks", StringComparison.OrdinalIgnoreCase))
                        mode = Mode.LowPricedStocks;
                    continue;
                }

                if (mode == null || line.StartsWith("Watch Date")) continue;
                
                var order = mode == Mode.Options ? ReadOptionOrder(line) :
                            mode == Mode.Stocks ? ReadStockOrder(line, false) :
                            mode == Mode.LowPricedStocks ? ReadStockOrder(line, true) :
                            throw new NotImplementedException($"Unsupported mode {mode}");
                if (order != null)
                {
                    if (order.WatchDate != DateOnly.FromDateTime(DateTime.Now))
                        throw new Exception($"Watch Date {order.WatchDate} is not today's date");
                    list.Add(order);
                }
            }
            
            return list;
        }

        private IOrder? ReadOptionOrder(string line)
        {
            try
            {
                var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return new OptionOrder
                {
                    WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
                    Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : throw new NotImplementedException($"Unsupported strategy {tokens[1]} {tokens[2]}"),
                    Ticker = tokens[3],
                    StrikeDate = DateOnly.ParseExact($"{tokens[4]}/{tokens[5]}/{tokens[6]}", "MMM/dd/yyyy"),
                    StrikePrice = double.Parse(tokens[7], System.Globalization.NumberStyles.Currency),
                    Type = tokens[8] == "Call" ? OptionType.Call : throw new NotImplementedException($"Unsupported option type {tokens[8]}"),
                    Count = int.TryParse(tokens[9], out var count) ? count : 0,
                    PotentialEntry = double.Parse(tokens[10]),
                    PotentialProfit = double.Parse(tokens[11]),
                    PotentialStop = double.Parse(tokens[12]),
                    PositionValue = double.Parse(tokens[13], System.Globalization.NumberStyles.Currency),
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
                    PositionValue = double.Parse(tokens[10].TrimStart('$')),
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
        Task<IList<IOrder>> ParseWatchlist(string watchlistFile, Mode mode, double expectedAccountBalance);

        IList<IOrder> ParseWatchlist(string watchlist);
    }
}

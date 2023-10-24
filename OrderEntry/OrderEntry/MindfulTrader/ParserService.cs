using Microsoft.Extensions.Logging;

namespace OrderEntry.MindfulTrader
{
	public class ParserService : IParserService
	{
		private ILogger<ParserService> logger;

		public ParserService(ILogger<ParserService> logger)
		{
			this.logger = logger;
		}

		public async Task<IList<WatchlistStock>> ParseWatchlist(string watchlistFile)
		{
			var headerRead = false;
			var list = new List<WatchlistStock>();
			foreach (var line in await File.ReadAllLinesAsync(watchlistFile))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;

				if (!headerRead)
				{
					headerRead = true;
					continue;
				}

				try
				{
					var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
					list.Add(new WatchlistStock
					{
						WatchDate = DateOnly.ParseExact(tokens[0], "MM/dd/yyyy"),
						Strategy = tokens[1] == "Main" && tokens[2] == "Pullback" ? Strategies.MainPullback : tokens[1] == "Double" && tokens[2] == "Down" ? Strategies.DoubleDown : null,
						Ticker = tokens[3],
						ShareCount = int.Parse(tokens[4]),
						PotentialEntry = double.Parse(tokens[5]),
						PotentialProfit = double.Parse(tokens[6]),
						PotentialStop = double.Parse(tokens[7]),
						CurrentPrice = double.Parse(tokens[8]),
						DistanceInATRs = double.Parse(tokens[9]),
						StockPositionValue = double.Parse(tokens[10].TrimStart('$')),
						EarningsDate = tokens[11],
						DividendsDate = tokens[12]
					});
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Unable to parse {line} from watchlist", line);
					continue;
				}				
			}

			return list;
		}
	}

	public interface IParserService
	{
		Task<IList<WatchlistStock>> ParseWatchlist(string watchlistFile);
	}
}

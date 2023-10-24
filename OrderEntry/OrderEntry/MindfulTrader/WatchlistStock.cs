namespace OrderEntry.MindfulTrader
{
    public class WatchlistStock
	{
		public DateOnly WatchDate { get; set; }
		public Strategies? Strategy { get; set; }
		public required string Ticker { get; set; }
		public int ShareCount { get; set; }
		public double PotentialEntry { get; set; }
		public double PotentialProfit { get; set; }
		public double PotentialStop { get; set; }
		public double CurrentPrice { get; set; }
		public double DistanceInATRs { get; set; }
		public double StockPositionValue { get; set; }
		public required string EarningsDate { get; set; }
		public required string DividendsDate { get; set; }

        public override string ToString()
        {
            return $"{ShareCount} {Ticker} limit {PotentialEntry} DAY with {PotentialProfit} profit GTC and {PotentialStop} stop GTC";
        }
    }
}

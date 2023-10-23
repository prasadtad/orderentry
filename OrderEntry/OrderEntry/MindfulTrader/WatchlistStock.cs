namespace OrderEntry.MindfulTrader
{
    public class WatchlistStock
	{
		public DateOnly WatchDate { get; set; }
		public Strategies? Strategy { get; set; }
		public required string Ticker { get; set; }
		public int ShareCount { get; set; }
		public decimal PotentialEntry { get; set; }
		public decimal PotentialProfit { get; set; }
		public decimal PotentialStop { get; set; }
		public decimal CurrentPrice { get; set; }
		public decimal DistanceInATRs { get; set; }
		public decimal StockPositionValue { get; set; }
		public required string EarningsDate { get; set; }
		public required string DividendsDate { get; set; }

        public override string ToString()
        {
            return $"{ShareCount} {Ticker} limit {PotentialEntry} DAY with {PotentialProfit} profit GTC and {PotentialStop} stop GTC";
        }
    }
}

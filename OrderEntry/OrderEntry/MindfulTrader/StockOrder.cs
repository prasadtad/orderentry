namespace OrderEntry.MindfulTrader
{
    public class StockOrder: IOrder
	{
		public required bool LowPriced { get; set; }
		public required DateOnly WatchDate { get; set; }
		public required Strategies Strategy { get; set; }
		public required string Ticker { get; set; }
		public required int Count { get; set; }
		public required double PotentialEntry { get; set; }
		public required double PotentialProfit { get; set; }
		public required double PotentialStop { get; set; }
		public required double CurrentPrice { get; set; }
		public required double DistanceInATRs { get; set; }
		public required double PositionValue { get; set; }
		public required string EarningsDate { get; set; }
		public required string DividendsDate { get; set; }		

        public override string ToString()
        {
            return $"{Strategy} {Count} {Ticker} [{PotentialStop} <- {PotentialEntry} -> {PotentialProfit}]";
        }
    }
}

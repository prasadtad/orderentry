namespace OrderEntry.MindfulTrader
{
    public class StockOrder: IOrder
	{
        public required Guid Id { get; set; }
        public required bool LowPriced { get; set; }
		public required DateOnly WatchDate { get; set; }
		public required Strategies Strategy { get; set; }
		public required string Ticker { get; set; }
		public required int Count { get; set; }
		public required decimal PotentialEntry { get; set; }
		public required decimal PotentialProfit { get; set; }
		public required decimal PotentialStop { get; set; }
		public required decimal CurrentPrice { get; set; }
		public required decimal DistanceInATRs { get; set; }
		public required decimal PositionValue { get; set; }
		public required string EarningsDate { get; set; }
		public required string DividendsDate { get; set; }
        public required int EntryOrderId { get; set; }
        public required int ProfitOrderId { get; set; }
        public required int StopOrderId { get; set; }

        public override string ToString()
        {
            return $"{Count} {Ticker} [{PotentialStop} <- {PotentialEntry} -> {PotentialProfit}]";
        }
    }
}

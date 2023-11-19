namespace OrderEntry.MindfulTrader
{
	public class OptionOrder: IOrder
	{
        public required Guid Id { get; set; }
        public required DateOnly WatchDate { get; set; }
        public required Strategies Strategy { get; set; }
        public required string Ticker { get; set; }
        public required DateOnly StrikeDate { get; set; }
        public required double StrikePrice { get; set; }
        public required OptionType Type { get; set; }
        public required int Count { get; set; }
        public required double PotentialEntry { get; set; }
        public required double PotentialProfit { get; set; }
        public required double PotentialStop { get; set; }
        public required double PositionValue { get; set; }
        public required string EarningsDate { get; set; }
        public required string DividendsDate { get; set; }

        public override string ToString()
        {
            if (PotentialStop > 0)
                return $"{Count} {Ticker} {StrikeDate} {StrikePrice} {Type} [{PotentialEntry} -> {PotentialProfit}]";
            else
                return $"{Count} {Ticker} {StrikeDate} {StrikePrice} {Type} [{PotentialStop} <- {PotentialEntry} -> {PotentialProfit}]";
        }
    }
}

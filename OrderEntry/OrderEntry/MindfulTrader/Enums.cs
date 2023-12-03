namespace OrderEntry.MindfulTrader
{
    public enum Modes
    {
		None,
        Stock,
        Option,
        LowPricedStock
    }

    public enum Strategies
	{
		None,
		MainPullback,
		DoubleDown
	}

	public enum OptionType
	{
		Call
	}

	public enum ParseTypes
	{
		Live,
		Watchlist,
		Options,
		DoubleDown,
		TriggeredList
	}
}

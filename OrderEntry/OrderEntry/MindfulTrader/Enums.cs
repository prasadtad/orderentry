using NpgsqlTypes;

namespace OrderEntry.MindfulTrader
{
    public enum Modes
    {
		[PgName("none")] None,
        [PgName("stock")] Stock,
        [PgName("option")] Option,
        [PgName("low_priced_stock")] LowPricedStock
    }

    public enum Strategies
	{
		[PgName("none")] None,
		[PgName("main_pullback")] MainPullback,
		[PgName("double_down")] DoubleDown
	}

	public enum OptionTypes
	{
		[PgName("call")] Call
	}

	public enum ParseTypes
	{
		[PgName("live")] Live,
		[PgName("watchlist")] Watchlist,
		[PgName("options")] Options,
		[PgName("double_down")] DoubleDown,
		[PgName("triggered_list")] TriggeredList
	}

	public enum Brokers
	{
		[PgName("interactive_brokers")] InteractiveBrokers,
		[PgName("charles_schwab")] CharlesSchwab
	}
}

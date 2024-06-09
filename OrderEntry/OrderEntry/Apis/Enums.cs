using NpgsqlTypes;

namespace OrderEntry.Apis
{
    public enum MarketDateTypes
    {
		[PgName("holiday")] Holiday,
        [PgName("monthly_expiration")] MonthlyExpiration
    }
}
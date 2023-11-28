namespace OrderEntry.MindfulTrader
{
    public interface IOrder
    {
        Guid Id { get; set; }
        DateOnly WatchDate { get; set; }
        Strategies Strategy { get; set; }
        string Ticker { get; set; }
        int Count { get; set; }
        double PotentialEntry { get; set; }
        double PotentialProfit { get; set; }
        double PotentialStop { get; set; }
        double PositionValue { get; set; }
        string EarningsDate { get; set; }
        string DividendsDate { get; set; }
        int EntryOrderId { get; set; }
        int ProfitOrderId { get; set; }
        int StopOrderId { get; set; }

        string ToString();
    }
}

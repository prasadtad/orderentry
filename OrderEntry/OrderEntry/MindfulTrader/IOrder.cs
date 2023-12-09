namespace OrderEntry.MindfulTrader
{
    public interface IOrder
    {
        string ParseSettingKey { get; set; }
        Guid Id { get; set; }
        DateOnly WatchDate { get; set; }
        Strategies Strategy { get; set; }
        string Ticker { get; set; }
        int Count { get; set; }
        decimal PotentialEntry { get; set; }
        decimal PotentialProfit { get; set; }
        decimal PotentialStop { get; set; }
        decimal PositionValue { get; set; }
        int? IBEntryOrderId { get; set; }
        int? IBProfitOrderId { get; set; }
        int? IBStopOrderId { get; set; }

        string ToString();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderEntry.MindfulTrader
{
    [Table("option_order")]
    public class OptionOrder : IOrder
    {        
        [Key, Column("id")] public required Guid Id { get; set; }
        [Column("parse_setting_key")] public required string ParseSettingKey { get; set; }
        [Column("watch_date")] public required DateOnly WatchDate { get; set; }
        [Column("strategy")] public required Strategies Strategy { get; set; }
        [Column("ticker")] public required string Ticker { get; set; }
        [Column("strike_date")] public required DateOnly StrikeDate { get; set; }
        [Column("strike_price")] public required decimal StrikePrice { get; set; }
        [Column("type")] public required OptionTypes Type { get; set; }
        [Column("count")] public required int Count { get; set; }
        [Column("potential_entry")] public required decimal PotentialEntry { get; set; }
        [Column("potential_profit")] public required decimal PotentialProfit { get; set; }
        [Column("potential_stop")] public required decimal PotentialStop { get; set; }
        [Column("position_value")] public required decimal PositionValue { get; set; }
        [Column("ib_entry_orderid")] public int? IBEntryOrderId { get; set; }
        [Column("ib_profit_orderid")] public int? IBProfitOrderId { get; set; }
        [Column("ib_stop_orderid")] public int? IBStopOrderId { get; set; }

        public override string ToString()
        {
            if (PotentialStop > 0)
                return $"{Count} {Ticker} {StrikeDate} {StrikePrice} {Type} [{PotentialEntry} -> {PotentialProfit}]";
            else
                return $"{Count} {Ticker} {StrikeDate} {StrikePrice} {Type} [{PotentialStop} <- {PotentialEntry} -> {PotentialProfit}]";
        }
    }
}

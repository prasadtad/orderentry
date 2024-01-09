using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokerages
{
    [Table("stock_position")]
    [PrimaryKey(nameof(Broker), nameof(AccountId), nameof(Ticker))]
    public class StockPosition
    {
        [Column("broker")] public required Brokers Broker { get; set; }
        [Column("account_id")] public required string AccountId { get; set; }
        [Column("ticker")] public required string Ticker { get; set; }
        [Column("count")] public required decimal Count { get; set; }
        [Column("average_cost")] public required decimal AverageCost { get; set; }
        [Column("actively_trade")] public required bool ActivelyTrade { get; set; }

        public override string ToString()
        {
            return $"{Ticker}";
        }
    }
}

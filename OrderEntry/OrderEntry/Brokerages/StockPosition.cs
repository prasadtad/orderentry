using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Brokerages
{
    [Table("stock_position")]
    [PrimaryKey(nameof(Broker), nameof(AccountId), nameof(Ticker))]
    public class StockPosition
    {
        [Column("broker")] public required Brokers Broker { get; set; }
        [Column("account_id")] public required string AccountId { get; set;}
        [Column("ticker")] public required string Ticker { get; set; }        
        [Column("count")] public required decimal Count { get; set; }
        [Column("average_cost")] public required decimal AverageCost { get; set; }
        [Column("actively_trade")] public required bool ActivelyTrade { get; set; }

        public override string ToString()
        {
            return $"{AccountId} - {Count} {Ticker} {AverageCost}";
        }
    }

    public class StockPositionStockComparer : IEqualityComparer<StockPosition>
    {
        public bool Equals(StockPosition? x, StockPosition? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            return x.Broker == y.Broker && x.AccountId == y.AccountId && x.Ticker == y.Ticker;
        }

        public int GetHashCode([DisallowNull] StockPosition obj)
        {
            return (obj.Broker, obj.AccountId, obj.Ticker).GetHashCode();
        }
    }
}

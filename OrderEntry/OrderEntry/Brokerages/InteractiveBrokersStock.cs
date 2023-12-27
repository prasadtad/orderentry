using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Brokerages
{
    [Table("interactive_brokers_stocks")]
    [PrimaryKey(nameof(AccountId), nameof(Ticker))]
    public class InteractiveBrokersStock
    {
        [Column("account_id")] public required string AccountId { get; set; }
        [Column("ticker")] public required string Ticker { get; set; }        
        [Column("count")] public required decimal Count { get; set; }
        [Column("average_cost")] public required decimal AverageCost { get; set; }
        [Column("actively_trade")] public required bool ActivelyTrade { get; set; }

        public override string ToString()
        {
            return $"{AccountId} - {Count} {Ticker} {AverageCost}";
        }
    }

    public class InteractiveBrokersStockComparer : IEqualityComparer<InteractiveBrokersStock>
    {
        public bool Equals(InteractiveBrokersStock? x, InteractiveBrokersStock? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            return x.AccountId == y.AccountId && x.Ticker == y.Ticker;
        }

        public int GetHashCode([DisallowNull] InteractiveBrokersStock obj)
        {
            return (obj.AccountId, obj.Ticker).GetHashCode();
        }
    }
}

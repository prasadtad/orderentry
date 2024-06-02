using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Apis
{
    [Table("stock_day_data")]
    [PrimaryKey(nameof(From), nameof(Symbol))]
    public class StockDayData
    {
        [Column("after_hours")] public decimal AfterHours { get; set; }
        [Column("close_price")] public decimal Close { get; set; }
        [Column("from_date")] public DateOnly From { get; set; }
        [Column("high_price")] public decimal High { get; set; }
        [Column("low_price")] public decimal Low { get; set; }
        [Column("open_price")] public decimal Open { get; set; }
        [Column("otc")] public bool? Otc { get; set; }
        [Column("pre_market")] public decimal PreMarket { get; set; }
        [Column("status")] public required string Status { get; set; }
        [Column("ticker")] public required string Symbol { get; set; }
        [Column("volume")] public decimal Volume { get; set; }
    }
}
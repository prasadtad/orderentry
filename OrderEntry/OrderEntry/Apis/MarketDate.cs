using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Apis
{
    [Table("market_date")]
    [PrimaryKey(nameof(Date))]
    public class MarketDate
    {
        [Column("date")] public DateOnly Date { get; set; }
        [Column("name")] public string? Name { get; set; }
        [Column("market_date_type")] public MarketDateTypes MarketDateType { get; set; }
    }
}
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Apis
{
    [Table("insider_recommendations")]
    [PrimaryKey(nameof(Date), nameof(Ticker))]
    public class InsiderRecommendation
    {
        [Column("date")] public DateOnly Date { get; set; }
        [Column("ticker")] public string Ticker { get; set; }
        [Column("theme")] public string Theme { get; set; }
        [Column("name")] public string Name { get; set; }
        [Column("issue")] public int Issue { get; set; }
    }
}
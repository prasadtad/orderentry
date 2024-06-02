using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Apis
{
    [Table("market_holiday")]
    [PrimaryKey(nameof(HolidayDate))]
    public class MarketHoliday
    {
        [Column("holiday_date")] public DateOnly HolidayDate { get; set; }
        [Column("name")] public required string Name { get; set; }
    }
}
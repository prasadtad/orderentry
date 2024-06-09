using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OrderEntry.Apis
{
    [Table("option_contract")]
    [PrimaryKey(nameof(Ticker))]
    public class OptionContract
    {
        [JsonPropertyName("cfi")] [Column("cfi")] public required string Cfi { get; set; }
        [JsonPropertyName("contract_type")] [Column("contract_type")] public required string ContractType { get; set; }
        [JsonPropertyName("exercise_style")] [Column("exercise_style")] public required string ExerciseStyle { get; set; }
        [JsonPropertyName("expiration_date")] [Column("expiration_date")] public DateOnly ExpirationDate { get; set; }
        [JsonPropertyName("primary_exchange")] [Column("primary_exchange")] public required string PrimaryExchange { get; set; }
        [JsonPropertyName("shares_per_contract")] [Column("shares_per_contract")] public int SharesPerContract { get; set; }
        [JsonPropertyName("strike_price")] [Column("strike_price")] public decimal StrikePrice { get; set; }
        [JsonPropertyName("ticker")] [Column("ticker")] public required string Ticker { get; set; }
        [JsonPropertyName("underlying_ticker")] [Column("underlying_ticker")] public required string UnderlyingTicker { get; set; }
    }
}
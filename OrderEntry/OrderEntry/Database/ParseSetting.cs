using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    [Table("parse_setting")]
    public class ParseSetting
    {
        [Key]
        [Column("key")] public required string Key { get; set; }

        [Column("parse_type")] public required ParseTypes ParseType { get; set; }

        [Column("account_balance")] public required decimal AccountBalance { get; set; }

        [Column("strategy")] public required Strategies Strategy { get; set; }

        [Column("mode")] public required Modes Mode { get; set; }

        public override string ToString()
        {
            return $"{Key} - {ParseType} {AccountBalance} {Mode} {Strategy}";
        }
    }
}

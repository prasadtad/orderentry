using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    [Table("parse_setting")]
    public class ParseSetting
    {        
        [Key, Column("key")] public required string Key { get; set; }

        [Column("parse_type")] public required ParseTypes ParseType { get; set; }

        [Column("account_balance")] public required decimal AccountBalance { get; set; }

        [Column("mindful_trader_allocation")] public required decimal MindfulTraderAllocation { get; set; }

        [Column("insider_recommendation_allocation")] public required decimal InsiderRecommendationAllocation { get; set; }

        [Column("strategy")] public required Strategies Strategy { get; set; }

        [Column("mode")] public required Modes Mode { get; set; }

        [Column("broker")] public required Brokers Broker { get; set; }

        [Column("account_id")] public required string AccountId { get; set;}

        [Column("account")] public required string Account { get; set;}

        [Column("active")] public required bool Active { get; set;}

        public override string ToString()
        {
            return $"{Key} - {ParseType} {AccountBalance} {Mode} {Strategy}";
        }

        public decimal GetMindfulTraderAccountBalance()
        {
            return Math.Min(AccountBalance, AccountBalance * MindfulTraderAllocation);
        }

        public decimal GetInsiderRecommendationAccountBalance()
        {
            return Math.Min(AccountBalance, AccountBalance * InsiderRecommendationAllocation);
        }
    }
}

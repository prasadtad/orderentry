using OrderEntry.MindfulTrader;

namespace OrderEntry.Database
{
    public class ParseSetting
    {
        public required string Key { get; set; }

        public required ParseTypes ParseType { get; set; }

        public required decimal AccountBalance { get; set; }

        public required Strategies Strategy { get; set; }

        public required Modes Mode { get; set; }

        public override string ToString()
        {
            return $"{Key} - {ParseType} {AccountBalance} {Mode} {Strategy}";
        }
    }
}

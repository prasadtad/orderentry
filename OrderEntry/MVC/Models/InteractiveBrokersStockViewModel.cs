namespace MVC.Models
{
    public class InteractiveBrokersStockViewModel
    {
        public required string Account { get; set; }

        public required string Ticker { get; set; }

        public required decimal Count { get; set; }

        public required decimal AverageCost { get; set; }

        public required bool ActivelyTrade { get; set; }

        public required string BackgroundColor { get; set; }
    }
}
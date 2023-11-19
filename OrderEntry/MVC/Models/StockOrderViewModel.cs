namespace MVC.Models
{
	public class StockOrderViewModel
	{
        public required bool Selected { get; set; }

        public required Guid Id { get; set; }

        public required string Description { get; set; }

		public required string TOSEntry { get; set; }

        public required string TOSProfit { get; set; }

        public required string TOSStop { get; set; }

        public required double PositionValue { get; set; }

        public required string BackgroundColor { get; set; }
    }
}

namespace MVC.Models
{
	public class StockOrderViewModel
	{
        public Guid Id { get; set; }

        public string Description { get; set; }

		public string TOSEntry { get; set; }

        public string TOSProfit { get; set; }

        public string TOSStop { get; set; }
    }
}

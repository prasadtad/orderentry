namespace MVC.Models
{
	public class OrdersViewModel
	{
		public List<StockOrderViewModel> Stock { get; set; }

		public List<OptionOrderViewModel> Option { get; set; }
	}
}

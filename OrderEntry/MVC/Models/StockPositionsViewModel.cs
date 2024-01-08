namespace MVC.Models
{
    public class StockPositionsViewModel
    {
        public required List<StockPositionViewModel> InteractiveBrokers { get; set; }

        public required List<StockPositionViewModel> CharlesSchwab { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using OrderEntry.MindfulTrader;

namespace MVC.Models
{
	public class ImportViewModel
	{
		public required Mode Mode { get; set; }

        public required Strategies Strategy { get; set; }

        [RegularExpression("([1-9][0-9]*)", ErrorMessage = "Count must be a natural number")]
        public required double AccountBalance { get; set; }

		public required string Text { get; set; }
	}
}


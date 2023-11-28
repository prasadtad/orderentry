using Microsoft.AspNetCore.Mvc.Rendering;

namespace MVC.Models
{
    public class ImportViewModel
	{
		public Guid? TradeSettingId { get; set; }

        public required List<SelectListItem> TradeSettings { get; set; }

		public string? Text { get; set; }
	}
}


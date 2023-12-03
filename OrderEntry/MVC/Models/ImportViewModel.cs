using Microsoft.AspNetCore.Mvc.Rendering;

namespace MVC.Models
{
    public class ImportViewModel
	{
		public string? ParseSettingKey { get; set; }

        public required List<SelectListItem> ParseSettings { get; set; }

		public string? Text { get; set; }
	}
}


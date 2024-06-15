using Microsoft.Extensions.Options;
using OrderEntry.Database;

namespace OrderEntry.Apis
{
    public class InsiderTraderService(IOptions<InsiderTraderSettings> options, IDatabaseService databaseService) : IInsiderTraderService
    {
        private static readonly string[] Themes = [
           "Aerospace","Agriculture","Aluminium","Argentina","Automotive","Avoid!","Banking","Banks","Base Metals","Battery Metals","Cement","Chemicals","Coal","Commodities","Construction","Consumer Goods","Copper","Cruiseliners","Diamonds","Dividend","Egypt indext","Electronics","Environment","Finance","Food","Fuel distribution","Gold","Gold and Copper","Gold, Uranium","Greece","Hong Kong","Industrial","Industrial machinery","International","Japan","Machinery","Manufacturing","Marine Services","Materials","Medical","Mining and Metals","Natural Gas","Offshore","Offshore oil","Offshore Oil","Offshore Oil and Gas","Offshore services","Offshore Services","Oil  Gas","Oil & gas","Oil & Gas","OIl & Gas","Oil and gas","Oil and Gas","Oil exploration","Oil Producers","Oil services","Oil Services","OIl services","Oil tankers","Oil Tankers","Oilfield services","Oilfield Services","Options and Warrants","Pakistan","Paper","Pharmaceuticals","Pipelines","Platinum","Ports","Postal Services","Precious metals","Precious Metals","Products","Rail","Rare Earth","Rare Earths","Real Estate","Renewable Energy","Renewables","Retail","Rigs","Russia","Security","Seismic","Self Defense","Shipping","Shipyards","Silver","Steel","Tankers","Technology","Telecom","Telecommunications","Theme","Tin","Tobacco","Transportation","Travel","Turkey","Uranium","Utilities","Utilities/Oil"
        ];

        public async Task Import(string filename)
        {
            var headerRead = false;
            var existingInsiderRecommendations = await databaseService.GetInsiderRecommendations();
            var newInsiderRecommendations = new List<InsiderRecommendation>();
            foreach (var line in await File.ReadAllLinesAsync(Path.Combine(options.Value.DataPath, filename)))
            {
                if (!headerRead)
                {
                    if (line == "Issue	Date	Theme	Name	Ticker")
                    {
                        headerRead = true;
                        continue;
                    }
                    else
                        throw new Exception("Header missing");
                }
                var insiderRecommendation = Parse(line);
                if (insiderRecommendation == null || existingInsiderRecommendations.Any(e => e.Date == insiderRecommendation.Date && e.Ticker == insiderRecommendation.Ticker))
                    continue;
                if (newInsiderRecommendations.Any(e => e.Date == insiderRecommendation.Date && e.Ticker == insiderRecommendation.Ticker))
                    continue;
                newInsiderRecommendations.Add(insiderRecommendation);
            }

            await databaseService.Insert(newInsiderRecommendations);
        }

        private static InsiderRecommendation? Parse(string line)
        {
            var ticker = line[(line.LastIndexOfAny([' ', '\t']) + 1)..];
            if (string.IsNullOrWhiteSpace(ticker) || ticker == "N/A" || ticker == "Various") return null;

            line = line[..line.LastIndexOfAny([' ', '\t'])].TrimEnd();

            var issue = line.Substring(1, line.IndexOfAny([' ', '\t']));
            var lineWithoutIssue = line[issue.Length..].TrimStart();
            issue = issue.TrimEnd();
            var dateString = lineWithoutIssue[..lineWithoutIssue.IndexOfAny([' ', '\t'])];
            var themeAndName = lineWithoutIssue[dateString.Length..].TrimStart();
            foreach (var theme in Themes.OrderByDescending(t => t.Length))
            {
                var i = themeAndName.IndexOf(theme, StringComparison.OrdinalIgnoreCase);
                if (i >= 0)
                {
                    var name = themeAndName[(i + theme.Length)..];
                    return new InsiderRecommendation
                    {
                        Date = DateOnly.ParseExact(dateString, "M/d/yyyy"),
                        Issue = int.Parse(issue),
                        Name = name.Trim(),
                        Theme = theme,
                        Ticker = ticker
                    };
                }
            }

            return null;
        }
    }

    public interface IInsiderTraderService
    {
        Task Import(string filename);
    }
}
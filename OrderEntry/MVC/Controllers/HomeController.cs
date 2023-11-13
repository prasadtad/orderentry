using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MVC.Models;
using OrderEntry.Brokers;
using OrderEntry.MindfulTrader;

namespace MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> logger;
    private readonly IParserService parserService;
    private readonly IMemoryCache memoryCache;
    private readonly ITDAmeritradeService ameritradeService;

    private const string ImportedOrdersKey = "ImportedOrders";

    public HomeController(ILogger<HomeController> logger, IParserService parserService, IMemoryCache memoryCache, ITDAmeritradeService ameritradeService)
    {
        this.logger = logger;
        this.parserService = parserService;
        this.memoryCache = memoryCache;
        this.ameritradeService = ameritradeService;
    }

    public IActionResult Index()
    {
        var model = new OrdersViewModel();
        model.Stock = new List<StockOrderViewModel>();
        model.Option = new List<OptionOrderViewModel>();
        var importedOrders = memoryCache.Get<IList<IOrder>>(ImportedOrdersKey);
        if (importedOrders != null)
        {
            foreach (var order in importedOrders)
            {
                if (order is StockOrder stockOrder)
                {
                    var toPaste = ameritradeService.GetPastableFormat(stockOrder);
                    model.Stock.Add(new StockOrderViewModel
                    {
                        Description = stockOrder.ToString(),
                        TOSEntry = toPaste.entry,
                        TOSProfit = toPaste.profit,
                        TOSStop = toPaste.stop
                    });
                }
                if (order is OptionOrder optionOrder)
                {
                    var toPaste = ameritradeService.GetPastableFormat(optionOrder);
                    model.Option.Add(new OptionOrderViewModel
                    {
                        Description = optionOrder.ToString(),
                        TOSEntry = toPaste.entry,
                        TOSProfit = toPaste.profit
                    });
                }
            }            
        }
        return View(model);
    }

    public IActionResult Import()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public void ImportOrders(ImportViewModel model)
    {
        memoryCache.Set(ImportedOrdersKey, parserService.ParseWatchlist(model.Text));
        Response.Redirect("Index");
    }
}


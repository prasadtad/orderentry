using System.Diagnostics;
using IBApi;
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
    private readonly IInteractiveBrokersService interactiveBrokersService;

    private const string ImportedStockAccountBalanceKey = "ImportedStockAccountBalance";
    private const string ImportedStockStrategyKey = "ImportedStockStrategy";
    private const string ImportedStockOrdersKey = "ImportedStockOrders";
    private const string ImportedOptionAccountBalanceKey = "ImportedOptionAccountBalance";
    private const string ImportedOptionStrategyKey = "ImportedOptionStrategy";
    private const string ImportedOptionOrdersKey = "ImportedOptionOrders";

    public HomeController(ILogger<HomeController> logger, IParserService parserService, IMemoryCache memoryCache, ITDAmeritradeService ameritradeService, IInteractiveBrokersService interactiveBrokersService)
    {
        this.logger = logger;
        this.parserService = parserService;
        this.memoryCache = memoryCache;
        this.ameritradeService = ameritradeService;
        this.interactiveBrokersService = interactiveBrokersService;
    }

    public IActionResult Stocks()
    {
        var accountBalance = memoryCache.Get<double?>(ImportedStockAccountBalanceKey) ?? 0;
        var stockOrders = memoryCache.Get<List<StockOrder>>(ImportedStockOrdersKey);
        ViewBag.Strategy = memoryCache.Get<Strategies>(ImportedStockStrategyKey);
        ViewBag.AccountBalance = accountBalance;
        var model = new List<StockOrderViewModel>();
        if (stockOrders != null)
        {
            var balance = 0.0;
            foreach (var stockOrder in stockOrders)
            {
                if (stockOrder.Count == 0) continue;

                var (entry, profit, stop) = ameritradeService.GetPastableFormat(stockOrder);
                balance += stockOrder.PositionValue;
                model.Add(new StockOrderViewModel
                {
                    Id = stockOrder.Id,
                    Description = stockOrder.ToString(),
                    TOSEntry = entry,
                    TOSProfit = profit,
                    TOSStop = stop,
                    PositionValue = stockOrder.PositionValue,
                    BackgroundColor = balance > accountBalance ? "red" : "green",
                    Selected = balance <= accountBalance
                });
            }         
        }
        return View(model);
    }

    public IActionResult Options()
    {
        var accountBalance = memoryCache.Get<double?>(ImportedOptionAccountBalanceKey) ?? 0;
        var optionOrders = memoryCache.Get<List<OptionOrder>>(ImportedOptionOrdersKey);
        ViewBag.Strategy = memoryCache.Get<Strategies>(ImportedOptionStrategyKey);
        ViewBag.AccountBalance = accountBalance;
        var model = new List<OptionOrderViewModel>();
        if (optionOrders != null)
        {
            var balance = 0.0;
            foreach (var optionOrder in optionOrders)
            {
                if (optionOrder.Count == 0) continue;

                var (entry, profit) = ameritradeService.GetPastableFormat(optionOrder);
                balance += optionOrder.PositionValue;
                model.Add(new OptionOrderViewModel
                {
                    Id = optionOrder.Id,
                    Description = optionOrder.ToString(),
                    TOSEntry = entry,
                    TOSProfit = profit,
                    PositionValue = optionOrder.PositionValue,
                    BackgroundColor = balance > accountBalance ? "red" : "green",
                    Selected = balance <= accountBalance
                });
            }
        }
        return View(model);
    }

    public IActionResult Index()
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
    public async Task SubmitStockOrders(List<StockOrderViewModel> model)
    {
        var stockOrders = memoryCache.Get<List<StockOrder>>(ImportedStockOrdersKey)?
                                      .Where(o => model.Exists(m => m.Id == o.Id && m.Selected));
        if (stockOrders == null) return;

        var ordersWithoutPositions = (await interactiveBrokersService.GetOrdersWithoutPositions(stockOrders))
                                        .Cast<StockOrder>();        
        foreach (var order in ordersWithoutPositions)
        {
            await interactiveBrokersService.Submit(order);
        }

        memoryCache.Remove(ImportedStockAccountBalanceKey);
        memoryCache.Remove(ImportedStockStrategyKey);
        memoryCache.Remove(ImportedStockOrdersKey);
        Response.Redirect("Index");
    }

    [HttpPost]
    public async Task SubmitOptionOrders(List<OptionOrderViewModel> model)
    {
        var optionOrders = memoryCache.Get<List<OptionOrder>>(ImportedOptionOrdersKey)?
                                      .Where(o => model.Exists(m => m.Id == o.Id && m.Selected));
        if (optionOrders == null) return;

        var ordersWithoutPositions = (await interactiveBrokersService.GetOrdersWithoutPositions(optionOrders))
                                        .Cast<OptionOrder>();
        var ordersWithPrices = new List<(OptionOrder order, double price, string tradingClass)>();
        foreach (var order in ordersWithoutPositions)
        {
            var price = await interactiveBrokersService.GetCurrentPrice(order.Ticker, order.StrikePrice, order.StrikeDate, order.Type);
            if (price != null)
                ordersWithPrices.Add((order, price.Value.price, price.Value.tradingClass));
        }
        
        foreach (var orderWithPrice in ordersWithPrices)
        {
            await interactiveBrokersService.Submit(orderWithPrice.order, orderWithPrice.tradingClass);
        }

        memoryCache.Remove(ImportedOptionAccountBalanceKey);
        memoryCache.Remove(ImportedOptionStrategyKey);
        memoryCache.Remove(ImportedOptionOrdersKey);
        Response.Redirect("Index");
    }

    [HttpPost]
    public void ImportOrders(ImportViewModel model)
    {
        if (model.Mode == Mode.Stocks)
        {
            memoryCache.Set(ImportedStockAccountBalanceKey, model.AccountBalance);
            memoryCache.Set(ImportedStockStrategyKey, model.Strategy);
            memoryCache.Set(ImportedStockOrdersKey, parserService.ParseWatchlist(model.Text, Mode.Stocks, model.Strategy, model.AccountBalance)                
                .Cast<StockOrder>().ToList());
            Response.Redirect("Stocks");
        }
        else if (model.Mode == Mode.Options)
        {
            memoryCache.Set(ImportedOptionAccountBalanceKey, model.AccountBalance);
            memoryCache.Set(ImportedOptionStrategyKey, model.Strategy);
            memoryCache.Set(ImportedOptionOrdersKey, parserService.ParseWatchlist(model.Text, Mode.Options, model.Strategy, model.AccountBalance)
                .Cast<OptionOrder>().ToList());
            Response.Redirect("Options");
        }
        else
            Response.Redirect("Index");
    }
}


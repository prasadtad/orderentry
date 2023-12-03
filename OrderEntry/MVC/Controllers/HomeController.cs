using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using MVC.Models;
using OrderEntry.Brokers;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

namespace MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> logger;
    private readonly IMindfulTraderService mindfulTraderService;
    private readonly IMemoryCache memoryCache;
    private readonly ICharlesSchwabService charlesSchwabService;
    private readonly IInteractiveBrokersService interactiveBrokersService;
    private readonly IDatabaseService databaseService;

    private const string ParseSettingsKey = "ParseSettings";
    private const string ImportedStockAccountBalanceKey = "ImportedStockAccountBalance";
    private const string ImportedStockStrategyKey = "ImportedStockStrategy";
    private const string ImportedStockOrdersKey = "ImportedStockOrders";
    private const string ImportedOptionAccountBalanceKey = "ImportedOptionAccountBalance";
    private const string ImportedOptionStrategyKey = "ImportedOptionStrategy";
    private const string ImportedOptionOrdersKey = "ImportedOptionOrders";

    public HomeController(ILogger<HomeController> logger, IMindfulTraderService mindfulTraderService, IMemoryCache memoryCache, ICharlesSchwabService charlesSchwabService, IInteractiveBrokersService interactiveBrokersService, IDatabaseService databaseService)
    {
        this.logger = logger;
        this.mindfulTraderService = mindfulTraderService;
        this.memoryCache = memoryCache;
        this.charlesSchwabService = charlesSchwabService;
        this.interactiveBrokersService = interactiveBrokersService;
        this.databaseService = databaseService;
    }

    public IActionResult Stocks()
    {
        var accountBalance = memoryCache.Get<decimal?>(ImportedStockAccountBalanceKey) ?? 0;
        var stockOrders = memoryCache.Get<List<StockOrder>>(ImportedStockOrdersKey);
        ViewBag.Strategy = memoryCache.Get<Strategies>(ImportedStockStrategyKey);
        ViewBag.AccountBalance = accountBalance;
        var model = new List<StockOrderViewModel>();
        if (stockOrders != null)
        {
            var balance = 0.0m;
            foreach (var stockOrder in stockOrders)
            {
                if (stockOrder.Count == 0) continue;

                var (entry, profit, stop) = charlesSchwabService.GetPastableFormat(stockOrder);
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
        var accountBalance = memoryCache.Get<decimal?>(ImportedOptionAccountBalanceKey) ?? 0;
        var optionOrders = memoryCache.Get<List<OptionOrder>>(ImportedOptionOrdersKey);
        ViewBag.Strategy = memoryCache.Get<Strategies>(ImportedOptionStrategyKey);
        ViewBag.AccountBalance = accountBalance;
        var model = new List<OptionOrderViewModel>();
        if (optionOrders != null)
        {
            var balance = 0.0m;
            foreach (var optionOrder in optionOrders)
            {
                if (optionOrder.Count == 0) continue;

                var (entry, profit) = charlesSchwabService.GetPastableFormat(optionOrder);
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

    public async Task<IActionResult> Index()
    {
        return View(new ImportViewModel
        {
            ParseSettings = (await GetParseSettings())
                    .Select(t => new SelectListItem {  Text = t.Key, Value = t.Key })
                    .ToList()
        });
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
        var ordersWithPrices = new List<(OptionOrder order, decimal price, string tradingClass)>();
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
    public async Task ImportOrders(ImportViewModel model)
    {
        if (model.ParseSettingKey == null) return;

        var parseSetting = (await GetParseSettings()).Single(t => t.Key == model.ParseSettingKey);
        if (parseSetting.Mode == Modes.Stock)
        {
            memoryCache.Set(ImportedStockAccountBalanceKey, parseSetting.AccountBalance);
            memoryCache.Set(ImportedStockStrategyKey, parseSetting.Strategy);
            var orders = (string.IsNullOrWhiteSpace(model.Text) ?
                await mindfulTraderService.GetWatchlist(parseSetting, "Content/Images/screenshot.png") :
                mindfulTraderService.ParseWatchlist(model.Text, parseSetting))
                .Cast<StockOrder>().OrderBy(s => s.DistanceInATRs).ToList();
            memoryCache.Set(ImportedStockOrdersKey, orders);            
            Response.Redirect("Stocks");
        }
        else if (parseSetting.Mode == Modes.Option)
        {
            memoryCache.Set(ImportedOptionAccountBalanceKey, parseSetting.AccountBalance);
            memoryCache.Set(ImportedOptionStrategyKey, parseSetting.Strategy);
            var orders = (string.IsNullOrWhiteSpace(model.Text) ?
                await mindfulTraderService.GetWatchlist(parseSetting, "Content/Images/screenshot.png") :
                mindfulTraderService.ParseWatchlist(model.Text, parseSetting))
                .Cast<OptionOrder>().ToList();
            memoryCache.Set(ImportedOptionOrdersKey, orders);
            Response.Redirect("Options");
        }
        else
            Response.Redirect("Index");
    }

    private async Task<List<ParseSetting>> GetParseSettings()
    {
        var parseSettings = memoryCache.Get<List<ParseSetting>>(ParseSettingsKey);
        if (parseSettings != null) return parseSettings;

        parseSettings = await databaseService.GetParseSettings();
        memoryCache.Set(ParseSettingsKey, parseSettings);
        return parseSettings;
    }
}


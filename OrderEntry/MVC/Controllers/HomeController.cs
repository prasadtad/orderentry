using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using MVC.Models;
using OrderEntry.Brokers;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;
using OrderEntry.Utils;

namespace MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> logger;
    private readonly IMemoryCache memoryCache;
    private readonly ICharlesSchwabService charlesSchwabService;
    private readonly IInteractiveBrokersService interactiveBrokersService;
    private readonly IDatabaseService databaseService;

    private const string ParseSettingsKey = "ParseSettings";
    private const string ImportedStockParseSettingKey = "ImportedStockParseSetting";
    private const string ImportedOptionParseSettingKey = "ImportedOptionParseSetting";

    public HomeController(ILogger<HomeController> logger, IMemoryCache memoryCache, ICharlesSchwabService charlesSchwabService, IInteractiveBrokersService interactiveBrokersService, IDatabaseService databaseService)
    {
        this.logger = logger;
        this.memoryCache = memoryCache;
        this.charlesSchwabService = charlesSchwabService;
        this.interactiveBrokersService = interactiveBrokersService;
        this.databaseService = databaseService;
    }

    public async Task<IActionResult> Stocks()
    {
        var importedStockParseSetting = memoryCache.Get<ParseSetting>(ImportedStockParseSettingKey);
        var accountBalance = importedStockParseSetting?.AccountBalance ?? 0;
        var stockOrders = await GetStockOrders(importedStockParseSetting);
        ViewBag.Strategy = importedStockParseSetting?.Strategy ?? Strategies.None;
        ViewBag.AccountBalance = accountBalance;
        var model = new List<StockOrderViewModel>();
        if (stockOrders != null)
        {
            var balance = 0.0m;
            foreach (var stockOrder in stockOrders)
            {
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

    public async Task<IActionResult> Options()
    {
        var importedOptionParseSetting = memoryCache.Get<ParseSetting>(ImportedOptionParseSettingKey);
        var accountBalance = importedOptionParseSetting?.AccountBalance ?? 0;
        var optionOrders = await GetOptionOrders(importedOptionParseSetting);
        ViewBag.Strategy = importedOptionParseSetting?.Strategy ?? Strategies.None;
        ViewBag.AccountBalance = accountBalance;
        var model = new List<OptionOrderViewModel>();
        if (optionOrders != null)
        {
            var balance = 0.0m;
            foreach (var optionOrder in optionOrders)
            {
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
                    .Select(t => new SelectListItem { Text = t.Key, Value = t.Key })
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
        var importedStockParseSetting = memoryCache.Get<ParseSetting>(ImportedStockParseSettingKey);
        var stockOrders = (await GetStockOrders(importedStockParseSetting))
                                      .Where(o => model.Exists(m => m.Id == o.Id && m.Selected));
        if (stockOrders == null) return;

        var ordersWithoutPositions = (await interactiveBrokersService.GetOrdersWithoutPositions(stockOrders))
                                        .Cast<StockOrder>();
        foreach (var order in ordersWithoutPositions)
        {
            await interactiveBrokersService.Submit(order);
        }

        memoryCache.Remove(ImportedStockParseSettingKey);
        Response.Redirect("Index");
    }

    [HttpPost]
    public async Task SubmitOptionOrders(List<OptionOrderViewModel> model)
    {
        var importedOptionParseSetting = memoryCache.Get<ParseSetting>(ImportedOptionParseSettingKey);
        var optionOrders = (await GetOptionOrders(importedOptionParseSetting))
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

        memoryCache.Remove(ImportedOptionParseSettingKey);
        Response.Redirect("Index");
    }

    [HttpPost]
    public async Task ImportOrders(ImportViewModel model)
    {
        if (model.ParseSettingKey == null) return;

        var parseSetting = (await GetParseSettings()).Single(t => t.Key == model.ParseSettingKey);
        if (parseSetting.Mode == Modes.Stock)
        {
            memoryCache.Set(ImportedStockParseSettingKey, parseSetting);
            Response.Redirect("Stocks");
        }
        else if (parseSetting.Mode == Modes.Option)
        {
            memoryCache.Set(ImportedOptionParseSettingKey, parseSetting);
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

    private async Task<List<StockOrder>> GetStockOrders(ParseSetting? setting)
    {
        if (setting == null) return [];

        var key = $"StockOrders:{setting.Key}";
        var orders = memoryCache.Get<List<StockOrder>>(key);
        if (orders != null) return orders;

        orders = (await databaseService.GetStockOrders(setting.Key, DateUtils.TodayEST)).OrderBy(o => o.DistanceInATRs).ToList();
        if (orders.Count > 0) memoryCache.Set(key, orders);

        return orders;
    }

    private async Task<List<OptionOrder>> GetOptionOrders(ParseSetting? setting)
    {
        if (setting == null) return [];

        var key = $"OptionOrders:{setting.Key}";
        var orders = memoryCache.Get<List<OptionOrder>>(key);
        if (orders != null) return orders;

        orders = await databaseService.GetOptionOrders(setting.Key, DateUtils.TodayEST);
        if (orders.Count > 0) memoryCache.Set(key, orders);

        return orders;
    }
}


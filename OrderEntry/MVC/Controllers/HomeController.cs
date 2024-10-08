﻿using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using MVC.Models;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;
using OrderEntry.Utils;

namespace MVC.Controllers;

public class HomeController(ILogger<HomeController> logger, IMemoryCache memoryCache, ICharlesSchwabService charlesSchwabService, IInteractiveBrokersService interactiveBrokersService, IDatabaseService databaseService) : Controller
{
    private readonly ILogger<HomeController> logger = logger;
    private readonly IMemoryCache memoryCache = memoryCache;
    private readonly ICharlesSchwabService charlesSchwabService = charlesSchwabService;
    private readonly IInteractiveBrokersService interactiveBrokersService = interactiveBrokersService;
    private readonly IDatabaseService databaseService = databaseService;

    private const string ParseSettingsKey = "ParseSettings";
    private const string ImportedStockParseSettingKey = "ImportedStockParseSetting";
    private const string ImportedOptionParseSettingKey = "ImportedOptionParseSetting";

    public async Task<IActionResult> Stocks()
    {
        var model = new List<StockOrderViewModel>();
        var importedStockParseSetting = memoryCache.Get<ParseSetting>(ImportedStockParseSettingKey);
        if (importedStockParseSetting == null)
            return View(model);

        var stockPositions = await GetStockPositions();
        var accountBalance = importedStockParseSetting.GetMindfulTraderAccountBalance();
        var stockOrders = await GetStockOrders(importedStockParseSetting);
        stockOrders = stockOrders.Where(s => !stockPositions.Any(p => 
                    p.Ticker == s.Ticker && 
                    p.Broker == importedStockParseSetting.Broker && 
                    p.AccountId == importedStockParseSetting.AccountId)).ToList();
        ViewBag.Strategy = importedStockParseSetting.Strategy;
        ViewBag.AccountBalance = accountBalance;
        
        if (stockOrders != null)
        {
            var balance = stockPositions.Where(p => p.Broker == importedStockParseSetting.Broker && p.AccountId == importedStockParseSetting.AccountId && p.ActivelyTrade)
                                        .Sum(p => p.AverageCost * p.Count);
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
        var model = new List<OptionOrderViewModel>();
        var importedOptionParseSetting = memoryCache.Get<ParseSetting>(ImportedOptionParseSettingKey);
        if (importedOptionParseSetting == null)
            return View(model);

        var accountBalance = importedOptionParseSetting.GetMindfulTraderAccountBalance();
        var optionOrders = await GetOptionOrders(importedOptionParseSetting);
        ViewBag.Strategy = importedOptionParseSetting.Strategy;
        ViewBag.AccountBalance = accountBalance;
        
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

    public IActionResult Tester()
    {
        return View();
    }

    public async Task TestCharlesSchwabOrderSubmission()
    {
        using var charlesSchwabSession = await charlesSchwabService.GetSession();
        await charlesSchwabSession.FillOrder(new StockOrder {
            Ticker = "SPY",
            Count = 10,
            CurrentPrice = 497.21m,
            DistanceInATRs = 0,
            Id = Guid.NewGuid(),
            LowPriced = false,
            ParseSettingKey = "Live Schwab IRA Stock",
            PositionValue = 2470m,
            PotentialEntry = 494.17m,
            PotentialProfit = 503.93m,
            PotentialStop = 484.41m,
            Strategy = Strategies.MainPullback,
            WatchDate = DateOnly.MinValue
        });
    }

    public async Task<IActionResult> StockPositions()
    {
        var stockPositions = await GetStockPositions();
        var allStockOrders = await GetStockOrders();
        var interactiveBrokersStockOrders = new List<StockOrder>();
        var charlesSchwabStockOrders = new List<StockOrder>();
        var parseSettings = await GetParseSettings();
        var balanceRemainingByParseSetting = new Dictionary<string, decimal>();
        foreach (var parseSetting in parseSettings)
        {
            if (parseSetting.Mode != Modes.Stock) continue;

            balanceRemainingByParseSetting.Add(parseSetting.Key, parseSetting!.GetMindfulTraderAccountBalance());
            interactiveBrokersStockOrders.AddRange(allStockOrders.Where(o => o.ParseSettingKey == parseSetting.Key && parseSetting.Broker == Brokers.InteractiveBrokers));
            charlesSchwabStockOrders.AddRange(allStockOrders.Where(o => o.ParseSettingKey == parseSetting.Key && parseSetting.Broker == Brokers.CharlesSchwab));
        }

        var recommendations = await databaseService.GetInsiderRecommendations();
        List<StockPositionViewModel> GetStockPositionsViewModel(List<StockPosition> stockPositions, Brokers broker)
        {            
            return [.. stockPositions.Where(s => s.Broker == broker).Select(stockPosition =>
            {
                var parseSetting = parseSettings.Single(p => p.Broker == broker && p.AccountId == stockPosition.AccountId);
                var recommendationsBalance = parseSetting.GetInsiderRecommendationAccountBalance();
                var orderExists = parseSetting == null ? (bool?) null :
                                  parseSetting.Broker == Brokers.CharlesSchwab ? 
                                     charlesSchwabStockOrders!.Any(o => o.ParseSettingKey == parseSetting.Key && o.Ticker == stockPosition.Ticker)
                                   : interactiveBrokersStockOrders!.Any(o => o.ParseSettingKey == parseSetting.Key && o.Ticker == stockPosition.Ticker);
                var theme = recommendations.FirstOrDefault(r => (r.Ticker.IndexOf(':') >= 0 ? r.Ticker.Substring(r.Ticker.IndexOf(':') + 1) : r.Ticker) == stockPosition.Ticker)?.Theme ?? string.Empty;
                var viewModel = new StockPositionViewModel
                {
                    AccountBalance = stockPosition.ActivelyTrade ? balanceRemainingByParseSetting[parseSetting!.Key] : recommendationsBalance,
                    Broker = stockPosition.Broker.ToString(),
                    Account = parseSetting?.Account ?? stockPosition.AccountId,
                    ActivelyTrade = stockPosition.ActivelyTrade,
                    AverageCost = stockPosition.AverageCost,
                    Count = stockPosition.Count,
                    Ticker = stockPosition.Ticker,
                    Theme = theme,
                    BackgroundColor = orderExists == null ? "gray" : !stockPosition.ActivelyTrade ? "lightblue" : !orderExists.Value ? "red" : "green"
                };
                if (stockPosition.ActivelyTrade)
                    balanceRemainingByParseSetting[parseSetting!.Key] -= stockPosition.Count * stockPosition.AverageCost;
                return viewModel;
            }).OrderBy(o => o.BackgroundColor == "gray" ? 4 : o.BackgroundColor == "lightblue" ? 3 : o.BackgroundColor == "green" ? 2 : 1)];
        }

        return View(new StockPositionsViewModel
        {
            InteractiveBrokers = GetStockPositionsViewModel(stockPositions, Brokers.InteractiveBrokers),
            CharlesSchwab = GetStockPositionsViewModel(stockPositions, Brokers.CharlesSchwab)
        });
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
        if (importedStockParseSetting == null) return;

        var stockOrders = (await GetStockOrders(importedStockParseSetting))
                                      .Where(o => model.Exists(m => m.Id == o.Id && m.Selected));
        if (stockOrders == null) return;

        var submittedOrders = new List<StockOrder>();
        if (importedStockParseSetting.Broker == Brokers.InteractiveBrokers)
        {
            foreach (var order in stockOrders)
            {
                if (await interactiveBrokersService.Submit(importedStockParseSetting.Account!, order))
                {
                    submittedOrders.Add(order);
                }
            }
        }

        if (importedStockParseSetting.Broker == Brokers.CharlesSchwab)
        {
            using var charlesSchwabSession = await charlesSchwabService.GetSession();
            foreach (var order in stockOrders)
            {
                if (await charlesSchwabSession.FillOrder(order))
                {
                    submittedOrders.Add(order);
                }
            }
        }

        await databaseService.Save(submittedOrders);
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

        var ordersWithoutPositions = (await interactiveBrokersService.GetOrdersWithoutPositions(importedOptionParseSetting!.Account!, optionOrders))
                                        .Cast<OptionOrder>();
        var ordersWithPrices = new List<(OptionOrder order, decimal price, string tradingClass)>();
        foreach (var order in ordersWithoutPositions)
        {
            var price = await interactiveBrokersService.GetCurrentPrice(importedOptionParseSetting!.Account!, order.Ticker, order.StrikePrice, order.StrikeDate, order.Type);
            if (price != null)
                ordersWithPrices.Add((order, price.Value.price, price.Value.tradingClass));
        }
        var submittedOrders = new List<OptionOrder>();
        foreach (var orderWithPrice in ordersWithPrices)
        {
            if (await interactiveBrokersService.Submit(importedOptionParseSetting!.Account!, orderWithPrice.order, orderWithPrice.tradingClass))
            {
                submittedOrders.Add(orderWithPrice.order);
            }
        }
        await databaseService.Save(submittedOrders);
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

    private async Task<List<StockOrder>> GetStockOrders()
    {
        var key = "StockOrders";
        var stockOrders = memoryCache.Get<List<StockOrder>>(key);
        if (stockOrders != null) return stockOrders;

        stockOrders = await databaseService.GetStockOrders();
        if (stockOrders.Count > 0) memoryCache.Set(key, stockOrders);

        return stockOrders;
    }

    private async Task<List<StockPosition>> GetStockPositions()
    {
        var key = "StockPositions";
        var stockPositions = memoryCache.Get<List<StockPosition>>(key);
        if (stockPositions != null) return stockPositions;

        stockPositions = await databaseService.GetStockPositions();
        if (stockPositions.Count > 0) memoryCache.Set(key, stockPositions);

        return stockPositions;
    }
}


using AutoFinance.Broker.InteractiveBrokers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderEntry.Brokers;
using OrderEntry.MindfulTrader;

namespace OrderEntry
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();

            var services = scope.ServiceProvider;
            var app = services.GetRequiredService<App>();
            await app.RunPrasadInteractiveBrokers();
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddJsonConsole(options =>
                    {
                        options.JsonWriterOptions = new()
                        {
                            Indented = true
                        };
                    });
                })
                .ConfigureServices((b, services) =>
                {
                    services.AddHttpClient();
                    services.Configure<TDAmeritradeSettings>(b.Configuration.GetSection(nameof(TDAmeritradeSettings)));
                    services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>(p => new InteractiveBrokersService(new TwsObjectFactory("localhost", 7496, 1)));
                    services.AddSingleton<ITDAmeritradeService, TDAmeritradeService>();
                    services.AddTransient<IParserService, ParserService>();
                    services.AddSingleton<App>();
                })
                .ConfigureAppConfiguration((h, c) =>
                {
                    if (h.HostingEnvironment.IsDevelopment())
                        c.AddUserSecrets<Program>();
                });
        }
    }
}
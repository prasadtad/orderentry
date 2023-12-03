using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderEntry.Brokers;
using OrderEntry.Database;
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
            await app.Run();
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
                .ConfigureServices((builder, services) =>
                {
                    services.AddHttpClient();
                    services.Configure<MindfulTraderSettings>(builder.Configuration.GetSection("MindfulTrader"));
                    services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
                    services.Configure<CharlesSchwabSettings>(builder.Configuration.GetSection("CharlesSchwab"));
                    services.Configure<InteractiveBrokersSettings>(builder.Configuration.GetSection("InteractiveBrokers"));
                    services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>();
                    services.AddSingleton<ICharlesSchwabService, CharlesSchwabService>();
                    services.AddSingleton<IMindfulTraderService, MindfulTraderService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
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
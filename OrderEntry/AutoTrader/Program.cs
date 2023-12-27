using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderEntry;
using OrderEntry.Utils;

namespace AutoTrader
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
                    logging.AddSimpleConsole(options =>
                    {
                        options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    });
                })
                .ConfigureServices((builder, services) =>
                {
                    services.AddHttpClient();
                    services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
                    services.Configure<CharlesSchwabSettings>(builder.Configuration.GetSection("CharlesSchwab"));
                    services.Configure<InteractiveBrokersSettings>(builder.Configuration.GetSection("InteractiveBrokers"));
                    services.AddBrokerages();
                    services.AddDatabase();
                    services.AddSingleton<App>();
                })
                .ConfigureAppConfiguration((h, c) =>
                {
                    c.AddUserSecrets<Program>();
                });
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderEntry;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

namespace Importer
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
                    services.AddSingleton<IMindfulTraderService, MindfulTraderService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddDbContext<OrderEntryDbContext>((provider, options) =>
                    {
                        var dbOptions = provider.GetService<IOptions<DatabaseSettings>>()!;
                        var dataSourceBuilder = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder
                        {
                            Pooling = true,
                            SslMode = SslMode.VerifyFull,
                            Host = dbOptions.Value.Host,
                            Port = dbOptions.Value.Port,
                            Username = dbOptions.Value.Username,
                            Password = dbOptions.Value.Password,
                            Database = dbOptions.Value.Database
                        }.ConnectionString);
                        dataSourceBuilder.MapEnum<Modes>("modes");
                        dataSourceBuilder.MapEnum<ParseTypes>("parse_types");
                        dataSourceBuilder.MapEnum<OptionTypes>("option_types");
                        dataSourceBuilder.MapEnum<Strategies>("strategies");
                        dataSourceBuilder.MapEnum<Brokers>("brokers");
                        options.UseNpgsql(dataSourceBuilder.Build());
                    });
                    services.AddSingleton<App>();
                })
                .ConfigureAppConfiguration((h, c) =>
                {
                    c.AddUserSecrets<Program>();
                });
        }
    }
}
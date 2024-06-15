using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderEntry.Algorithms;
using OrderEntry.Apis;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

namespace OrderEntry.Utils
{
    public static class ServiceExtensions
    {
        public static void AddBrokerages(this IServiceCollection services)
        {
            services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>();
            services.AddSingleton<ICharlesSchwabService, CharlesSchwabService>();
        }

        public static void AddDatabase(this IServiceCollection services)
        {
            services.AddScoped<IDatabaseService, DatabaseService>();
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
                dataSourceBuilder.MapEnum<MarketDateTypes>("market_date_types");
                options.UseNpgsql(dataSourceBuilder.Build());
            });
        }

        public static void AddApis(this IServiceCollection services)
        {
            services.AddSingleton<IPolygonApiService, PolygonApiService>();
            services.AddSingleton<ICoveredCallStrategy, CoveredCallStrategy>();
            services.AddSingleton<IInsiderTraderService, InsiderTraderService>();
        }
    }
}
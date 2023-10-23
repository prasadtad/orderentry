using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderEntry.IB;
using OrderEntry.MindfulTrader;
using Polly;
using Polly.Extensions.Http;

namespace OrderEntry
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();

            var services = scope.ServiceProvider;
            await services.GetRequiredService<App>().Run();
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
                .ConfigureServices((_, services) =>
                {
                    services.AddHttpClient<IBrokersService, BrokersService>(client =>
                        {
                            client.BaseAddress = new("https://localhost:5001");
                        }).AddPolicyHandler(GetRetryPolicy())
                          .ConfigurePrimaryHttpMessageHandler(() =>
                            new HttpClientHandler
                                {
                                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                                });
                    services.AddTransient<IParserService, ParserService>();
                    services.AddSingleton<App>();
                });
        }

        static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(1, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }
    }
}
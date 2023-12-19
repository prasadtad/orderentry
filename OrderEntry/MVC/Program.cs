using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderEntry;
using OrderEntry.Brokerages;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<CharlesSchwabSettings>(builder.Configuration.GetSection("CharlesSchwab"));
builder.Services.Configure<InteractiveBrokersSettings>(builder.Configuration.GetSection("InteractiveBrokers"));
builder.Services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>();
builder.Services.AddSingleton<ICharlesSchwabService, CharlesSchwabService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddDbContext<OrderEntryDbContext>((provider, options) =>
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
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path
        .Combine(Directory.GetCurrentDirectory(), "Content")),
    RequestPath = "/Content"
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


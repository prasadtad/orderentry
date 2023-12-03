using Microsoft.Extensions.FileProviders;
using OrderEntry;
using OrderEntry.Brokers;
using OrderEntry.Database;
using OrderEntry.MindfulTrader;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.Configure<MindfulTraderSettings>(builder.Configuration.GetSection("MindfulTrader"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<CharlesSchwabSettings>(builder.Configuration.GetSection("CharlesSchwab"));
builder.Services.Configure<InteractiveBrokersSettings>(builder.Configuration.GetSection("InteractiveBrokers"));
builder.Services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>();
builder.Services.AddSingleton<ICharlesSchwabService, CharlesSchwabService>();
builder.Services.AddSingleton<IMindfulTraderService, MindfulTraderService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
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


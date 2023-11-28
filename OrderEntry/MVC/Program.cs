using Microsoft.Extensions.FileProviders;
using OrderEntry.Brokers;
using OrderEntry.MindfulTrader;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.Configure<TDAmeritradeSettings>(builder.Configuration.GetSection(nameof(TDAmeritradeSettings)));
builder.Services.Configure<InteractiveBrokersSettings>(builder.Configuration.GetSection(nameof(InteractiveBrokersSettings)));
builder.Services.Configure<MindfulTraderSettings>(builder.Configuration.GetSection(nameof(MindfulTraderSettings)));
builder.Services.AddSingleton<IInteractiveBrokersService, InteractiveBrokersService>();
builder.Services.AddSingleton<ITDAmeritradeService, TDAmeritradeService>();
builder.Services.AddSingleton<IParserService, ParserService>();
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


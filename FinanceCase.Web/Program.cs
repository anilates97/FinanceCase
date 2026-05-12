using FinanceCase.Web.Data;
using FinanceCase.Web.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    client.BaseAddress = new Uri("https://testapi.finmaks.com/");
});
builder.Services.AddScoped<IAppStateService, AppStateService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IDemoDatasetService, DemoDatasetService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new Hangfire.SqlServer.SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("FinanceCase:EnableHangfireDashboard"))
{
    app.UseHangfireDashboard("/hangfire");
}

RecurringJob.AddOrUpdate<IExchangeRateService>(
    "hourly-exchange-rate-sync",
    service => service.FetchAndSaveCurrentRatesAsync(),
    "0 * * * *");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Import}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

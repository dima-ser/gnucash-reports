using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));


builder.Services.AddSingleton<IValidateOptions<AppSettings>, AppSettingsValidation>();

builder.Services.AddSingleton<DatabaseService>(serviceProvider =>
{
    var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
    return new DatabaseService(appSettings);
});


var app = builder.Build();

// we want to see full error details in production, since this app is not meant to be public facing
app.UseDeveloperExceptionPage();

// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Error");
//}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.UseRequestLocalization(new string[] { "en-US", });

// Do some additional settings validations that are not possible to do inside of AppSettings
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var options = services.GetRequiredService<IOptions<AppSettings>>();
    var appSettings = options.Value;

    // If NumYearsAvailable or NetWorthMaxYears are not provided, fetch sensible defaults from DB
    if (appSettings.NumYearsAvailable < AppSettings.MIN_NUM_YEARS_AVAILABLE || appSettings.NetWorthMaxYears <= 0)
    {
        var dbService = services.GetRequiredService<DatabaseService>();
        var dbStats = await dbService.GetDatabaseStatsAsync();
        if (appSettings.NumYearsAvailable < AppSettings.MIN_NUM_YEARS_AVAILABLE)
        {
            Console.WriteLine("NumYearsAvailable is outside of valid range, using default value based on available data in the database");
            appSettings.NumYearsAvailable = dbStats.YearsAvailableForReports;
        }
        if (appSettings.NetWorthMaxYears <= 0)
        {
            Console.WriteLine("NetWorthMaxYears is outside of valid range, using default value based on available data in the database");
            if (dbStats.YearsAvailableForReports <= AppSettings.DEFAULT_NET_WORTH_YEARS_MAX)
                appSettings.NetWorthMaxYears = dbStats.YearsAvailableForReports;
            else
                appSettings.NetWorthMaxYears = AppSettings.DEFAULT_NET_WORTH_YEARS_MAX;
        }
    }
}

app.Run();

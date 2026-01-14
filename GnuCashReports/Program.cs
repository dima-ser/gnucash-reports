using GnuCashReports.Models;
using GnuCashReports.Services;
using Microsoft.Extensions.Options;

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

app.Run();

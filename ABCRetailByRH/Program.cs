using System;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Resolve Azure Storage connection string from either section/env.
var storageConnection =
    builder.Configuration["AzureStorage:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("AzureStorage")
    ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(storageConnection))
{
    throw new InvalidOperationException(
        "Missing Azure Storage connection string. " +
        "Set AzureStorage:ConnectionString in appsettings.json OR ConnectionStrings:AzureStorage " +
        "OR AZURE_STORAGE_CONNECTION_STRING as an environment variable.");
}

// DI for storage service (ctor takes string connection)
builder.Services.AddSingleton<IAzureStorageService>(_ => new AzureStorageService(storageConnection));

// ---- Functions typed HttpClient (calls your Azure Functions) ----
var functionsBaseUrl = builder.Configuration["Functions:BaseUrl"];
if (string.IsNullOrWhiteSpace(functionsBaseUrl))
{
    throw new InvalidOperationException(
        "Missing Functions:BaseUrl. Add it to appsettings.Development.json " +
        "(e.g. \"https://<your-func-app>.azurewebsites.net\") so the web app can call your Functions.");
}

// NEW: bind only the Key (BaseAddress comes from HttpClient below)
builder.Services.Configure<FunctionsOptions>(builder.Configuration.GetSection("Functions"));

builder.Services.AddHttpClient<IFunctionsClient, FunctionsClient>(client =>
{
    client.BaseAddress = new Uri(functionsBaseUrl.TrimEnd('/')); // no /api
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

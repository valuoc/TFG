using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using SocialApp.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices();
builder.Configuration.AddJsonFile("appsettings.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Development.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
var app = builder.Build();
app.MapApiEndpoints();
app.Run();
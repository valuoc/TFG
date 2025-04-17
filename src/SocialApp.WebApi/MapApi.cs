using SocialApp.Models.Account;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;

namespace SocialApp.WebApi;

public static class MapApi
{
    record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
    
    public static void MapApiEndpoints(this WebApplication app)
    {
        MapAccount(app);
        
        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        
        app.MapGet("/health", () =>
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        summaries[Random.Shared.Next(summaries.Length)]
                    ))
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast");
    }

    private static void MapAccount(WebApplication app)
    {
        app.MapPost("/register", async (RegisterRequest model, CancellationToken cancel) =>
        {
            var account = app.Services.GetRequiredService<IAccountService>();

            var context = new OperationContext(cancel);
            return await account.RegisterAsync(model, context);
        });
    }
}
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SocialApp.Models.Account;
using SocialApp.Models.Session;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Session.Services;

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
        MapSession(app);
        
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
        app.MapPost("/register", async (RegisterRequest model, IAccountService accounts, CancellationToken cancel) =>
        {
            var context = new OperationContext(cancel);
            return await accounts.RegisterAsync(model, context);
        });
    }
    
    private static void MapSession(WebApplication app)
    {
        app.MapPost("/login", async (LoginRequest model, ISessionService sessions, HttpContext http) =>
        {
            var context = new OperationContext(http.RequestAborted);
            var session = await sessions.LoginWithPasswordAsync(model, context);
            
            if(session?.SessionId == null)
                throw new InvalidOperationException("No session was generated.");
            
            var claims = new List<Claim> { new(ClaimTypes.Sid, session.SessionId) };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
        });
        
        app.MapPost("/logout", async (ISessionService sessions, HttpContext http) =>
        {
            var context = new OperationContext(http.RequestAborted);
            var sessionId = http.User.Claims.Where(x => x.Type == ClaimTypes.Sid).Select(x => x.Value).SingleOrDefault();
            
            if(sessionId == null)
                throw new InvalidOperationException("No session was found in the request.");
            
            await sessions.EndSessionAsync(sessionId, context);
            
            return http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        });
    }
}
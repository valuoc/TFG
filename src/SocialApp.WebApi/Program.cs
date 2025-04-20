using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging.Console;
using SocialApp.WebApi;
using SocialApp.WebApi.Infrastructure;
using SocialApp.WebApi.Infrastructure.Middlewares;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices();
builder.Services.AddAuthorizationBuilder()
    .AddDefaultPolicy("test", policy => policy.RequireClaim(ClaimTypes.Sid));
builder.Configuration.AddJsonFile("appsettings.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Development.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "short";
});

builder.Logging.AddConsoleFormatter<SocialAppConsoleFormatter, ConsoleFormatterOptions>();


var app = builder.Build();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            message = "Oops!",
            detail = error?.Message
        }));
    });
});
app.UseMiddleware<RequestLog>();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();
app.Run();
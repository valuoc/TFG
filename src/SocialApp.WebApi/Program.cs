using System.Security.Claims;
using System.Text.Json;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging.Console;
using SocialApp.WebApi;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;
using SocialApp.WebApi.Infrastructure;
using SocialApp.WebApi.Infrastructure.Middlewares;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices();
builder.Services.AddAuthorizationBuilder()
    .AddDefaultPolicy("test", policy => policy.RequireClaim(ClaimTypes.Sid));
builder.Configuration.AddJsonFile("appsettings.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Development.json", false, false);
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
builder.Configuration.AddEnvironmentVariables();

if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(builder.Configuration["KEY_VAULT_URI"]),
        new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = builder.Configuration["MANAGED_CLIENT_ID"]
        }));
}
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
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        context.Response.StatusCode = error switch
        {
            ContentException { Error: ContentError.ContentNotFound } => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            message = error switch
            {
                SocialAppException saex => saex.Message,
                _ => "Oops! something went wrong."
            }
        }));
    });
});
app.UseMiddleware<RequestLog>();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();
app.Run("http://*:7000");
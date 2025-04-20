using System.Diagnostics;
using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Infrastructure.Middlewares;

public class RequestLog
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLog> _logger;

    public RequestLog(RequestDelegate next, ILogger<RequestLog> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context); // pass to next middleware
            sw.Stop();
            
            var operationContext = context.RequestServices.GetRequiredService<OperationContext>();

            _logger.LogInformation(
                "HTTP {Method} {Path} => {StatusCode}, {Elapsed}ms, {Operation}RU",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                operationContext.OperationCharge);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var operationContext = context.RequestServices.GetRequiredService<OperationContext>();
            _logger.LogError(
                "HTTP {Method} {Path} => {StatusCode}, {Elapsed}ms, {Operation}RU",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                operationContext.OperationCharge);

            throw; // rethrow to preserve behavior
        }
    }
}


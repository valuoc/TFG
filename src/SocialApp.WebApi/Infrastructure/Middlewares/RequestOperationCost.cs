using SocialApp.WebApi.Features._Shared.Services;

namespace SocialApp.WebApi.Infrastructure.Middlewares;

public class RequestOperationCost
{
    private readonly RequestDelegate _next;

    public RequestOperationCost(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var operationContext = context.RequestServices.GetRequiredService<OperationContext>();
            context.Response.Headers.Append("X-OperationCharge", operationContext.OperationCharge.ToString());
            return Task.CompletedTask;
        });
        await _next(context);
    }
}


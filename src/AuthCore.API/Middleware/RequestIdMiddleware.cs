using Serilog.Context;

namespace AuthCore.API.Middleware;

public class RequestIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[Header].FirstOrDefault()
                        ?? Guid.NewGuid().ToString();

        context.Response.Headers[Header] = requestId;

        using (LogContext.PushProperty("RequestId", requestId))
        {
            await next(context);
        }
    }
}

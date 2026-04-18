using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthCore.API.Extensions;

public static class RateLimitingExtensions
{
    public const string LoginPolicy = "login";
    public const string RegisterPolicy = "register";
    public const string GeneralPolicy = "general";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(new { error = "Too many requests. Please try again later." });
                await context.HttpContext.Response.WriteAsync(body, cancellationToken);
            };

            // 5 req/min por IP — login
            options.AddFixedWindowLimiter(LoginPolicy, o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 5;
                o.QueueLimit = 0;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            // 3 req/min por IP — register e forgot-password
            options.AddFixedWindowLimiter(RegisterPolicy, o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 3;
                o.QueueLimit = 0;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            // 60 req/min por usuário autenticado (fallback por IP)
            options.AddPolicy(GeneralPolicy, context =>
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? context.Connection.RemoteIpAddress?.ToString()
                             ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 60,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }
}

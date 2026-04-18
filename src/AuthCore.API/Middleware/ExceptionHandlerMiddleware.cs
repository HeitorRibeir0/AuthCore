using System.Text.Json;
using AuthCore.API.Exceptions;

namespace AuthCore.API.Middleware;

public class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            EmailAlreadyExistsException => (StatusCodes.Status409Conflict, exception.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, exception.Message),
            InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, exception.Message),
            InvalidPasswordException => (StatusCodes.Status400BadRequest, exception.Message),
            InvalidResetTokenException => (StatusCodes.Status401Unauthorized, exception.Message),
            KeyNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }
}

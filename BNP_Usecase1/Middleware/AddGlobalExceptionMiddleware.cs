using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BNP_Usecase1.Middleware
{
    // Single place that turns unhandled exceptions into HTTP responses (.NET 8 IExceptionHandler),
    // so repositories, business and controllers can simply throw.
    public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
            CancellationToken cancellationToken)
        {
            // Bad input from the caller => 400 with the message; anything else => 500 without internals.
            var (status, title, detail) = exception switch
            {
                ArgumentException or FormatException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            else
                logger.LogWarning("{Title} for {Method} {Path}: {Message}", title, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }, cancellationToken);

            return true;
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IServiceCollection AddGlobalExceptionMiddleware(this IServiceCollection services)
        {
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();
            return services;
        }

        public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseExceptionHandler();
        }
    }
}

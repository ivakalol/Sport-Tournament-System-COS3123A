using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SportTournaments.Registration.API;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller disconnected; do not turn cancellation into a business error.
        }
        catch (Exception exception)
        {
            var (status, title) = exception switch
            {
                ArgumentException or ValidationException => (400, "Invalid request"),
                KeyNotFoundException => (404, "Not found"),
                InvalidOperationException => (409, "Business rule conflict"),
                DbUpdateException => (409, "Database write conflict"),
                HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                    or TaskCanceledException => (503, "A required service is unavailable"),
                _ => (500, "Unexpected server error")
            };
            logger.LogWarning(exception, "Request failed with status {Status}", status);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status is 400 or 404 or 409 && exception is not DbUpdateException
                    ? exception.Message : "Please retry later. See the service logs for details.",
                Instance = context.Request.Path
            });
        }
    }
}

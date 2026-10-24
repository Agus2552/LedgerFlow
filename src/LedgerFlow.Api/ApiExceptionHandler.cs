using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LedgerFlow.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var databaseFailure = IsDatabaseUnavailable(exception);
        httpContext.Response.StatusCode = databaseFailure
            ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = databaseFailure ? "Database is unavailable." : "An unexpected error occurred."
            }
        });
        return true;
    }

    private static bool IsDatabaseUnavailable(Exception exception)
    {
        // EF can wrap a transient provider failure in InvalidOperationException.
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is NpgsqlException { IsTransient: true } or TimeoutException) return true;
        return false;
    }
}

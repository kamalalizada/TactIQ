using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TactIQ.API;

public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Invalid operation"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (0, string.Empty)
        };

        if (status == 0)
            return false;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
            cancellationToken);

        return true;
    }
}

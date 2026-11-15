using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Oregon.Backend.Games;

/// <summary>Maps domain exceptions to RFC 7807 problem responses with player-facing messages.</summary>
internal sealed class GameExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title)? mapped = exception switch
        {
            SlotUnavailableException => (StatusCodes.Status409Conflict, "No free save slot"),
            GameCommandRejectedException => (StatusCodes.Status409Conflict, "Command refused"),
            GameNotFoundException => (StatusCodes.Status404NotFound, "Voyage not found"),
            GameEngineUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Game engine unavailable"),
            _ => null,
        };
        if (mapped is not { } m)
        {
            return false;
        }

        httpContext.Response.StatusCode = m.status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = m.status, Title = m.title, Detail = exception.Message },
        });
    }
}

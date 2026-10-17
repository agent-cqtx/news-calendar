using NewsCalendar.Api.Models;

namespace NewsCalendar.Api.Infrastructure;

public static class Errors
{
    public static IResult BadRequest(string error, object? details = null) =>
        Results.Json(new ApiError(error, details), statusCode: StatusCodes.Status400BadRequest);
    public static IResult NotFound(string error = "Not found.") =>
        Results.Json(new ApiError(error), statusCode: StatusCodes.Status404NotFound);
    public static IResult Conflict(string error, object? details = null) =>
        Results.Json(new ApiError(error, details), statusCode: StatusCodes.Status409Conflict);
}

/// Last-resort handler so every failure still has the { error, details } shape.
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (BadHttpRequestException ex)   // malformed JSON, bad route values, etc.
        {
            await Write(ctx, ex.StatusCode is >= 400 and < 500 ? ex.StatusCode : StatusCodes.Status400BadRequest, new ApiError(ex.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Request body too large." : "Invalid request."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            await Write(ctx, StatusCodes.Status500InternalServerError, new ApiError("Internal server error."));
        }
    }

    private static Task Write(HttpContext ctx, int status, ApiError body)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(body);
    }
}

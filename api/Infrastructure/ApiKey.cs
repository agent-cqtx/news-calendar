using System.Security.Cryptography;
using System.Text;
using NewsCalendar.Api.Models;

namespace NewsCalendar.Api.Infrastructure;

/// If API_KEY is set, write endpoints require a matching X-Api-Key header. GETs stay open.
public class ApiKeyFilter(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var expected = config["API_KEY"];
        if (string.IsNullOrEmpty(expected)) return await next(ctx);

        var provided = ctx.HttpContext.Request.Headers["X-Api-Key"].ToString();
        var ok = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

        return ok ? await next(ctx)
                  : Results.Json(new ApiError("Missing or invalid API key."), statusCode: StatusCodes.Status401Unauthorized);
    }
}

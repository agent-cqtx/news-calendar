using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

namespace NewsCalendar.Api.Infrastructure;

/// Public-URL hardening: security headers, request size cap, per-client rate limits.
public static class Hardening
{
    public const int MaxBodyBytes = 256 * 1024;

    /// Config (all optional):
    ///   RateLimit__ReadPerMinute  (default 300, 0 = off)   per client IP, GET
    ///   RateLimit__WritePerMinute (default 60,  0 = off)   per client IP, POST/PUT/DELETE
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    new Models.ApiError("Too many requests. Slow down and retry shortly."), ct);
            };
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (http.Request.Path.StartsWithSegments("/health")) return RateLimitPartition.GetNoLimiter("health");
                var cfg = http.RequestServices.GetRequiredService<IConfiguration>();
                var isRead = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method);
                var limit = cfg.GetValue(isRead ? "RateLimit:ReadPerMinute" : "RateLimit:WritePerMinute", isRead ? 300 : 60);
                if (limit <= 0) return RateLimitPartition.GetNoLimiter("off");
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"{(isRead ? "r" : "w")}:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                });
            });
        });
        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";   // JSON API, never renders content
        h["Cache-Control"] = "no-store";
        await next();
    });

    public static IApplicationBuilder UseBodySizeLimit(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var feature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = MaxBodyBytes;
        await next();
    });
}

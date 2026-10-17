using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NewsCalendar.Api.Endpoints;

namespace NewsCalendar.Api.Tests;

/// A Postgres unique violation (23505) from SaveChanges must become 409 { error, details }, never 500.
public class ConflictMappingTests
{
    private static DbUpdateException Violation(string sqlState, string? constraint) =>
        new("save failed", new PostgresException("dup", "ERROR", "ERROR", sqlState, constraintName: constraint));

    private static async Task<(int Status, JsonElement Body)> Run(IResult result)
    {
        var ctx = new DefaultHttpContext { RequestServices = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(new Microsoft.Extensions.DependencyInjection.ServiceCollection())) };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteAsync(ctx);
        ctx.Response.Body.Position = 0;
        return (ctx.Response.StatusCode, JsonDocument.Parse(ctx.Response.Body).RootElement.Clone());
    }

    [Theory]
    [InlineData("ux_events_event_date_title_normalized", "title_date")]
    [InlineData("uq_events_date_title", "title_date")]
    [InlineData("ux_events_external_key", "external_key")]
    [InlineData("something_else", "unique")]
    public async Task Unique_violation_maps_to_409(string constraint, string reason)
    {
        var result = EventEndpoints.ToConflict(Violation(PostgresErrorCodes.UniqueViolation, constraint));
        Assert.NotNull(result);
        var (status, body) = await Run(result!);
        Assert.Equal(409, status);
        Assert.True(body.TryGetProperty("error", out _));
        Assert.Equal(reason, body.GetProperty("details").GetProperty("reason").GetString());
    }

    [Fact]
    public void Other_db_errors_are_not_swallowed() =>
        Assert.Null(EventEndpoints.ToConflict(Violation(PostgresErrorCodes.ForeignKeyViolation, "fk")));
}

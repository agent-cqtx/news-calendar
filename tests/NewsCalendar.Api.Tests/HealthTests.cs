using System.Net.Http.Json;
using System.Net;
using System.Text.Json;

namespace NewsCalendar.Api.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_is_200_when_db_reachable()
    {
        using var f = new ApiFactory();   // in-memory DB reports as reachable
        var res = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("up", json.GetProperty("db").GetString());
    }

    [Fact]
    public async Task Health_is_503_with_error_shape_when_db_unreachable()
    {
        // Real Npgsql provider pointed at a closed port; no database required.
        using var f = new ApiFactory(
            connectionString: "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2;Command Timeout=2",
            useInMemory: false);
        var res = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Database unreachable.", json.GetProperty("error").GetString());
        Assert.Equal("down", json.GetProperty("details").GetProperty("db").GetString());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NewsCalendar.Api.Tests;

public class HardeningTests
{
    private static object Event(string title, object? sources = null, string? description = null) =>
        new { date = "2031-05-01", title, description, sources };

    [Fact]
    public async Task Responses_carry_security_headers_and_no_server_banner()
    {
        using var f = new ApiFactory(); var http = f.CreateClient();
        var res = await http.GetAsync("/health");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(res.Headers.Contains("Content-Security-Policy"));
        Assert.False(res.Headers.Contains("X-Powered-By"));
    }

    [Fact]
    public async Task Writes_are_rate_limited_per_client_with_429_in_error_shape()
    {
        using var f = new ApiFactory(configure: new() { ["RateLimit:WritePerMinute"] = "3" }); var http = f.CreateClient();
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await http.PostAsJsonAsync("/api/events", Event($"Story {i}"))).StatusCode);
        var limited = await http.PostAsJsonAsync("/api/events", Event("Story 99"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(JsonDocument.Parse(await limited.Content.ReadAsStringAsync()).RootElement.TryGetProperty("error", out _));
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/events/month/2031-05")).StatusCode);   // reads use their own bucket
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);                      // health is exempt
    }

    [Fact]
    public async Task Rate_limit_zero_disables_limiting()
    {
        using var f = new ApiFactory(configure: new() { ["RateLimit:WritePerMinute"] = "0" }); var http = f.CreateClient();
        for (var i = 0; i < 70; i++)
            Assert.Equal(HttpStatusCode.Created, (await http.PostAsJsonAsync("/api/events", Event($"Bulk {i}"))).StatusCode);
    }

    [Fact]
    public async Task Oversized_description_and_too_many_sources_are_400()
    {
        using var f = new ApiFactory(); var http = f.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/events", Event("Big", description: new string('x', 20_001)))).StatusCode);
        var many = Enumerable.Range(0, 21).Select(i => new { url = $"https://a.com/{i}" }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/events", Event("Many", many))).StatusCode);
    }

    [Fact]
    public async Task Null_source_url_is_400_not_500()
    {
        using var f = new ApiFactory(); var http = f.CreateClient();
        var res = await http.PostAsync("/api/events", new StringContent("""{"date":"2031-05-01","title":"x","sources":[null,{"url":null}]}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Errors_do_not_leak_internals()
    {
        using var f = new ApiFactory(); var http = f.CreateClient();
        var res = await http.PostAsync("/api/events", new StringContent("{ not json", Encoding.UTF8, "application/json"));
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("LineNumber", body);
    }

    [Fact]
    public async Task Overlong_search_query_is_400()
    {
        using var f = new ApiFactory(); var http = f.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/events/search?q=" + new string('a', 201))).StatusCode);
    }
}

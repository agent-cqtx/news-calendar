using System.Net;
using System.Net.Http.Json;

namespace NewsCalendar.Api.Tests;

public class ApiKeyTests
{
    private static readonly object NewEvent = new { date = "2026-10-17", title = "Keyed" };

    [Fact]
    public async Task No_API_KEY_configured_leaves_writes_open()
    {
        using var f = new ApiFactory(apiKey: null);
        var res = await f.CreateClient().PostAsJsonAsync("/api/events", NewEvent);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Writes_without_key_are_401_with_error_shape()
    {
        using var f = new ApiFactory(apiKey: "secret");
        var res = await f.CreateClient().PostAsJsonAsync("/api/events", NewEvent);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("\"error\"", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Writes_with_wrong_key_are_401()
    {
        using var f = new ApiFactory(apiKey: "secret");
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", "nope");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/events", NewEvent)).StatusCode);
    }

    [Fact]
    public async Task Writes_with_correct_key_succeed_for_post_put_delete()
    {
        using var f = new ApiFactory(apiKey: "secret");
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", "secret");

        var created = await c.PostAsJsonAsync("/api/events", NewEvent);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("event").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/events/{id}", new { date = "2026-10-18", title = "Keyed 2" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Put_and_delete_without_key_are_401()
    {
        using var f = new ApiFactory(apiKey: "secret");
        var c = f.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PutAsJsonAsync($"/api/events/{id}", NewEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/events/day/2026-10-17")]
    [InlineData("/api/events/month/2026-10")]
    [InlineData("/health")]
    public async Task Gets_stay_open_when_key_is_set(string url)
    {
        using var f = new ApiFactory(apiKey: "secret");
        var res = await f.CreateClient().GetAsync(url);
        Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

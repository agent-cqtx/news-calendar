using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NewsCalendar.Api.Tests;

public class EventApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http;
    public EventApiTests(ApiFactory factory) => _http = factory.CreateClient();

    private static object Body(string title, string date = "2026-10-17", string? externalKey = null, params string[] urls) => new
    {
        date, title, externalKey,
        sources = urls.Select(u => new { url = u, kind = "article", label = "src" }).ToArray()
    };

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>());

    // ---- create + read ----
    [Fact]
    public async Task Create_returns_201_and_event_is_readable()
    {
        var res = await _http.PostAsJsonAsync("/api/events", Body("Launch day", "2026-11-02", "k-create", "https://example.com/launch"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await Json(res)).GetProperty("event");
        Assert.Equal("2026-11-02", created.GetProperty("date").GetString());

        var get = await _http.GetAsync($"/api/events/{created.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("Launch day", (await Json(get)).GetProperty("title").GetString());
    }

    // ---- dedupe ----
    [Fact]
    public async Task Same_externalKey_returns_200_duplicate_with_original_id()
    {
        var first = await _http.PostAsJsonAsync("/api/events", Body("Key event", "2026-09-01", "k-dup"));
        var firstId = (await Json(first)).GetProperty("event").GetProperty("id").GetGuid();

        // different title/date on purpose: externalKey wins
        var second = await _http.PostAsJsonAsync("/api/events", Body("Totally different", "2026-09-05", "k-dup"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var json = await Json(second);
        Assert.True(json.GetProperty("duplicate").GetBoolean());
        Assert.Equal(firstId, json.GetProperty("event").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Same_normalized_title_and_date_returns_409_with_existingId()
    {
        var first = await _http.PostAsJsonAsync("/api/events", Body("Fed cuts rates", "2026-12-10"));
        var firstId = (await Json(first)).GetProperty("event").GetProperty("id").GetGuid();

        var dup = await _http.PostAsJsonAsync("/api/events", Body("  FED  cuts, rates! ", "2026-12-10"));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var json = await Json(dup);
        Assert.Equal(firstId, json.GetProperty("details").GetProperty("existingId").GetGuid());
        Assert.Equal("title_date", json.GetProperty("details").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Same_title_on_a_different_date_is_allowed()
    {
        await _http.PostAsJsonAsync("/api/events", Body("Weekly review", "2026-08-01"));
        var other = await _http.PostAsJsonAsync("/api/events", Body("Weekly review", "2026-08-08"));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task Same_source_url_on_different_events_is_allowed()
    {
        var a = await _http.PostAsJsonAsync("/api/events", Body("Story A", "2026-07-01", null, "https://www.news.com/story?utm_source=x"));
        var b = await _http.PostAsJsonAsync("/api/events", Body("Story B", "2026-07-02", null, "https://news.com/story/"));
        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
    }

    [Fact]
    public async Task Duplicate_urls_within_one_event_are_collapsed()
    {
        var res = await Json(await _http.PostAsJsonAsync("/api/events",
            Body("One source", "2026-07-03", null, "https://www.news.com/s?utm_source=x", "https://news.com/s/")));
        Assert.Single(res.GetProperty("event").GetProperty("sources").EnumerateArray());
    }

    [Fact]
    public async Task Search_requires_q()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/events/search")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/events/search?q=%20")).StatusCode);
    }
    // NOTE: ranked full-text search needs real Postgres (tsvector); the InMemory provider cannot run it.

    // ---- validation / errors ----
    [Fact]
    public async Task Missing_title_and_bad_date_return_400_with_error_shape()
    {
        var res = await _http.PostAsJsonAsync("/api/events", new { title = " ", date = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var json = await Json(res);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("error").GetString()));
        Assert.True(json.GetProperty("details").TryGetProperty("title", out _));
        Assert.True(json.GetProperty("details").TryGetProperty("date", out _));
    }

    [Fact]
    public async Task Title_over_200_chars_is_rejected()
    {
        var res = await _http.PostAsJsonAsync("/api/events", Body(new string('a', 201)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_400_in_error_shape()
    {
        var res = await _http.PostAsync("/api/events", new StringContent("{ nope", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True((await Json(res)).TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        var res = await _http.GetAsync($"/api/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.True((await Json(res)).TryGetProperty("error", out _));
    }

    // ---- date parsing at the HTTP edge ----
    [Theory]
    [InlineData("/api/events/day/2026-10-17", HttpStatusCode.OK)]
    [InlineData("/api/events/day/10-17-2026", HttpStatusCode.BadRequest)]
    [InlineData("/api/events/month/2026-10", HttpStatusCode.OK)]
    [InlineData("/api/events/month/2026-13", HttpStatusCode.BadRequest)]
    public async Task Date_routes_validate_format(string url, HttpStatusCode expected) =>
        Assert.Equal(expected, (await _http.GetAsync(url)).StatusCode);

    [Fact]
    public async Task Month_lists_only_that_month_sorted_by_date()
    {
        await _http.PostAsJsonAsync("/api/events", Body("Late oct", "2030-10-30"));
        await _http.PostAsJsonAsync("/api/events", Body("Early oct", "2030-10-02"));
        await _http.PostAsJsonAsync("/api/events", Body("Nov", "2030-11-01"));

        var list = await Json(await _http.GetAsync("/api/events/month/2030-10"));
        var titles = list.EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToArray();
        Assert.Equal(new[] { "Early oct", "Late oct" }, titles);
    }

    [Fact]
    public async Task All_lists_events_from_more_than_one_month_newest_first()
    {
        await _http.PostAsJsonAsync("/api/events", Body("All nov event", "2030-11-28"));
        await _http.PostAsJsonAsync("/api/events", Body("All jan event", "2031-01-02"));
        await _http.PostAsJsonAsync("/api/events", Body("All mar event", "2031-03-15"));

        // limit=200 (the max) so the whole fixture is on one page and the ordering
        // is asserted globally; the default page is capped at 50 (see pagination tests).
        var list = await Json(await _http.GetAsync("/api/events/all?limit=200"));
        var items = list.EnumerateArray()
            .Select(e => (date: e.GetProperty("date").GetString(), title: e.GetProperty("title").GetString()))
            .ToArray();

        // /all spans months — a single /month response could never do this.
        Assert.Contains("2030-11", items.Select(i => i.date!.Substring(0, 7)));
        Assert.Contains("2031-01", items.Select(i => i.date!.Substring(0, 7)));
        Assert.Contains("2031-03", items.Select(i => i.date!.Substring(0, 7)));

        // whole response is ordered date DESC (newest first), then title ASC within a date
        Assert.Equal(items.OrderByDescending(i => i.date).ThenBy(i => i.title), items);

        // our three appear newest-first
        var ours = items.Where(i => i.title!.StartsWith("All ")).Select(i => i.date).ToArray();
        Assert.Equal(new[] { "2031-03-15", "2031-01-02", "2030-11-28" }, ours);
    }

    // ---- /all pagination ----
    /// Seeding uses a DEDICATED ApiFactory: each factory boots its own app, so it
    /// gets a fresh in-memory DB and a fresh write-rate-limit budget (60/min per
    /// instance — 55 seed POSTs on the shared class fixture would 429 sibling tests).
    /// Far-future dates so the seeds are the newest rows under date DESC.
    private static async Task SeedPagingEvents(HttpClient http)
    {
        for (var i = 1; i <= 55; i++)
        {
            var res = await http.PostAsJsonAsync("/api/events",
                Body($"Pag ev {i:D2}", new DateOnly(2040, 1, 1).AddDays(i).ToString("yyyy-MM-dd")));
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }
    }

    [Fact]
    public async Task All_default_page_caps_at_50_newest_first()
    {
        using var factory = new ApiFactory();
        var http = factory.CreateClient();
        await SeedPagingEvents(http); // 55 events, nothing else in this DB

        var res = await http.GetAsync("/api/events/all");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var titles = (await Json(res)).EnumerateArray()
            .Select(e => e.GetProperty("title").GetString()).ToArray();

        Assert.Equal(50, titles.Length);                          // default limit is 50
        Assert.All(titles, t => Assert.StartsWith("Pag ev ", t!)); // newest 50 of our 55
        Assert.Equal(new[] { "Pag ev 55", "Pag ev 54", "Pag ev 53" }, titles.Take(3)); // DESC
    }

    [Fact]
    public async Task All_offset_returns_next_window_without_overlap()
    {
        using var factory = new ApiFactory();
        var http = factory.CreateClient();
        await SeedPagingEvents(http); // 55 events: page 1 = ev 55..06, page 2 = ev 05..01

        var page1 = (await Json(await http.GetAsync("/api/events/all?limit=50&offset=0")))
            .EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToArray();
        var page2 = (await Json(await http.GetAsync("/api/events/all?limit=50&offset=50")))
            .EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToArray();

        Assert.Equal(50, page1.Length);
        // DESC continues exactly where page 1 stopped: no overlap, no gap
        Assert.Equal(new[] { "Pag ev 05", "Pag ev 04", "Pag ev 03", "Pag ev 02", "Pag ev 01" }, page2);
        Assert.Empty(page1.Intersect(page2));
    }

    [Fact]
    public async Task All_offset_past_the_end_returns_empty_200()
    {
        var res = await _http.GetAsync("/api/events/all?limit=50&offset=100000");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Empty((await Json(res)).EnumerateArray());
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=abc", "limit")]
    [InlineData("limit=201", "limit")]
    [InlineData("offset=-1", "offset")]
    [InlineData("offset=abc", "offset")]
    public async Task All_invalid_pagination_param_returns_400_with_field_details(string qs, string field)
    {
        var res = await _http.GetAsync($"/api/events/all?{qs}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True((await Json(res)).GetProperty("details").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("limit=1")]
    [InlineData("limit=200")]   // allowed maximum
    [InlineData("offset=0")]
    public async Task All_accepts_pagination_params_within_bounds(string qs) =>
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync($"/api/events/all?{qs}")).StatusCode);

    // ---- updates + delete ----
    [Fact]
    public async Task Add_update_then_delete_event()
    {
        var created = await Json(await _http.PostAsJsonAsync("/api/events", Body("With updates", "2031-01-05")));
        var id = created.GetProperty("event").GetProperty("id").GetGuid();

        var upd = await _http.PostAsJsonAsync($"/api/events/{id}/updates", new { date = "2031-01-06", text = "Follow-up", showOnCalendar = true });
        Assert.Equal(HttpStatusCode.Created, upd.StatusCode);
        Assert.True((await Json(upd)).GetProperty("showOnCalendar").GetBoolean());

        var detail = await Json(await _http.GetAsync($"/api/events/{id}"));
        Assert.Single(detail.GetProperty("updates").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Update_without_date_defaults_to_the_events_own_date()
    {
        var created = await Json(await _http.PostAsJsonAsync("/api/events", Body("Default date", "2031-03-09")));
        var id = created.GetProperty("event").GetProperty("id").GetGuid();
        var res = await _http.PostAsJsonAsync($"/api/events/{id}/updates", new { text = "No date given" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("2031-03-09", (await Json(res)).GetProperty("date").GetString());
    }

    [Fact]
    public async Task Update_without_text_returns_400()
    {
        var created = await Json(await _http.PostAsJsonAsync("/api/events", Body("Needs text", "2031-02-01")));
        var id = created.GetProperty("event").GetProperty("id").GetGuid();
        var res = await _http.PostAsJsonAsync($"/api/events/{id}/updates", new { text = " " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Put_conflicting_title_and_date_returns_409()
    {
        await _http.PostAsJsonAsync("/api/events", Body("Taken", "2032-03-03"));
        var other = await Json(await _http.PostAsJsonAsync("/api/events", Body("Other", "2032-03-04")));
        var id = other.GetProperty("event").GetProperty("id").GetGuid();

        var res = await _http.PutAsJsonAsync($"/api/events/{id}", new { date = "2032-03-03", title = "taken" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}

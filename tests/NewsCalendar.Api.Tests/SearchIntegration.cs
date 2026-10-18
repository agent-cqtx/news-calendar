using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NewsCalendar.Api.Data;
using Testcontainers.PostgreSql;

namespace NewsCalendar.Api.Tests;

/// Search integration tests against real Postgres (Testcontainers).
/// The in-memory EF provider cannot map tsvector — these tests exercise the actual
/// @@ websearch_to_tsquery / to_tsquery path including prefix matching.
public class SearchIntegration : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private ApiFactory _factory = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Apply the canonical schema (same DDL as db/init/001_schema.sql — idempotent).
        await using (var conn = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = Schema;
            await cmd.ExecuteNonQueryAsync();
        }

        // Factory with real Postgres (no in-memory swap).
        _factory = new ApiFactory(connectionString: _pg.GetConnectionString(), useInMemory: false);
        _http = _factory.CreateClient();

        // Seed test events via the API so normalisation + tsvector generation run.
        await Post("SpaceX Starship Flight Test", "2026-10-12", "Next Starship test flight from Starbase.");
        await Post("Fed Interest Rate Decision", "2026-10-07", "FOMC holds rates steady.");
        await Post("PostgreSQL 19 Beta Announced", "2026-11-12", "Community beta with new features.");
        await Post("Apple September Event Recap", "2026-10-01", "New iPhone lineup announced.");
    }

    public async Task DisposeAsync()
    {
        _http?.Dispose();
        _factory?.Dispose();
        await _pg.DisposeAsync();
    }

    // ── helpers ──────────────────────────────────────────────

    private async Task Post(string title, string date, string? summary = null)
    {
        var res = await _http.PostAsJsonAsync("/api/events",
            new { date, title, summary });
        res.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement[]> Search(string q)
    {
        var res = await _http.GetAsync(
            $"/api/events/search?q={Uri.EscapeDataString(q)}&limit=10");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement[]>()
            ?? throw new InvalidOperationException("search response null");
    }

    // ── tests ────────────────────────────────────────────────

    [Fact]
    public async Task Exact_lexeme_match()
    {
        var r = await Search("starship");
        Assert.Single(r);
        Assert.Contains("SpaceX", r[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Prefix_match_finds_compound_name()
    {
        // "space" should find "SpaceX" via prefix matching (space:*)
        // This was the bug: before the fix, 'space' ≠ 'spacex' in PG tsquery.
        var r = await Search("space");
        Assert.NotEmpty(r);
        Assert.Contains(r, e =>
            e.GetProperty("title").GetString().Contains("SpaceX"));
    }

    [Fact]
    public async Task Prefix_match_on_stemmed_word()
    {
        // "announce" should stem-match "Announced" (both in PostgreSQL title and Apple summary)
        var r = await Search("announce");
        Assert.True(r.Length >= 1);
        Assert.Contains(r, e =>
            e.GetProperty("title").GetString().Contains("PostgreSQL"));
    }

    [Fact]
    public async Task Multi_word_AND()
    {
        var r = await Search("postgresql beta");
        Assert.Single(r);
        Assert.Contains("PostgreSQL", r[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Or_operator()
    {
        // "or" should be converted to | in the tsquery
        var r = await Search("space or apple");
        Assert.Equal(2, r.Length);
    }

    [Fact]
    public async Task Missing_query_returns_400()
    {
        var res = await _http.GetAsync("/api/events/search");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
        res = await _http.GetAsync("/api/events/search?q=%20");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Query_too_long_returns_400()
    {
        var res = await _http.GetAsync("/api/events/search?q=" + new string('x', 201));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── schema DDL (same as db/init/001_schema.sql — idempotent) ──

    private const string Schema = """
CREATE TABLE IF NOT EXISTS events (
    id               uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    title            varchar(200) NOT NULL,
    summary          varchar(500),
    description      text,
    event_date       date         NOT NULL,
    external_key     varchar(200),
    title_normalized varchar(200) NOT NULL,
    created_at       timestamptz  NOT NULL DEFAULT now(),
    updated_at       timestamptz  NOT NULL DEFAULT now(),
    search_vector    tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(title, '')),       'A') ||
        setweight(to_tsvector('english', coalesce(summary, '')),     'B') ||
        setweight(to_tsvector('english', coalesce(description, '')), 'C')
    ) STORED,
    CONSTRAINT ck_events_title_not_blank CHECK (length(btrim(title)) > 0)
);

CREATE TABLE IF NOT EXISTS event_updates (
    id               uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id         uuid        NOT NULL REFERENCES events(id) ON DELETE CASCADE,
    update_date      date        NOT NULL,
    text             text        NOT NULL,
    show_on_calendar boolean     NOT NULL DEFAULT false,
    created_at       timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS event_sources (
    id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id       uuid        NOT NULL REFERENCES events(id) ON DELETE CASCADE,
    update_id      uuid        REFERENCES event_updates(id) ON DELETE SET NULL,
    url            text        NOT NULL,
    url_normalized text        NOT NULL,
    kind           varchar(30),
    label          varchar(200)
);

CREATE INDEX IF NOT EXISTS ix_events_event_date ON events (event_date);
CREATE UNIQUE INDEX IF NOT EXISTS ux_events_external_key
    ON events (external_key) WHERE external_key IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_events_date_title_norm
    ON events (event_date, title_normalized);
CREATE INDEX IF NOT EXISTS ix_events_search_vector
    ON events USING gin (search_vector);

CREATE INDEX IF NOT EXISTS ix_event_sources_url_normalized ON event_sources (url_normalized);
CREATE INDEX IF NOT EXISTS ix_event_sources_event_id       ON event_sources (event_id);
CREATE INDEX IF NOT EXISTS ix_event_updates_event_id       ON event_updates (event_id);
CREATE INDEX IF NOT EXISTS ix_event_updates_calendar
    ON event_updates (update_date) WHERE show_on_calendar;

CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_events_updated_at ON events;
CREATE TRIGGER trg_events_updated_at
    BEFORE UPDATE ON events
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
""";
}
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NewsCalendar.Api.Data;
using NewsCalendar.Api.Infrastructure;
using NewsCalendar.Api.Models;

namespace NewsCalendar.Api.Endpoints;

public static class EventEndpoints
{
    private const int MaxTitle = 200, MaxSummary = 500, MaxExternalKey = 200,
        MaxDescription = 20_000, MaxSources = 20, MaxUrl = 2000, MaxKind = 50, MaxLabel = 200, MaxUpdateText = 5000, MaxQuery = 200;

    public static void MapEventEndpoints(this WebApplication app)
    {
        var read = app.MapGroup("/api/events");
        var write = app.MapGroup("/api/events").AddEndpointFilter<ApiKeyFilter>();

        // ---- reads (open) ----
        read.MapGet("/month/{month}", async (string month, AppDbContext db, CancellationToken ct) =>
        {
            if (!DateParsing.TryMonth(month, out var first)) return Errors.BadRequest("Month must be yyyy-MM.");
            var next = first.AddMonths(1);
            var items = await db.Events.AsNoTracking()
                .Where(e => e.EventDate >= first && e.EventDate < next)
                .OrderBy(e => e.EventDate).ThenBy(e => e.Title)
                .Select(e => new EventSummaryDto(e.Id, e.EventDate, e.Title, e.Summary))
                .ToListAsync(ct);
            return Results.Ok(items);
        });

        read.MapGet("/day/{date}", async (string date, AppDbContext db, CancellationToken ct) =>
        {
            if (!DateParsing.TryDay(date, out var d)) return Errors.BadRequest("Date must be yyyy-MM-dd.");
            var events = await db.Events.AsNoTracking()
                .Include(e => e.Sources).Include(e => e.Updates)
                .Where(e => e.EventDate == d)
                .OrderBy(e => e.Title)
                .ToListAsync(ct);
            return Results.Ok(events.Select(ToDto));
        });

        read.MapGet("/search", async (string? q, int? limit, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Errors.BadRequest("Query parameter 'q' is required.");
            if (q.Length > MaxQuery) return Errors.BadRequest($"Query must be at most {MaxQuery} characters.");
            var take = Math.Clamp(limit ?? 25, 1, 100);
            // Prefix matching ("space" → "space:*") so partial-word queries match compound names (SpaceX).
            var tsQuery = BuildTsQuery(q);
            var items = await db.Events.AsNoTracking()
                .Where(e => e.SearchVector.Matches(EF.Functions.ToTsQuery("english", tsQuery)))
                .OrderByDescending(e => e.SearchVector.Rank(EF.Functions.ToTsQuery("english", tsQuery)))
                .ThenByDescending(e => e.EventDate)
                .Take(take)
                .Select(e => new EventSummaryDto(e.Id, e.EventDate, e.Title, e.Summary))
                .ToListAsync(ct);
            return Results.Ok(items);
        });

        read.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var e = await db.Events.AsNoTracking().Include(x => x.Sources).Include(x => x.Updates)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return e is null ? Errors.NotFound("Event not found.") : Results.Ok(ToDto(e));
        });

        // ---- writes (API key when API_KEY is set) ----
        write.MapPost("/", async (CreateEventRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var (errors, sources) = Validate(req.Date, req.Title, req.Summary, req.ExternalKey, req.Sources, req.Description);
            if (errors.Count > 0) return Errors.BadRequest("Validation failed.", errors);

            var titleNorm = Normalize.Title(req.Title);
            var date = req.Date!.Value;
            var externalKey = Clean(req.ExternalKey);

            // 1) externalKey match -> 200 duplicate (idempotent re-import)
            if (externalKey is not null)
            {
                var existing = await db.Events.Include(x => x.Sources).Include(x => x.Updates)
                    .FirstOrDefaultAsync(x => x.ExternalKey == externalKey, ct);
                if (existing is not null) return Results.Ok(new CreateEventResponse(ToDto(existing), Duplicate: true));
            }

            // 2) same normalized title + date -> 409
            var titleDup = await db.Events.AsNoTracking()
                .Where(x => x.EventDate == date && x.TitleNormalized == titleNorm)
                .Select(x => x.Id).FirstOrDefaultAsync(ct);
            if (titleDup != Guid.Empty)
                return Errors.Conflict("An event with the same title and date already exists.", new { existingId = titleDup, reason = "title_date" });

            // Source URLs are NOT globally unique: one article can back several events.
            // (Duplicate URLs inside one request are collapsed in Validate.)

            var now = DateTime.UtcNow;
            var entity = new Event
            {
                Id = Guid.NewGuid(), EventDate = date, Title = req.Title!.Trim(), TitleNormalized = titleNorm,
                Summary = Clean(req.Summary), Description = Clean(req.Description), ExternalKey = externalKey,
                CreatedAt = now, UpdatedAt = now
            };
            foreach (var s in sources) { s.EventId = entity.Id; entity.Sources.Add(s); }
            db.Events.Add(entity);
            return await Save(db, ct, () => Results.Created($"/api/events/{entity.Id}", new CreateEventResponse(ToDto(entity), false)));
        });

        write.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Events.Include(x => x.Sources).Include(x => x.Updates).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Errors.NotFound("Event not found.");

            var (errors, sources) = Validate(req.Date, req.Title, req.Summary, null, req.Sources);
            if (errors.Count > 0) return Errors.BadRequest("Validation failed.", errors);

            var titleNorm = Normalize.Title(req.Title);
            var date = req.Date!.Value;

            var titleDup = await db.Events.AsNoTracking()
                .Where(x => x.Id != id && x.EventDate == date && x.TitleNormalized == titleNorm)
                .Select(x => x.Id).FirstOrDefaultAsync(ct);
            if (titleDup != Guid.Empty)
                return Errors.Conflict("Another event with the same title and date already exists.", new { existingId = titleDup, reason = "title_date" });

            entity.EventDate = date;
            entity.Title = req.Title!.Trim();
            entity.TitleNormalized = titleNorm;
            entity.Summary = Clean(req.Summary);
            entity.Description = Clean(req.Description);
            entity.UpdatedAt = DateTime.UtcNow;

            if (req.Sources is not null)   // null = leave sources alone; [] = remove all event-level sources
            {
                var eventLevel = entity.Sources.Where(s => s.UpdateId is null).ToList(); // update-level sources are kept
                db.EventSources.RemoveRange(eventLevel);
                foreach (var s in eventLevel) entity.Sources.Remove(s);
                foreach (var s in sources) { s.EventId = id; entity.Sources.Add(s); }
            }
            return await Save(db, ct, () => Results.Ok(ToDto(entity)));
        });

        write.MapPost("/{id:guid}/updates", async (Guid id, CreateUpdateRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return Errors.BadRequest("Validation failed.", new Dictionary<string, string> { ["text"] = "Text is required." });
            if (req.Text.Trim().Length > MaxUpdateText)
                return Errors.BadRequest("Validation failed.", new Dictionary<string, string> { ["text"] = $"Text must be at most {MaxUpdateText} characters." });
            var ev = await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (ev is null) return Errors.NotFound("Event not found.");

            var now = DateTime.UtcNow;
            var update = new EventUpdate
            {
                Id = Guid.NewGuid(), EventId = id, Text = req.Text.Trim(),
                UpdateDate = req.Date ?? ev.EventDate,   // no UTC "today": default to the event's own date
                ShowOnCalendar = req.ShowOnCalendar ?? false, CreatedAt = now
            };
            db.EventUpdates.Add(update);
            ev.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/events/{id}", ToDto(update));
        });

        write.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var ev = await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (ev is null) return Errors.NotFound("Event not found.");
            db.Events.Remove(ev); // sources/updates cascade
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    // ---- helpers ----
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// Builds a tsquery string with prefix matching on every token.
    /// "space" → "space:*"  so partial-word queries match compound names (SpaceX, Starship, etc.).
    /// Preserves websearch operators: "or" → |,  "-word" → !word:*,  multiple words joined with &amp;.
    private static string BuildTsQuery(string q)
    {
        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>();
        var nextIsOr = false;
        foreach (var t in tokens)
        {
            if (t.Equals("or", StringComparison.OrdinalIgnoreCase))
            {
                nextIsOr = true;
                continue;
            }
            if (parts.Count > 0) parts.Add(nextIsOr ? "|" : "&");
            nextIsOr = false;
            if (t.StartsWith("-") && t.Length > 1)
                parts.Add("!" + t[1..] + ":*");
            else
                parts.Add(t + ":*");
        }
        return string.Join(" ", parts);
    }

    private static (Dictionary<string, string> errors, List<EventSource> sources) Validate(
        DateOnly? date, string? title, string? summary, string? externalKey, List<SourceInput>? input, string? description = null)
    {
        var errors = new Dictionary<string, string>();
        if (date is null) errors["date"] = "Date is required (yyyy-MM-dd).";
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = "Title is required.";
        else if (title.Trim().Length > MaxTitle) errors["title"] = $"Title must be at most {MaxTitle} characters.";
        else if (Normalize.Title(title).Length == 0) errors["title"] = "Title must contain letters or digits.";
        if (summary is not null && summary.Trim().Length > MaxSummary) errors["summary"] = $"Summary must be at most {MaxSummary} characters.";
        if (externalKey is not null && externalKey.Trim().Length > MaxExternalKey) errors["externalKey"] = $"externalKey must be at most {MaxExternalKey} characters.";

        if (description is not null && description.Length > MaxDescription) errors["description"] = $"Description must be at most {MaxDescription} characters.";
        if (input is { Count: > MaxSources }) { errors["sources"] = $"At most {MaxSources} sources per event."; return (errors, []); }

        var sources = new List<EventSource>();
        foreach (var (s, i) in (input ?? []).Select((s, i) => (s, i)))
        {
            if (s is null || string.IsNullOrWhiteSpace(s.Url)) { errors[$"sources[{i}].url"] = "URL is required."; continue; }
            if (s.Url.Trim().Length > MaxUrl) { errors[$"sources[{i}].url"] = $"URL must be at most {MaxUrl} characters."; continue; }
            if (s.Kind is { } k && k.Trim().Length > MaxKind) { errors[$"sources[{i}].kind"] = $"Kind must be at most {MaxKind} characters."; continue; }
            if (s.Label is { } l && l.Trim().Length > MaxLabel) { errors[$"sources[{i}].label"] = $"Label must be at most {MaxLabel} characters."; continue; }
            var norm = Normalize.Url(s.Url);
            if (norm is null) { errors[$"sources[{i}].url"] = "Must be an absolute http(s) URL."; continue; }
            if (sources.Any(x => x.UrlNormalized == norm)) continue; // drop in-request duplicates
            sources.Add(new EventSource { Id = Guid.NewGuid(), Url = s.Url.Trim(), UrlNormalized = norm, Kind = Clean(s.Kind) ?? "link", Label = Clean(s.Label) });
        }
        return (errors, sources);
    }

    // Race safety net: any Postgres unique violation (external_key index, unique(event_date, title_normalized), ...) is a 409, never a 500.
    private static async Task<IResult> Save(AppDbContext db, CancellationToken ct, Func<IResult> ok)
    {
        try { await db.SaveChangesAsync(ct); return ok(); }
        catch (DbUpdateException ex) when (ToConflict(ex) is { } conflict) { return conflict; }
    }

    public static IResult? ToConflict(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg) return null;
        var name = pg.ConstraintName ?? "";
        var reason = name.Contains("external", StringComparison.OrdinalIgnoreCase) ? "external_key"
                   : name.Contains("title", StringComparison.OrdinalIgnoreCase) || name.Contains("date", StringComparison.OrdinalIgnoreCase) ? "title_date"
                   : "unique";
        var message = reason == "title_date" ? "Another event with the same title and date already exists." : "Duplicate event.";
        return Errors.Conflict(message, new { reason, constraint = pg.ConstraintName });
    }

    private static UpdateDto ToDto(EventUpdate u) => new(u.Id, u.UpdateDate, u.Text, u.ShowOnCalendar, u.CreatedAt);

    private static EventDto ToDto(Event e) => new(
        e.Id, e.EventDate, e.Title, e.Summary, e.Description, e.ExternalKey, e.CreatedAt, e.UpdatedAt,
        e.Sources.Select(s => new SourceDto(s.Id, s.UpdateId, s.Url, s.Kind, s.Label)).ToList(),
        e.Updates.OrderByDescending(u => u.UpdateDate).ThenByDescending(u => u.CreatedAt).Select(ToDto).ToList());
}

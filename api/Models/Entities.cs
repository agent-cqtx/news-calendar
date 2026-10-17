using NpgsqlTypes;

namespace NewsCalendar.Api.Models;

public class Event
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";              // varchar(200)
    public string TitleNormalized { get; set; } = "";    // varchar(200), set by API
    public string? Summary { get; set; }                 // varchar(500)
    public string? Description { get; set; }             // text
    public DateOnly EventDate { get; set; }              // date
    public string? ExternalKey { get; set; }             // varchar(200), unique where not null
    public DateTime CreatedAt { get; set; }              // timestamptz (UTC)
    public DateTime UpdatedAt { get; set; }              // timestamptz (UTC)
    public NpgsqlTsVector SearchVector { get; set; } = null!; // GENERATED, read-only
    public List<EventSource> Sources { get; set; } = [];
    public List<EventUpdate> Updates { get; set; } = [];
}

public class EventSource
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? UpdateId { get; set; }                  // null = source of the event itself
    public string Url { get; set; } = "";
    public string UrlNormalized { get; set; } = "";      // set by API
    public string? Kind { get; set; }
    public string? Label { get; set; }
}

public class EventUpdate
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateOnly UpdateDate { get; set; }             // date
    public string Text { get; set; } = "";
    public bool ShowOnCalendar { get; set; }             // default false
    public DateTime CreatedAt { get; set; }
}

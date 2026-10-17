namespace NewsCalendar.Api.Models;

// DateOnly serializes as "yyyy-MM-dd" in System.Text.Json (.NET 8).
public record SourceInput(string Url, string? Kind, string? Label);
public record CreateEventRequest(DateOnly? Date, string? Title, string? Summary, string? Description,
                                 string? ExternalKey, List<SourceInput>? Sources);
public record UpdateEventRequest(DateOnly? Date, string? Title, string? Summary, string? Description,
                                 List<SourceInput>? Sources);
public record CreateUpdateRequest(DateOnly? Date, string? Text, bool? ShowOnCalendar);

public record SourceDto(Guid Id, Guid? UpdateId, string Url, string? Kind, string? Label);
public record UpdateDto(Guid Id, DateOnly Date, string Text, bool ShowOnCalendar, DateTime CreatedAt);
public record EventSummaryDto(Guid Id, DateOnly Date, string Title, string? Summary);
public record EventDto(Guid Id, DateOnly Date, string Title, string? Summary, string? Description,
                       string? ExternalKey, DateTime CreatedAt, DateTime UpdatedAt,
                       List<SourceDto> Sources, List<UpdateDto> Updates);
public record CreateEventResponse(EventDto Event, bool Duplicate);

public record ApiError(string Error, object? Details = null);

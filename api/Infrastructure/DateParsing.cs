using System.Globalization;

namespace NewsCalendar.Api.Infrastructure;

/// Strict, culture-invariant parsing for the API's plain date formats.
public static class DateParsing
{
    /// "2026-10-17" only (no times, no other separators).
    public static bool TryDay(string? s, out DateOnly day) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// "2026-10" -> first day of that month.
    public static bool TryMonth(string? s, out DateOnly firstOfMonth)
    {
        firstOfMonth = default;
        return s is { Length: 7 } && TryDay(s + "-01", out firstOfMonth);
    }
}

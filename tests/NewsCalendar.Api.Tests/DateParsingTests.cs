using NewsCalendar.Api.Infrastructure;

namespace NewsCalendar.Api.Tests;

public class DateParsingTests
{
    [Fact]
    public void TryDay_accepts_plain_date()
    {
        Assert.True(DateParsing.TryDay("2026-10-17", out var d));
        Assert.Equal(new DateOnly(2026, 10, 17), d);
    }

    [Theory]
    [InlineData("2026-10-17T00:00:00")]
    [InlineData("10/17/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    [InlineData("2026-1-5")]
    [InlineData("")]
    [InlineData(null)]
    public void TryDay_rejects_everything_else(string? s) => Assert.False(DateParsing.TryDay(s, out _));

    [Fact]
    public void TryMonth_returns_first_of_month()
    {
        Assert.True(DateParsing.TryMonth("2026-02", out var d));
        Assert.Equal(new DateOnly(2026, 2, 1), d);
    }

    [Theory]
    [InlineData("2026-2")]
    [InlineData("2026-13")]
    [InlineData("2026-02-01")]
    [InlineData("abcd-ef")]
    [InlineData(null)]
    public void TryMonth_rejects_bad_input(string? s) => Assert.False(DateParsing.TryMonth(s, out _));
}

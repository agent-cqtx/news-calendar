using NewsCalendar.Api.Infrastructure;

namespace NewsCalendar.Api.Tests;

public class NormalizeTests
{
    [Theory]
    [InlineData("  The  Fed's Decision! ", "the feds decision")]
    [InlineData("Café—Résumé", "cafe resume")]
    [InlineData("Rate-Cut: Q3 2026", "rate cut q3 2026")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("!!!", "")]
    public void Title_normalizes(string? input, string expected) =>
        Assert.Equal(expected, Normalize.Title(input));

    [Fact]
    public void Title_variants_collide() =>
        Assert.Equal(Normalize.Title("Fed cuts rates"), Normalize.Title("  FED  cuts, rates! "));

    [Theory]
    [InlineData("HTTPS://WWW.Example.com/News/", "https://example.com/News")]
    [InlineData("https://example.com/a?utm_source=x&b=2&a=1#frag", "https://example.com/a?a=1&b=2")]
    [InlineData("https://example.com:443/a", "https://example.com/a")]
    [InlineData("http://example.com:8080/a", "http://example.com:8080/a")]
    [InlineData("https://example.com/a?fbclid=abc", "https://example.com/a")]
    public void Url_normalizes(string input, string expected) =>
        Assert.Equal(expected, Normalize.Url(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("/relative/path")]
    public void Url_rejects_invalid(string? input) => Assert.Null(Normalize.Url(input));
}

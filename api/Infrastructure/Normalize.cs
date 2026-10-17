using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NewsCalendar.Api.Infrastructure;

public static partial class Normalize
{
    /// "  The  Fed's Decision! " -> "the feds decision"  (lowercase, no accents/punctuation, single spaces)
    public static string Title(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var sb = new StringBuilder();
        foreach (var ch in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue; // strip accents
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (char.IsWhiteSpace(ch) || ch is '-' or '–' or '—') sb.Append(' ');
            // other punctuation (apostrophes etc.) is dropped
        }
        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    /// Lowercase scheme/host, drop "www.", default port, fragment, tracking params, trailing slash; sort query.
    /// Returns null if the URL isn't an absolute http(s) URL.
    public static string? Url(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var path = uri.AbsolutePath.TrimEnd('/');

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p =>
            {
                var key = p.Split('=')[0].ToLowerInvariant();
                return !key.StartsWith("utm_") && key is not ("fbclid" or "gclid" or "ref" or "mc_cid" or "mc_eid");
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        return $"{uri.Scheme}://{host}{port}{path}{(query.Length > 0 ? "?" + string.Join('&', query) : "")}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

# Normalization

The API computes normalized forms so duplicates are caught even when text differs slightly. Code: `api/Infrastructure/Normalize.cs`. The original text is always stored and shown; the normalized form is only for comparing.

## Titles

Lowercase, accents removed, punctuation dropped (apostrophes disappear, hyphens and dashes become spaces), runs of spaces collapsed, trimmed.

`"  The  Fed's Decision! "` becomes `the feds decision`. Two events on the same date with the same normalized title conflict (`409`). A title with no letters or digits is rejected.

## URLs

Only absolute `http` and `https` URLs are accepted. Then:
- scheme and host lowercased, a leading `www.` removed
- default port (80/443) removed
- fragment (`#...`) removed
- trailing slash removed from the path
- tracking parameters removed: `utm_*`, `fbclid`, `gclid`, `ref`, `mc_cid`, `mc_eid`
- remaining query parameters sorted

`https://www.Example.com/story/?utm_source=x&b=2&a=1#top` becomes `https://example.com/story?a=1&b=2`.

A source URL that matches one already on a different event is a `409` (`reason: "url"`).

## Dates

Plain `yyyy-MM-dd` only. No times, no other separators. Months are `yyyy-MM`. Parsing ignores the server's culture.

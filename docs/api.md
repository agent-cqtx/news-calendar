# API reference

Base path `/api/events` (through the web proxy at `http://localhost:8088`, or the API container's port 8080 on the Compose network). JSON in and out. Dates are `yyyy-MM-dd`; months are `yyyy-MM`.

Writes need header `X-Api-Key: <key>` only when `API_KEY` is set. Without it, writes are open. A wrong key returns `401 {"error":"Missing or invalid API key."}`.

Errors: `{"error": "message", "details": ...}` (details is optional).

## Reads

| Method and path | Returns |
|---|---|
| `GET /health` | `200 {"status":"ok","db":"up"}`, or `503` when the database is unreachable |
| `GET /api/events/month/{yyyy-MM}` | `EventSummary[]`, ordered by date then title. Bad month: `400` |
| `GET /api/events/day/{yyyy-MM-dd}` | `Event[]` with sources and updates, ordered by title. Bad date: `400` |
| `GET /api/events/search?q=...&limit=25` | `EventSummary[]`, best match first, then newest. `q` required (`400` if blank). `limit` is 1 to 100. Uses web-style search: quoted phrases, `-exclude`, `or` |
| `GET /api/events/{id}` | one `Event`, or `404` |

## Writes

| Method and path | Body | Result |
|---|---|---|
| `POST /api/events` | `{date, title, summary?, description?, externalKey?, sources?[]}` | `201 {event, duplicate:false}`; `200 {event, duplicate:true}` if the `externalKey` already exists; `400` validation; `409` duplicate |
| `PUT /api/events/{id}` | `{date, title, summary?, description?, sources?[]}` | `200 Event`; `404`; `400`; `409` |
| `POST /api/events/{id}/updates` | `{date?, text, showOnCalendar?}` | `201` + the created update (`date` defaults to the event's own date); `404`; `400` (text required) |
| `DELETE /api/events/{id}` | none | `204`; `404`. Removes its updates and sources too |

A source is `{url, kind?, label?}`. `kind` is free text and defaults to `link`.

## Shapes

```jsonc
// EventSummary
{ "id": "guid", "date": "2026-10-15", "title": "...", "summary": "..." }

// Event
{ "id": "guid", "date": "2026-10-15", "title": "...", "summary": "...", "description": "...",
  "externalKey": "seed-x", "createdAt": "ISO-8601", "updatedAt": "ISO-8601",
  "sources": [{ "id": "guid", "updateId": null, "url": "...", "kind": "link", "label": "..." }],
  "updates": [{ "id": "guid", "date": "2026-10-16", "text": "...", "showOnCalendar": false, "createdAt": "ISO-8601" }] }
```

## Validation

- `date` required and valid.
- `title` required, at most 200 characters, and must contain a letter or digit.
- `summary` at most 500 characters. `externalKey` at most 200.
- Every source `url` must be an absolute `http` or `https` URL. Repeats of the same URL inside one request are dropped.

Field errors come back as `400 {"error":"Validation failed.","details":{"title":"Title is required."}}`.

## Duplicate rules (create)

Checked in this order:

1. Same `externalKey`: not an error. Returns `200` with the existing event and `duplicate: true`, so re-importing is safe.
2. Same normalized title on the same date: `409`, `details.reason = "title_date"`, plus `existingId`.
3. A source URL (normalized) already on another event: `409`, `details.reason = "url"`, plus `existingId`.

The database enforces the same unique rules, so two simultaneous requests also end in `409`, never a `500`. See [normalization.md](normalization.md) for what counts as "same".

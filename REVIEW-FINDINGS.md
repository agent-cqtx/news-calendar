# News Calendar v1 — Peer Review Findings

Reviewer: Peer Review · Date: 2026-10-17
Scope: `db/`, `api/`, `web/`, `docker-compose.yml`, `.env.example`, `README.md`, `RUN.md`, `tests/` (read-only static review; I did **not** build or run the stack or the tests).
Spec: Postgres, optional API key on writes, plain `yyyy-MM-dd` dates, month view, day list, search, post test page, HA-friendly.

**Verdict: not runnable yet.** Two blockers (no schema, month endpoint mismatch) stop a first `docker compose up` from showing a working calendar. Everything else is fixable in small steps. The API code itself is in good shape.

Severity: **Blocker** = app doesn't work · **High** = wrong behavior likely hit · **Medium** = edge case / robustness · **Low** = polish.

## Summary table

| # | Sev | Finding | Owner |
|---|---|---|---|
| 1 | Blocker | `db/init/` is empty: no schema exists | SQL Dev |
| 2 | Blocker | Month view calls a route the API doesn't have | React Front End (or Back End, see fix) |
| 3 | High | Search results: FE expects `snippet`, API returns `summary` | React Front End |
| 4 | High | Post form: `date` is optional in FE but required by API | React Front End |
| 5 | High | Dedupe rules aren't enforced by the DB (race / drift) | SQL Dev + Back End |
| 6 | Medium | "Today" default for updates uses UTC date | Back End |
| 7 | Medium | FE types don't match API DTOs (`label`, `showOnCalendar`, create response) | React Front End |
| 8 | Medium | Duplicate detection in FE keys off status codes, not `duplicate` flag | React Front End |
| 9 | Medium | Source-URL uniqueness rule is stricter than spec implies | Biz Analyst + Back End |
| 10 | Medium | Web healthcheck `wget http://localhost/` can fail on IPv6 resolution | DevOps |
| 11 | Low | CSP `frame-ancestors` for HA is very broad | DevOps |
| 12 | Low | Docs: stale `RUN.md`, no API contract/curl examples, template `web/README.md` | DevOps (+ Front End) |
| 13 | Low | New events don't appear until the cache refetches | React Front End |
| 14 | Low | API container runs as root; API key sent over plain HTTP | DevOps / Back End |

## Details

### 1. Blocker — No schema (SQL Dev)
`db/init/` exists but contains no files, and compose mounts it as the Postgres init dir. The API maps `events`, `event_sources`, `event_updates` with a generated `search_vector` and does not migrate, so every request fails with "relation does not exist" and `/health` stays fine only because it just opens a connection.
**Fix:** add `db/init/001_schema.sql` creating exactly what `AppDbContext` maps:
- `events(id uuid pk, title varchar(200), title_normalized varchar(200), summary varchar(500), description text, event_date date, external_key varchar(200), created_at timestamptz, updated_at timestamptz, search_vector tsvector GENERATED ALWAYS … STORED)` with a GIN index on `search_vector` (use the `english` config to match the API's `WebSearchToTsQuery("english", …)`).
- `event_sources(id, event_id fk cascade, update_id fk nullable, url, url_normalized, kind, label)`.
- `event_updates(id, event_id fk cascade, update_date date, text, show_on_calendar bool default false, created_at timestamptz)`.
- Indexes on `event_date`, `(event_date, title_normalized)`, `url_normalized`, and a **unique partial index** on `external_key where external_key is not null` (the API's 409 safety net relies on it).
Optionally add a tiny `002_seed.sql` with a few sample events so the month view isn't empty. Note in a comment that init scripts only run on an empty volume.

### 2. Blocker — Month route mismatch
FE calls `GET /api/events?month=2026-10` (`web/src/api/client.ts`). The API only exposes `GET /api/events/month/{month}` and there is no list route, so the month grid gets a 404/405.
**Fix (pick one, tell both bots):** simplest is for **React Front End** to call `` `${BASE}/month/${month}` ``. Update the comment in `types.ts` too. (Don't add a second route on the API just for this.)

### 3. High — Search shape mismatch
`SearchResult` expects `{id,date,title,snippet}`; API returns `EventSummaryDto {id,date,title,summary}`. The snippet paragraph renders empty.
**Fix (Front End):** use `summary` (optional) in `SearchBox` and the type. If a highlighted snippet is wanted later, that's a Back End change using `ts_headline`; not needed for v1.

### 4. High — Date required by API, optional in the form
API validation returns 400 `date: Date is required (yyyy-MM-dd)` when missing; the form labels date as optional and omits it when blank. FE validation also only checks the *format*, not that it's a real date (`2026-02-31` passes `isDateKey`, API then rejects).
**Fix (Front End):** make date required (default it to today via the local-parts helper, not `new Date(string)`), and validate month/day ranges. Show API `details` field errors next to inputs (the API returns `{error, details:{field:msg}}`).

### 5. High — Dedupe is only partly enforced in the DB
The API checks title+date and source URL in code, but only `external_key` has a unique index (per code comments). Two concurrent posts, or a manual SQL insert, can create duplicates; PUT can also change a title/date into a duplicate without any DB guard.
**Fix:** **SQL Dev** to add `unique (event_date, title_normalized)` on `events`, and decide on the URL rule (see #9). **Back End** to extend `Save()` so any `UniqueViolation` maps to 409 with the constraint name (it already does; just confirm the new constraints are caught on PUT too, which currently goes through `Save`).

### 6. Medium — "Today" for updates uses UTC
`CreateUpdateRequest.Date` defaults to `DateOnly.FromDateTime(DateTime.UtcNow)`. After ~8 PM Eastern that is tomorrow's date. Spec says plain dates, so the day should be the user's.
**Fix (Back End):** make `Date` required on updates, or use local time from the container `TZ` (`DateTime.Now`) and note it. Also add a test for the evening case.

### 7. Medium — FE types vs API DTOs
- FE `Source.title` vs API `label`; API also returns `id`, `updateId`.
- FE `EventUpdate` lacks `showOnCalendar`, `createdAt`.
- FE `NewEvent` is fine, but the POST **response** is `{ event, duplicate }`, not an event.
- `EventDetail` omits `externalKey`, `createdAt`, `updatedAt`.
**Fix (Front End):** replace the "ASSUMED" types with ones copied from `Dtos.cs`, remove the "ASSUMED" comments, and render `label` (fallback to hostname) in `EventCard`.

### 8. Medium — Duplicate UI logic
`PostPage` treats any 200 or 409 as "Duplicate". Per the API: 201 created, 200 = same `externalKey` (idempotent, `duplicate:true`), 409 = same title+date or reused URL (with `details.reason` of `title_date` or `url`), 400 = validation, 401 = bad key.
**Fix (Front End):** label by status plus `body.duplicate` / `details.reason`, and show a clear 401 message ("API key required").

### 9. Medium — Source URL uniqueness is strict
Posting any event whose source URL already exists on **another** event returns 409. In a news calendar, one article (or one YouTube video) often supports several events. This could reject legitimate posts and was probably not in the agreed spec.
**Fix:** **Biz Analyst** to confirm intended rule (global unique vs. unique per event). If per-event, **Back End** drops the cross-event check and **SQL Dev** uses `unique (event_id, url_normalized)` instead.

### 10. Medium — Web healthcheck
`wget -qO- http://localhost/` in nginx:alpine can resolve `localhost` to `::1` while nginx listens only on IPv4, giving flapping "unhealthy" (and `web` has nothing depending on it, but HA/uptime tools may watch it).
**Fix (DevOps):** use `http://127.0.0.1/` or add `listen [::]:80;` to `nginx.conf`.

### 11. Low — CSP for Home Assistant
`frame-ancestors 'self' http://homeassistant.local:8123 http://*:8123;` allows any host on :8123 to frame the app. Fine on a home LAN, but README should say to replace it with the real HA address. (README mentions editing; just make the default narrower or add an `.env`-driven template.)

### 12. Low — Docs
- `RUN.md` only describes the web dev server and contradicts README. Fold into README or delete.
- `web/README.md` is presumably the Vite template; replace with a short React-for-.NET-devs note.
- README lacks a sample `curl` for `POST /api/events` (with and without `X-Api-Key`) and a table of endpoints and status codes. The "What to notice" notes are good and clear; add one on `DateOnly` ↔ `yyyy-MM-dd` and one on why dates are never parsed with `new Date(string)`.
- README says init SQL is in `db/init/`; it will be true once #1 lands.

### 13. Low — Cache after post
After a successful POST the month/day queries remain cached. Call `queryClient.invalidateQueries({ queryKey: ['events'] })` on 200/201 so the new event shows when the user navigates back. Good teaching moment for the "What to notice" notes.

### 14. Low — Hardening notes
- API image runs as root; add a non-root `USER` (use `APP_UID` in .NET 8 images).
- With plain HTTP, the `X-Api-Key` is visible on the LAN; the README already warns not to expose publicly, so mention that the key is a convenience guard, not real auth.
- `.env` copy step is documented; add a root `.gitignore` containing `.env`.

## What looks good
- Strict, culture-invariant date parsing (`DateParsing`) and `DateOnly` end to end; FE date helpers deliberately avoid `new Date("yyyy-MM-dd")`.
- API key: constant-time compare via hashed values, GETs stay open, blank key = open writes, matching the spec.
- Consistent `{ error, details }` error shape, including a catch-all middleware; sensible length validation matching column sizes.
- Month query uses a half-open date range; search uses `WebSearchToTsQuery` with ranking and a clamped limit.
- Compose: health checks chained with `service_healthy`, only `web` publishes a port, API proxied same-origin through nginx (no CORS needed), backup/restore and reset documented.
- Test project covers normalization, date parsing, API key, health, and event endpoints (not executed in this review).

## Suggested order
1) SQL Dev schema (#1) → 2) Front End route + shape fixes (#2, #3, #4, #7, #8) → 3) Back End/SQL Dev dedupe + date default (#5, #6, #9 after BA decision) → 4) DevOps (#10–#12, #14) → 5) Peer Review re-check.

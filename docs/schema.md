# Schema

Defined in `db/init/001_schema.sql` (PostgreSQL 16). SQL owns the schema; the API only maps it with EF Core and runs no migrations. The script is idempotent, and it runs once, on first volume creation.

## Tables

**events**: one row per news event.
- `id` uuid PK, `title` varchar(200), `summary` varchar(500), `description` text
- `event_date` date: a calendar date, no time zone
- `external_key` varchar(200), optional, for idempotent imports
- `title_normalized` varchar(200): set by the API, used for dedupe
- `created_at`, `updated_at` timestamptz (a trigger keeps `updated_at` current, even for manual SQL edits)
- `search_vector` tsvector: generated and stored from title (weight A), summary (B), description (C), English config
- Check: title is not blank

**event_updates**: follow-ups on an event. `event_id` FK (delete cascades), `update_date`, `text`, `show_on_calendar` (default false), `created_at`.

**event_sources**: links for an event. `event_id` FK (cascade), optional `update_id` FK (set null if the update is deleted), `url`, `url_normalized`, `kind`, `label`.

## Constraints and indexes

| Index | Why |
|---|---|
| `ix_events_event_date` | month and day lookups |
| `ux_events_external_key` (unique, where not null) | idempotent imports |
| `ux_events_date_title_norm` (unique on date + normalized title) | same-day duplicate titles |
| `ix_events_search_vector` (GIN) | full-text search |
| `ix_event_sources_url_normalized` | "is this URL already used" check |
| `ix_event_sources_event_id`, `ix_event_updates_event_id` | loading an event's children |
| `ix_event_updates_calendar` (partial, where `show_on_calendar`) | updates that appear on the calendar |

## Seed data

`db/init/002_seed.sql` inserts 10 sample events with fixed ids (`e0000000-...`) and `ON CONFLICT DO NOTHING`. `db/seed-wipe.sql` deletes only those rows.

## Change the schema

Edit the SQL, then apply it by hand with `psql`, or reset with `docker compose down -v` (this deletes all data). Init scripts don't run again on an existing volume.

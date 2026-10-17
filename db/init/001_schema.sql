-- News Calendar v1 schema (Postgres 13+). Idempotent: safe to re-run.
-- SQL owns the schema; the API (EF Core) only maps it, no migrations.
-- Runs once on first volume create via /docker-entrypoint-initdb.d.

CREATE TABLE IF NOT EXISTS events (
    id               uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    title            varchar(200) NOT NULL,
    summary          varchar(500),
    description      text,
    event_date       date         NOT NULL,
    external_key     varchar(200),
    title_normalized varchar(200) NOT NULL,           -- API sets lower(trim(title))
    created_at       timestamptz  NOT NULL DEFAULT now(),
    updated_at       timestamptz  NOT NULL DEFAULT now(),
    search_vector    tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(title, '')),       'A') ||
        setweight(to_tsvector('english', coalesce(summary, '')),     'B') ||
        setweight(to_tsvector('english', coalesce(description, '')), 'C')
    ) STORED,
    CONSTRAINT ck_events_title_not_blank CHECK (length(btrim(title)) > 0)
);

CREATE TABLE IF NOT EXISTS event_updates (
    id               uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id         uuid        NOT NULL REFERENCES events(id) ON DELETE CASCADE,
    update_date      date        NOT NULL,
    text             text        NOT NULL,
    show_on_calendar boolean     NOT NULL DEFAULT false,
    created_at       timestamptz NOT NULL DEFAULT now()
);

-- Created after event_updates because a source may point at an update.
CREATE TABLE IF NOT EXISTS event_sources (
    id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id       uuid        NOT NULL REFERENCES events(id) ON DELETE CASCADE,
    update_id      uuid        REFERENCES event_updates(id) ON DELETE SET NULL,
    url            text        NOT NULL,
    url_normalized text        NOT NULL,              -- API sets the normalized form
    kind           varchar(30),                       -- free text; API defaults to 'link'
    label          varchar(200)
);

-- events indexes
CREATE INDEX IF NOT EXISTS ix_events_event_date
    ON events (event_date);
CREATE UNIQUE INDEX IF NOT EXISTS ux_events_external_key
    ON events (external_key) WHERE external_key IS NOT NULL;
-- Title+date dedupe enforced by the DB (API still pre-checks to return a friendly 409).
-- This unique index also serves the (event_date, title_normalized) lookup/sort.
CREATE UNIQUE INDEX IF NOT EXISTS ux_events_date_title_norm
    ON events (event_date, title_normalized);
CREATE INDEX IF NOT EXISTS ix_events_search_vector
    ON events USING gin (search_vector);

-- child-table indexes
-- Source URL rule: the same URL MAY appear on many events (one article/video can support
-- several events). This index is deliberately NON-unique, lookup only. No uniqueness is
-- enforced in the DB; the API drops duplicate URLs within a single request.
-- For databases created before this rule: DROP INDEX IF EXISTS ux_event_sources_url_normalized;
CREATE INDEX IF NOT EXISTS ix_event_sources_url_normalized ON event_sources (url_normalized);
CREATE INDEX IF NOT EXISTS ix_event_sources_event_id       ON event_sources (event_id);
CREATE INDEX IF NOT EXISTS ix_event_updates_event_id       ON event_updates (event_id);
CREATE INDEX IF NOT EXISTS ix_event_updates_calendar
    ON event_updates (update_date) WHERE show_on_calendar;

-- Keep updated_at honest even for manual SQL edits.
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_events_updated_at ON events;
CREATE TRIGGER trg_events_updated_at
    BEFORE UPDATE ON events
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

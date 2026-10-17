-- Sample data so first `compose up` shows something. Wipe with db/seed-wipe.sql.
-- Fixed ids + ON CONFLICT DO NOTHING keep it idempotent.
INSERT INTO events (id, title, summary, description, event_date, external_key, title_normalized)
SELECT id, title, summary, description, event_date::date, external_key, lower(btrim(title))
FROM (VALUES
 ('e0000000-0000-0000-0000-000000000001'::uuid, 'Apple September Event Recap', 'New iPhone lineup announced.', 'Apple unveiled its new iPhone lineup and watch updates.', '2026-10-01', 'seed-apple-sept'),
 ('e0000000-0000-0000-0000-000000000002'::uuid, 'Fed Interest Rate Decision', 'FOMC holds rates steady.', 'The Federal Open Market Committee left rates unchanged and signaled data-dependent policy.', '2026-10-07', 'seed-fed-oct'),
 ('e0000000-0000-0000-0000-000000000003'::uuid, 'SpaceX Starship Flight Test', 'Next Starship test flight.', 'Launch of the next integrated Starship flight test from Starbase, with booster catch attempt.', '2026-10-12', 'seed-starship-12'),
 ('e0000000-0000-0000-0000-000000000004'::uuid, '.NET 11 Preview Released', 'New preview with runtime and C# changes.', 'Microsoft shipped a new .NET 11 preview covering runtime, ASP.NET Core and C# language updates.', '2026-10-15', 'seed-dotnet11-preview'),
 ('e0000000-0000-0000-0000-000000000005'::uuid, 'Global Climate Summit Opens', 'Delegates gather for annual climate talks.', 'Annual climate summit opens with emissions targets and funding on the agenda.', '2026-10-21', 'seed-climate-summit'),
 ('e0000000-0000-0000-0000-000000000006'::uuid, 'React Conf Keynote', 'React team shares roadmap.', 'Keynote covering the React compiler, server components, and tooling roadmap.', '2026-10-28', 'seed-react-conf'),
 ('e0000000-0000-0000-0000-000000000007'::uuid, 'US Election Day', 'Voters go to the polls.', 'General election day across the United States.', '2026-11-03', 'seed-election-day'),
 ('e0000000-0000-0000-0000-000000000008'::uuid, 'Postgres 19 Beta Announced', 'Community beta with new features.', 'PostgreSQL community announces the beta with planner and replication improvements.', '2026-11-12', 'seed-pg19-beta'),
 ('e0000000-0000-0000-0000-000000000009'::uuid, 'Black Friday Sales Begin', 'Retail sales kick off.', 'Major retailers start Black Friday deals.', '2026-11-27', 'seed-black-friday'),
 ('e0000000-0000-0000-0000-000000000010'::uuid, 'Year-End Tech Layoff Watch', 'Tracking announced cuts.', 'Running event tracking layoff announcements across large tech companies.', '2026-11-05', NULL)
) AS v(id, title, summary, description, event_date, external_key)
ON CONFLICT DO NOTHING;

INSERT INTO event_updates (id, event_id, update_date, text, show_on_calendar) VALUES
 ('a0000000-0000-0000-0000-000000000001', 'e0000000-0000-0000-0000-000000000003', '2026-10-10', 'Launch window confirmed for Oct 12.', false),
 ('a0000000-0000-0000-0000-000000000002', 'e0000000-0000-0000-0000-000000000003', '2026-10-13', 'Flight completed; booster catch succeeded. Full review pending.', true)
ON CONFLICT DO NOTHING;

INSERT INTO event_sources (id, event_id, update_id, url, url_normalized, kind, label) VALUES
 ('b0000000-0000-0000-0000-000000000001', 'e0000000-0000-0000-0000-000000000001', NULL, 'https://www.apple.com/newsroom/', 'https://apple.com/newsroom', 'article', 'Apple Newsroom'),
 ('b0000000-0000-0000-0000-000000000002', 'e0000000-0000-0000-0000-000000000003', NULL, 'https://www.youtube.com/watch?v=dQw4w9WgXcQ', 'https://youtube.com/watch?v=dQw4w9WgXcQ', 'youtube', 'Launch livestream (placeholder video)'),
 ('b0000000-0000-0000-0000-000000000003', 'e0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000002', 'https://www.spacex.com/launches/', 'https://spacex.com/launches', 'article', 'SpaceX launches'),
 ('b0000000-0000-0000-0000-000000000004', 'e0000000-0000-0000-0000-000000000004', NULL, 'https://devblogs.microsoft.com/dotnet/', 'https://devblogs.microsoft.com/dotnet', 'article', '.NET Blog'),
 ('b0000000-0000-0000-0000-000000000005', 'e0000000-0000-0000-0000-000000000008', NULL, 'https://www.postgresql.org/about/news/', 'https://postgresql.org/about/news', 'article', 'PostgreSQL news')
ON CONFLICT DO NOTHING;

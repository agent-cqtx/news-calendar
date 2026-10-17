# News Calendar v1

A personal calendar of news events: browse by month, open a day, search, and post new events.
React (Vite) + ASP.NET Core API + PostgreSQL 16, run with Docker Compose. Built for a trusted home network, and it embeds in Home Assistant.

## Quick start

```bash
cd /workspace/news-calendar
cp .env.example .env      # set POSTGRES_PASSWORD, and use the same password in ConnectionStrings__Default
docker compose up --build
```

- App: http://localhost:8088 (from another device: `http://<server-ip>:8088`)
- Post form: http://localhost:8088/post
- First boot loads 10 sample events so the calendar isn't empty (remove them with `db/seed-wipe.sql`, see Reset).

You need Docker with Compose v2.

## More docs

| File | What's in it |
|---|---|
| [docs/api.md](docs/api.md) | Every endpoint: request, response, status codes, duplicate rules |
| [docs/schema.md](docs/schema.md) | Tables, constraints, indexes, search |
| [docs/normalization.md](docs/normalization.md) | How titles and URLs are normalized for dedupe |
| [docs/operations.md](docs/operations.md) | Containers, healthchecks, backup/restore, running without Docker, troubleshooting |
| [RUN.md](RUN.md) | Frontend-only dev loop (`npm run dev`) |

## Layout

| Path | What |
|---|---|
| `docker-compose.yml`, `.env.example` | the stack and its config |
| `docker-compose.public.yml` | override for public use: refuses to start without `API_KEY` |
| `api/` | ASP.NET Core API (`NewsCalendar.Api`), `GET /health` |
| `web/` | React app; `nginx.conf.template` serves the build and proxies `/api` |
| `db/init/` | `001_schema.sql`, `002_seed.sql`: run once, on first database boot |
| `db/seed-wipe.sql` | manual: deletes only the sample rows |
| `tests/` | API tests |

Only `web` publishes a port (8088). The API and Postgres are reachable only on the Compose network.

## Environment variables (`.env`)

| Variable | Purpose |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | database name and login (defaults: `newscal` / `newscal`; change the password) |
| `ConnectionStrings__Default` | API connection string. Host is `db` (the Compose service name). Keep the password identical to `POSTGRES_PASSWORD` |
| `API_KEY` | blank = anyone who can reach the app can write. Set it to require an `X-Api-Key` header on writes. **Required for public use** |
| `RateLimit__ReadPerMinute` | optional, default 300 per client IP, `0` = off |
| `RateLimit__WritePerMinute` | optional, default 60 per client IP, `0` = off. Behind a proxy also set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so limits apply per real client |
| `CORS_ORIGINS` | only matters when a browser calls the API from a different origin. Through the web proxy you don't need it |
| `WEB_BIND` | which host address publishes port 8088. `0.0.0.0` = whole LAN; `127.0.0.1` = this machine only (use for public setups behind a proxy) |
| `HA_ORIGIN` | one exact origin allowed to iframe the app, e.g. `http://192.168.1.50:8123`. Blank = no embedding |
| `TZ` | `America/New_York` |

Dates are plain `yyyy-MM-dd` calendar dates with no time zone. Created and updated timestamps are stored in UTC.

## What to notice (Docker basics)

- Containers reach each other by service name, so the API connects to `Host=db`, not `localhost`.
- `depends_on: condition: service_healthy` makes the API wait for Postgres, and the web container wait for the API.
- `db/init/` runs only when the data volume is empty. Editing those files later changes nothing until you reset.
- The API runs as a non-root user. Healthchecks use `127.0.0.1` because `localhost` can resolve to IPv6, where nginx isn't listening.

## Health

```bash
docker compose ps          # db, api and web should all say "healthy"
docker compose logs -f api
curl http://localhost:8088/health
```

## Reset (deletes data)

```bash
docker compose down -v && docker compose up --build    # wipe everything, reload schema + samples
```

To remove only the 10 sample events and keep your own:

```bash
docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"' < db/seed-wipe.sql
```

## Backup and restore

```bash
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > backup_$(date +%F).sql
docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"' < backup_YYYY-MM-DD.sql
```

Restore into a fresh database (`down -v`, then `up` first, so the schema exists), or dump with `--clean --if-exists` to overwrite in place.

## API examples

Base URL: `http://localhost:8088/api/events`. Reads are always open. Writes need `X-Api-Key: <key>` only when `API_KEY` is set. Full detail in [docs/api.md](docs/api.md).

```bash
curl -s http://localhost:8088/health
curl -s http://localhost:8088/api/events/month/2026-10          # list a month (summaries)
curl -s http://localhost:8088/api/events/day/2026-10-15         # one day, full detail
curl -s "http://localhost:8088/api/events/search?q=starship&limit=10"
curl -s http://localhost:8088/api/events/<event-id>

# create (drop the X-Api-Key line when API_KEY is blank)
curl -s -X POST http://localhost:8088/api/events \
  -H "Content-Type: application/json" -H "X-Api-Key: $API_KEY" \
  -d '{"date":"2026-10-17","title":"Example headline","summary":"One line.","externalKey":"example-1","sources":[{"url":"https://example.com/story","kind":"article","label":"Example"}]}'

# add an update to an event
curl -s -X POST http://localhost:8088/api/events/<event-id>/updates \
  -H "Content-Type: application/json" -H "X-Api-Key: $API_KEY" \
  -d '{"date":"2026-10-18","text":"Follow-up detail.","showOnCalendar":true}'

curl -s -X DELETE http://localhost:8088/api/events/<event-id> -H "X-Api-Key: $API_KEY"
```

Errors come back as `{"error": "...", "details": ...}`. A bad key is `401`, validation is `400`, a duplicate is `409`, an unknown id is `404`.

## Public exposure (internet-facing checklist)

Reads are open to anyone who can reach the site. Writes are protected only by `API_KEY`. So before you publish a URL:

1. **Set a long random `API_KEY`.** `openssl rand -hex 32`, paste it into `.env`. Never leave it blank on a public site: anyone could add or delete events.
2. **Start with the public override.** It refuses to start without an API key:
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.public.yml up -d --build
   ```
3. **Keep the container off the open network.** Put `WEB_BIND=127.0.0.1` in `.env` so port 8088 is reachable only from this machine, then let an HTTPS reverse proxy or tunnel (below) be the only way in.
4. **Use HTTPS.** The `X-Api-Key` header and the key you type into `/post` travel in clear text over plain HTTP. The container deliberately speaks plain HTTP; terminate TLS in front of it.
5. **Set `CORS_ORIGINS`** to your public URL (e.g. `https://news.example.com`). It only matters if something calls the API from another origin.
6. **Don't forward Postgres (5432) or the API (8080).** Neither is published by Compose; keep it that way.

### Posting with the API key (scripts, Grok, cron)

Send the key in the `X-Api-Key` header on every write. Reads need nothing.

```bash
export NEWS_URL=https://news.example.com
export API_KEY=...           # same value as in .env; keep it out of git and shell history

curl -s -X POST "$NEWS_URL/api/events" \
  -H "Content-Type: application/json" -H "X-Api-Key: $API_KEY" \
  -d '{"date":"2026-10-17","title":"Headline","summary":"One line.","externalKey":"source-123","sources":[{"url":"https://example.com/story","kind":"article"}]}'
```

- A wrong or missing key gives `401 {"error":"Missing or invalid API key."}`.
- Use a stable `externalKey` per item. Re-posting the same one returns `200` with `duplicate: true` instead of creating a second event, so a script can safely retry.
- To rotate the key: change `API_KEY` in `.env`, run `docker compose up -d` (api restarts), and update your scripts.
- The key is checked with a constant-time comparison. It is a shared secret, not per-user auth. If you need to hide reads too, or have several users, add authentication at the reverse proxy.
- The key box on `/post` is typed per visit and not saved. On a public site, only type it over HTTPS.

### What nginx now does for you (`web/nginx.conf.template`)

| Protection | Setting |
|---|---|
| Security headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, and a strict `Content-Security-Policy` (everything from this site only; framing limited to `'self'` plus `HA_ORIGIN`). `frame-ancestors` replaces `X-Frame-Options` so Home Assistant can still embed it |
| Rate limits per IP | reads about 10 req/s (burst 40), writes (POST/PUT/DELETE) about 30/min (burst 10); excess gets `429` |
| Connection limit | 30 simultaneous connections per IP |
| Request limits | body at most 64 KB; 10 s header/body timeouts; `server_tokens off` |
| Real client IP | trusts `X-Forwarded-For` only from private addresses (your proxy on this host or LAN), so the limits apply per visitor, not to the proxy. A client straight from the internet can't spoof it |
| `/health` | answers only to local and private-network clients, so the public can't probe database status |

Tune the numbers at the top of `web/nginx.conf.template`, then `docker compose up -d --build web`. If scripts post in big batches, raise the `writes` rate or use one request per item with a short pause.

### HTTPS reverse proxy or tunnel

Pick one. All three keep the Compose stack on plain HTTP behind `127.0.0.1:8088`.

**Caddy** (simplest; gets and renews certificates by itself; needs ports 80/443 forwarded to this machine and a DNS name pointing at your home IP):

```caddyfile
news.example.com {
    reverse_proxy 127.0.0.1:8088
    header Strict-Transport-Security "max-age=31536000"
}
```

**Cloudflare Tunnel** (no port forwarding, hides your home IP, free): install `cloudflared`, create a tunnel, and point a hostname at `http://localhost:8088`. HTTPS is handled by Cloudflare.

**Existing nginx / Traefik / Nginx Proxy Manager:** proxy to `http://127.0.0.1:8088`, pass `Host`, `X-Forwarded-For` and `X-Forwarded-Proto`, and add the HSTS header there. Turn on HSTS only once HTTPS works for you, because browsers remember it.

If this machine sits behind a home router, forward only 80 and 443 to the proxy, never 8088, 8080 or 5432.

### Containers

- API and Postgres run as non-root users; the API image uses the built-in `app` user. All three containers also set `no-new-privileges`.
- nginx's main process runs as root inside the web container (so it can bind port 80 and set up) and its workers run as the unprivileged `nginx` user. For stricter setups, switch the web image to `nginxinc/nginx-unprivileged` and change the port mapping.
- Healthchecks are unchanged: `db` uses `pg_isready`, `api` curls `127.0.0.1:8080/health`, `web` requests `http://127.0.0.1/`. The web check uses `/`, not `/health`, so it works with the new restriction on `/health`.
- Keep images current: `docker compose pull && docker compose up -d --build` now and then. Back up first (see above).

## Home Assistant

Add a **Webpage** card pointing at `http://<server-ip>:8088`.

By default nothing else may frame the app. To allow your Home Assistant, set one exact origin, then recreate the web container:

```bash
# .env  (scheme + host + port, no trailing slash or path)
HA_ORIGIN=http://192.168.1.50:8123

docker compose up -d --force-recreate web
curl -sI http://localhost:8088/ | grep -i content-security     # confirm the header
```

- Use the address you type in your browser to reach Home Assistant. An IP and a hostname are two different origins.
- An HTTPS Home Assistant blocks an HTTP iframe (mixed content). Use HA over HTTP on your LAN, or serve this app over HTTPS.
- The header is built from `web/nginx.conf.template` when the container starts, so there is no wildcard list to edit. It is part of a strict CSP now, and `X-Frame-Options` is intentionally not sent (it can't name a specific origin).
- Publishing publicly and embedding in HA over HTTPS: set `HA_ORIGIN` to your HA's `https://` origin.

## Troubleshooting

- **Calendar is empty and `/health` says the database is down:** the passwords in `POSTGRES_PASSWORD` and `ConnectionStrings__Default` don't match. If you changed the password after the first run, the old password is stored in the volume, so run `docker compose down -v` first.
- **Tables don't exist:** the volume was created before `db/init/` had files. Reset with `down -v`.
- **Port 8088 in use:** change `"8088:80"` in `docker-compose.yml`.
- More in [docs/operations.md](docs/operations.md).

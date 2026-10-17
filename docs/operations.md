# Operations

## Services

| Service | Image | Port | Notes |
|---|---|---|---|
| db | `postgres:16-alpine` | 5432 (internal) | data in the `pgdata` named volume; `db/init` mounted read-only into `/docker-entrypoint-initdb.d` |
| api | built from `api/Dockerfile` | 8080 (internal) | .NET 8, non-root, `GET /health` |
| web | built from `web/Dockerfile` | 8088 to 80 | nginx: serves the React build, proxies `/api/` and `/health` to the API |

Start order is enforced by healthchecks: db healthy, then api healthy, then web starts.

## Dockerfiles

- **api:** SDK stage restores from the `.csproj` first (cached layer), then publishes. The runtime stage is `aspnet:8.0` plus `curl` for the healthcheck, running as the built-in non-root user.
- **web:** `node:22-alpine` runs `npm ci` and `npm run build`. The final `nginx:1.27-alpine` stage copies `dist/` and renders `nginx.conf.template` with `HA_ORIGIN` at start.

## Public use

See the "Public exposure" section of the README: `docker-compose.public.yml` (requires `API_KEY`), `WEB_BIND=127.0.0.1`, an HTTPS proxy or tunnel in front, and the nginx rate limits and headers in `web/nginx.conf.template`.

## Day to day

```bash
docker compose up -d --build      # start in the background
docker compose ps                 # health
docker compose logs -f api
docker compose down               # stop, keep data
docker compose down -v            # stop and delete data
```

## Without Docker

1. Run Postgres 16 locally, create the database, and apply `db/init/*.sql` with `psql`.
2. `cd api`, set `ConnectionStrings__Default`, then `dotnet run`. Set `ASPNETCORE_URLS=http://localhost:8080` if needed.
3. `cd web && npm install && npm run dev`. Vite proxies `/api` to `http://localhost:8080` (see `web/vite.config.ts`).

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `api` never turns healthy | check `docker compose logs api`; usually the connection string password doesn't match `POSTGRES_PASSWORD` |
| Changed the password and it still fails | Postgres stored the old one in the volume at first boot; `docker compose down -v` |
| Empty tables / no sample data | the volume predates `db/init/`; reset with `down -v` |
| `web` unhealthy but app works | healthcheck uses `127.0.0.1`; confirm nginx is up with `docker compose logs web` |
| HA card is blank | `HA_ORIGIN` not set or doesn't match the exact origin, or HA is HTTPS while this app is HTTP |
| Getting `429 Too Many Requests` | an nginx rate limit tripped; raise the rates at the top of `web/nginx.conf.template` and rebuild web |
| `curl /health` from the internet returns 403 | intended; only local/private clients may call it |
| Port 8088 busy | change the host port in `docker-compose.yml` |

# Run News Calendar (web)

    cd web
    npm install
    npm run dev        # http://localhost:5173 ; /api is proxied to http://localhost:8080 (edit vite.config.ts)
    npm run build      # production bundle in dist/ (serve behind nginx; add SPA fallback try_files $uri /index.html)

Pages: `/` month grid (+search) · `/day/yyyy-MM-dd` · `/post` API test form.

Full-stack run, env vars, API examples and Home Assistant notes: see [README.md](README.md). Lower-level notes are in [docs/](docs/).

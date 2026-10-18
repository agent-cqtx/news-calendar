import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// BASE_HREF build arg: '/' (default, standalone) or '/news-calendar/' (VPS path-based deploy).
const BASE = process.env.BASE_HREF || '/'

export default defineConfig({
  plugins: [react()],
  base: BASE,
  server: { proxy: { '/api': 'http://localhost:8080', '/news-calendar/api': 'http://localhost:8080' } },
})

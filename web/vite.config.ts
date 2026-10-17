import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev only: forward /api to the API so code can use the same relative URL as production.
export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': 'http://localhost:8080' } }, // change to your API port
})

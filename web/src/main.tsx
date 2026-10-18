import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'

// Providers ≈ DI registrations: anything below can use routing / the query cache.
const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 60_000, retry: 1 } },
})

// React Router basename: matches Vite's base config (via import.meta.env.BASE_URL).
// Standalone: '/' → no prefix. VPS path-based: '/news-calendar/' → all links under that prefix.
const BASENAME = import.meta.env.BASE_URL?.replace(/\/+$/, '') || undefined

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter basename={BASENAME}>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)

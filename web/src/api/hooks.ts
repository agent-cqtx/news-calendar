import { useQuery } from '@tanstack/react-query'
import { api } from './client'

// Query key = cache key (≈ a cache dictionary key). Same key → shared, cached data.
export const useMonthEvents = (month: string) =>
  useQuery({ queryKey: ['events', 'month', month], queryFn: () => api.eventsForMonth(month) })

// Fixed page size for the All view; the API's limit param stays flexible.
export const ALL_PAGE_SIZE = 50

// Page is 1-based; offset = (page-1) * ALL_PAGE_SIZE. queryKey includes the
// page so each page is cached separately.
export const useAllEvents = (page: number) =>
  useQuery({
    queryKey: ['events', 'all', page],
    queryFn: () => api.allEvents(ALL_PAGE_SIZE, (page - 1) * ALL_PAGE_SIZE),
  })

export const useDayEvents = (date: string) =>
  useQuery({ queryKey: ['events', 'day', date], queryFn: () => api.eventsForDay(date), enabled: date !== '' })

export const useSearch = (q: string) =>
  useQuery({
    queryKey: ['events', 'search', q],
    queryFn: () => api.search(q),
    enabled: q.length >= 2, // don't call the API for empty/1-char input
  })

import { useQuery } from '@tanstack/react-query'
import { api } from './client'

// Query key = cache key (≈ a cache dictionary key). Same key → shared, cached data.
export const useMonthEvents = (month: string) =>
  useQuery({ queryKey: ['events', 'month', month], queryFn: () => api.eventsForMonth(month) })

export const useDayEvents = (date: string) =>
  useQuery({ queryKey: ['events', 'day', date], queryFn: () => api.eventsForDay(date), enabled: date !== '' })

export const useSearch = (q: string) =>
  useQuery({
    queryKey: ['events', 'search', q],
    queryFn: () => api.search(q),
    enabled: q.length >= 2, // don't call the API for empty/1-char input
  })

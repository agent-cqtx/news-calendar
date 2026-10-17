import type { ApiError, CreateEventResponse, EventDetail, EventSummary, NewEvent } from './types'

// Relative base → works behind the nginx proxy (and Vite's dev proxy).
const BASE = '/api/events'

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) {
    let msg = `Request failed (${res.status})`
    try {
      const body = await res.json()
      if (body?.error) msg = body.error
    } catch { /* non-JSON body */ }
    throw new Error(msg)
  }
  return res.json() as Promise<T>
}

export const api = {
  eventsForMonth: (month: string) =>
    getJson<EventSummary[]>(`${BASE}/month/${encodeURIComponent(month)}`),
  eventsForDay: (date: string) =>
    getJson<EventDetail[]>(`${BASE}/day/${encodeURIComponent(date)}`),
  search: (q: string) =>
    getJson<EventSummary[]>(`${BASE}/search?q=${encodeURIComponent(q)}`),
}

/** Outcome of POST /api/events, kept as data so the test page can show every status. */
export type PostResult =
  | { kind: 'created'; status: 201; data: CreateEventResponse }
  | { kind: 'duplicate'; status: 200; data: CreateEventResponse } // same externalKey
  | { kind: 'conflict'; status: 409; error: ApiError } // same title+date, or a source URL already used
  | { kind: 'invalid'; status: 400; error: ApiError } // error.details = field → message
  | { kind: 'unauthorized'; status: 401; error: ApiError }
  | { kind: 'error'; status: number; error: ApiError }

/**
 * Returns the outcome instead of throwing, so a 409 or 400 is data the UI can render.
 * Network failures still throw.
 */
export async function postEvent(event: NewEvent, apiKey: string): Promise<PostResult> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (apiKey.trim()) headers['X-Api-Key'] = apiKey.trim()
  const res = await fetch(BASE, { method: 'POST', headers, body: JSON.stringify(event) })
  const text = await res.text()
  let body: unknown = null
  try { body = text ? JSON.parse(text) : null } catch { /* non-JSON */ }

  if (res.status === 201) return { kind: 'created', status: 201, data: body as CreateEventResponse }
  if (res.status === 200) return { kind: 'duplicate', status: 200, data: body as CreateEventResponse }
  const error: ApiError = (body as ApiError | null)?.error ? (body as ApiError) : { error: text || `Request failed (${res.status})` }
  if (res.status === 409) return { kind: 'conflict', status: 409, error }
  if (res.status === 400) return { kind: 'invalid', status: 400, error }
  if (res.status === 401) return { kind: 'unauthorized', status: 401, error }
  return { kind: 'error', status: res.status, error }
}

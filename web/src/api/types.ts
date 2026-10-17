// Mirrors api/Models/Dtos.cs. DateOnly serializes as "yyyy-MM-dd".
// Dates stay plain strings — never convert them with new Date(string).
// C# `string?` / `Guid?` arrive as `string | null`.

export type Source = {
  id: string
  updateId: string | null // null = event-level source, otherwise belongs to that update
  url: string
  kind: string | null // "link" by default; e.g. "youtube"
  label: string | null
}

export type EventUpdate = {
  id: string
  date: string // yyyy-MM-dd
  text: string
  showOnCalendar: boolean
  createdAt: string // ISO-8601 instant
}

/** GET /api/events/month/{yyyy-MM} and GET /api/events/search?q= */
export type EventSummary = {
  id: string
  date: string
  title: string
  summary: string | null
}

/** GET /api/events/day/{yyyy-MM-dd} (array) and GET /api/events/{id} */
export type EventDetail = EventSummary & {
  description: string | null
  externalKey: string | null
  createdAt: string
  updatedAt: string
  sources: Source[]
  updates: EventUpdate[]
}

/** Body for POST /api/events. date + title are required by the API. */
export type NewEvent = {
  date: string
  title: string
  summary?: string
  description?: string
  externalKey?: string
  sources?: { url: string; kind?: string; label?: string }[]
}

/** 201 (created) or 200 (same externalKey → duplicate: true). */
export type CreateEventResponse = {
  event: EventDetail
  duplicate: boolean
}

/** Error body for 400/401/404/409: details is a field→message map (400) or { existingId, reason } (409). */
export type ApiError = {
  error: string
  details?: Record<string, string> | { existingId?: string; reason?: string; [k: string]: unknown } | null
}

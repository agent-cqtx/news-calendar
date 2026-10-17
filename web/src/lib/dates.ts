// All date math uses numbers + strings only. No new Date("yyyy-MM-dd"), which
// parses as UTC and can shift the day in local time zones.

const pad = (n: number) => String(n).padStart(2, '0')

export const toMonthKey = (y: number, m: number) => `${y}-${pad(m)}` // m is 1-12
export const toDateKey = (y: number, m: number, d: number) => `${y}-${pad(m)}-${pad(d)}`

export function parseMonth(s: string | null): { year: number; month: number } | null {
  const m = s && /^(\d{4})-(\d{2})$/.exec(s)
  if (!m) return null
  const month = Number(m[2])
  return month >= 1 && month <= 12 ? { year: Number(m[1]), month } : null
}

export function todayParts() {
  const n = new Date() // fine: "now" in local time, not parsing a string
  return { year: n.getFullYear(), month: n.getMonth() + 1, day: n.getDate() }
}

export function shiftMonth(year: number, month: number, delta: number) {
  const idx = year * 12 + (month - 1) + delta
  return { year: Math.floor(idx / 12), month: (idx % 12) + 1 }
}

export const daysInMonth = (year: number, month: number) =>
  new Date(year, month, 0).getDate() // numeric constructor = local, safe

/** 0 = Sunday … 6 = Saturday, for the 1st of the month. */
export const firstWeekday = (year: number, month: number) =>
  new Date(year, month - 1, 1).getDay()

/** Cells for a month grid: null = leading/trailing blank. */
export function buildGrid(year: number, month: number): (number | null)[] {
  const cells: (number | null)[] = Array(firstWeekday(year, month)).fill(null)
  for (let d = 1; d <= daysInMonth(year, month); d++) cells.push(d)
  while (cells.length % 7 !== 0) cells.push(null)
  return cells
}

export const isDateKey = (s: string) => /^\d{4}-\d{2}-\d{2}$/.test(s)

const MONTHS = ['January','February','March','April','May','June','July','August','September','October','November','December']
export const monthLabel = (y: number, m: number) => `${MONTHS[m - 1]} ${y}`

/** "2026-10-17" → "October 17, 2026" without touching Date. */
export function prettyDate(key: string) {
  const [y, m, d] = key.split('-').map(Number)
  return `${MONTHS[m - 1]} ${d}, ${y}`
}

/** True only for a real calendar date, e.g. rejects 2026-02-31. Pure number math, no Date parsing. */
export function isRealDate(key: string): boolean {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(key)
  if (!m) return false
  const y = Number(m[1]), mo = Number(m[2]), d = Number(m[3])
  return mo >= 1 && mo <= 12 && d >= 1 && d <= daysInMonth(y, mo)
}

/** Today as yyyy-MM-dd in the browser's local time. */
export function todayKey(): string {
  const t = todayParts()
  return toDateKey(t.year, t.month, t.day)
}

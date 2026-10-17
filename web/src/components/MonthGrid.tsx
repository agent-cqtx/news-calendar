import { Link } from 'react-router-dom'
import type { EventSummary } from '../api/types'
import { buildGrid, toDateKey, todayParts } from '../lib/dates'

const MAX_VISIBLE = 3
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

type Props = { year: number; month: number; events: EventSummary[] }

export function MonthGrid({ year, month, events }: Props) {
  // Group events by date string once per render (≈ LINQ GroupBy).
  const byDate = new Map<string, EventSummary[]>()
  for (const e of events) byDate.set(e.date, [...(byDate.get(e.date) ?? []), e])

  const t = todayParts()
  const todayKey = toDateKey(t.year, t.month, t.day)

  return (
    <div className="grid" role="grid">
      {WEEKDAYS.map((w) => <div key={w} className="grid-head">{w}</div>)}
      {buildGrid(year, month).map((day, i) => {
        if (day === null) return <div key={`b${i}`} className="cell blank" />
        const key = toDateKey(year, month, day)
        const list = byDate.get(key) ?? []
        const extra = list.length - MAX_VISIBLE
        return (
          <Link key={key} to={`/day/${key}`} className={`cell${key === todayKey ? ' today' : ''}`}>
            <span className="day-num">{day}</span>
            {list.slice(0, MAX_VISIBLE).map((e) => (
              <span key={e.id} className="chip" title={e.title}>{e.title}</span>
            ))}
            {extra > 0 && <span className="more">+{extra} more</span>}
          </Link>
        )
      })}
    </div>
  )
}

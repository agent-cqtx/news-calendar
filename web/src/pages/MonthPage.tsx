import { useSearchParams } from 'react-router-dom'
import { useMonthEvents } from '../api/hooks'
import { MonthGrid } from '../components/MonthGrid'
import { QueryState } from '../components/QueryState'
import { SearchBox } from '../components/SearchBox'
import { monthLabel, parseMonth, shiftMonth, todayParts, toMonthKey } from '../lib/dates'

export function MonthPage() {
  // URL is the state: ?month=yyyy-MM. Navigating = changing the query string.
  const [params, setParams] = useSearchParams()
  const t = todayParts()
  const { year, month } = parseMonth(params.get('month')) ?? { year: t.year, month: t.month }
  const key = toMonthKey(year, month)

  const { data, isLoading, error } = useMonthEvents(key)

  const go = (y: number, m: number) => setParams({ month: toMonthKey(y, m) })
  const prev = shiftMonth(year, month, -1)
  const next = shiftMonth(year, month, 1)

  return (
    <>
      <SearchBox />
      <div className="toolbar">
        <button onClick={() => go(prev.year, prev.month)}>← Prev</button>
        <h2>{monthLabel(year, month)}</h2>
        <button onClick={() => go(next.year, next.month)}>Next →</button>
        <button onClick={() => go(t.year, t.month)}>Today</button>
      </div>
      <QueryState isLoading={isLoading} error={error}>
        <MonthGrid year={year} month={month} events={data ?? []} />
        {data?.length === 0 && <p className="muted">No events this month.</p>}
      </QueryState>
    </>
  )
}

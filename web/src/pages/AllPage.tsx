import { Link, useSearchParams } from 'react-router-dom'
import { ALL_PAGE_SIZE, useAllEvents } from '../api/hooks'
import { QueryState } from '../components/QueryState'
import { prettyDate } from '../lib/dates'

/**
 * GET /api/events/all — everything stored, newest first, 50 per page.
 * The page lives in the URL (?page=N, 1-based) so links are shareable;
 * each row opens its day.
 */
export function AllPage() {
  const [params, setParams] = useSearchParams()
  const parsed = Number.parseInt(params.get('page') ?? '1', 10)
  const page = Number.isFinite(parsed) && parsed > 0 ? parsed : 1
  const { data, isLoading, error } = useAllEvents(page)

  const setPage = (next: number) => {
    const updated = new URLSearchParams(params)
    if (next <= 1) updated.delete('page') // page 1 = clean URL
    else updated.set('page', String(next))
    setParams(updated)
  }

  const isFirstPage = page === 1
  // No total-count query: "Older" unlocks only when the page came back full.
  const hasOlder = data?.length === ALL_PAGE_SIZE

  return (
    <>
      <h2>All events</h2>
      <p className="muted">
        Everything stored, newest first, {ALL_PAGE_SIZE} per page. Click a row to open its day.
      </p>
      <QueryState isLoading={isLoading} error={error} isEmpty={data?.length === 0}
        emptyText={isFirstPage ? 'No events yet.' : 'No more entries.'}>
        <ul className="all-list">
          {data?.map((e) => (
            <li key={e.id}>
              <Link to={`/day/${e.date}`}>
                <strong>{e.title}</strong>
                <span className="muted"> · {prettyDate(e.date)}</span>
              </Link>
              {e.summary && <p className="muted">{e.summary}</p>}
            </li>
          ))}
        </ul>
      </QueryState>
      {!isLoading && !error && (
        <p className="muted">
          <button onClick={() => setPage(page - 1)} disabled={isFirstPage}>← Newer</button>
          {' · page '}{page}{' · '}
          <button onClick={() => setPage(page + 1)} disabled={!hasOlder}>Older →</button>
        </p>
      )}
    </>
  )
}

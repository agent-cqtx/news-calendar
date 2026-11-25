import { Link } from 'react-router-dom'
import { useAllEvents } from '../api/hooks'
import { QueryState } from '../components/QueryState'
import { prettyDate } from '../lib/dates'

/**
 * GET /api/events/all — everything stored, not just this month:
 * a plain chronological list; each row opens its day.
 */
export function AllPage() {
  const { data, isLoading, error } = useAllEvents()

  return (
    <>
      <h2>All events</h2>
      <p className="muted">
        Everything stored. Click a row to open its day.
      </p>
      <QueryState isLoading={isLoading} error={error} isEmpty={data?.length === 0}
        emptyText="No events yet.">
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
    </>
  )
}

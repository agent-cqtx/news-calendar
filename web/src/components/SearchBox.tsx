import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useSearch } from '../api/hooks'
import { prettyDate } from '../lib/dates'
import { QueryState } from './QueryState'

// Debounce: only update `debounced` 300ms after the user stops typing.
function useDebounced(value: string, ms = 300) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(id) // cleanup ≈ IDisposable.Dispose
  }, [value, ms])
  return debounced
}

export function SearchBox() {
  const [text, setText] = useState('')
  const q = useDebounced(text.trim())
  const { data, isLoading, error } = useSearch(q)

  return (
    <section className="search">
      <input
        type="search"
        value={text}
        placeholder="Search events…"
        aria-label="Search events"
        onChange={(e) => setText(e.target.value)}
      />
      {q.length >= 2 && (
        <QueryState isLoading={isLoading} error={error} isEmpty={data?.length === 0} emptyText={`No results for “${q}”.`}>
          <ul className="results">
            {data?.map((r) => (
              <li key={r.id}>
                <Link to={`/day/${r.date}`}>
                  <strong>{r.title}</strong>
                  <span className="muted"> · {prettyDate(r.date)}</span>
                  {r.summary && <p className="snippet">{r.summary}</p>}
                </Link>
              </li>
            ))}
          </ul>
        </QueryState>
      )}
    </section>
  )
}

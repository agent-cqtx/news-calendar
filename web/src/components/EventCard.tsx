import type { EventDetail, Source } from '../api/types'
import { prettyDate } from '../lib/dates'

// Show the label if there is one, otherwise the hostname, otherwise the raw URL.
function sourceText(s: Source) {
  if (s.label) return s.label
  try { return new URL(s.url).hostname } catch { return s.url }
}

function SourceList({ sources }: { sources: Source[] }) {
  return (
    <ul>
      {sources.map((s) => (
        <li key={s.id}>
          {/* rel="noreferrer" is good hygiene for external links */}
          <a href={s.url} target="_blank" rel="noreferrer">{sourceText(s)}</a>
          {(s.kind === 'youtube' || /youtu\.?be/.test(s.url)) && <span className="badge">YouTube</span>}
        </li>
      ))}
    </ul>
  )
}

export function EventCard({ event }: { event: EventDetail }) {
  // Sources with an updateId belong to an update; the rest belong to the event itself.
  const eventSources = event.sources.filter((s) => s.updateId === null)

  return (
    <article className="card">
      <h3>{event.title}</h3>
      {event.summary && <p className="summary">{event.summary}</p>}
      {event.description && <p>{event.description}</p>}

      {eventSources.length > 0 && (
        <>
          <h4>Sources</h4>
          <SourceList sources={eventSources} />
        </>
      )}

      {event.updates.length > 0 && (
        <>
          <h4>Updates</h4>
          <ul>
            {event.updates.map((u) => {
              const own = event.sources.filter((s) => s.updateId === u.id)
              return (
                <li key={u.id}>
                  <strong>{prettyDate(u.date)}: </strong>{u.text}
                  {u.showOnCalendar && <span className="badge on-cal">on calendar</span>}
                  {own.length > 0 && <SourceList sources={own} />}
                </li>
              )
            })}
          </ul>
        </>
      )}
    </article>
  )
}

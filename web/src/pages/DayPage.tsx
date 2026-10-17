import { Link, useParams } from 'react-router-dom'
import { useDayEvents } from '../api/hooks'
import { EventCard } from '../components/EventCard'
import { QueryState } from '../components/QueryState'
import { isRealDate, prettyDate } from '../lib/dates'

export function DayPage() {
  const { date = '' } = useParams() // route param ≈ [FromRoute]
  const valid = isRealDate(date)
  const { data, isLoading, error } = useDayEvents(valid ? date : '')

  if (!valid) return <p className="error">“{date}” isn’t a valid date (yyyy-MM-dd).</p>

  return (
    <>
      <Link to={`/?month=${date.slice(0, 7)}`}>← Back to {date.slice(0, 7)}</Link>
      <h2>{prettyDate(date)}</h2>
      <QueryState isLoading={isLoading} error={error} isEmpty={data?.length === 0} emptyText="No events on this day.">
        {data?.map((e) => <EventCard key={e.id} event={e} />)}
      </QueryState>
    </>
  )
}

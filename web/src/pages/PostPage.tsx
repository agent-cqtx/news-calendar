import { useState } from 'react'
import type { FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { postEvent } from '../api/client'
import type { PostResult } from '../api/client'
import { isRealDate, todayKey } from '../lib/dates'

type SourceRow = { url: string; kind: string; label: string }
const KINDS = ['link', 'youtube', 'other'] // the API defaults a blank kind to "link"

const newRow = (): SourceRow => ({ url: '', kind: 'link', label: '' })

export function PostPage() {
  const queryClient = useQueryClient()

  // One piece of state per field ≈ properties on a form ViewModel.
  const [title, setTitle] = useState('')
  const [summary, setSummary] = useState('')
  const [description, setDescription] = useState('')
  const [date, setDate] = useState(todayKey()) // required by the API, so default to today
  const [externalKey, setExternalKey] = useState('')
  const [sources, setSources] = useState<SourceRow[]>([newRow()])
  const [apiKey, setApiKey] = useState('') // blank = no X-Api-Key header
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [networkError, setNetworkError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<PostResult | null>(null)

  const updateSource = (i: number, patch: Partial<SourceRow>) =>
    setSources((rows) => rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setResult(null)
    setNetworkError(null)

    // Client-side checks mirror the API's rules (cheap feedback before a round trip).
    const errs: Record<string, string> = {}
    if (!title.trim()) errs.title = 'Title is required.'
    else if (title.trim().length > 200) errs.title = 'Title must be at most 200 characters.'
    if (!date) errs.date = 'Date is required (yyyy-MM-dd).'
    else if (!isRealDate(date)) errs.date = 'Enter a real date as yyyy-MM-dd.'
    if (summary.trim().length > 500) errs.summary = 'Summary must be at most 500 characters.'
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    // Omit empty optionals so the API doesn't receive "" values.
    const payload = {
      date,
      title: title.trim(),
      summary: summary.trim() || undefined,
      description: description.trim() || undefined,
      externalKey: externalKey.trim() || undefined,
      sources: sources
        .filter((s) => s.url.trim())
        .map((s) => ({ url: s.url.trim(), kind: s.kind, label: s.label.trim() || undefined })),
    }

    setBusy(true)
    try {
      const r = await postEvent(payload, apiKey)
      setResult(r)
      if (r.kind === 'invalid' && r.error.details) {
        setFieldErrors(r.error.details as Record<string, string>) // API: { field: message }
      }
      if (r.kind === 'created' || r.kind === 'duplicate') {
        // Mark every cached 'events' query (month, day, search) stale so views refetch.
        await queryClient.invalidateQueries({ queryKey: ['events'] })
      }
    } catch (err) {
      setNetworkError(err instanceof Error ? err.message : 'Network error')
    } finally {
      setBusy(false)
    }
  }

  const err = (field: string) => fieldErrors[field] && <span className="error">{fieldErrors[field]}</span>

  return (
    <>
      <h2>Post a news event (test)</h2>
      <p className="muted">Sends <code>POST /api/events</code>. Date and title are required.</p>
      <form className="post-form" onSubmit={submit} noValidate>
        <label>X-Api-Key
          <input value={apiKey} onChange={(e) => setApiKey(e.target.value)} placeholder="required if the API has a key set" />
        </label>
        <label>Title *
          <input value={title} onChange={(e) => setTitle(e.target.value)} />
          {err('title')}
        </label>
        <label>Date * (yyyy-MM-dd)
          <input value={date} onChange={(e) => setDate(e.target.value)} placeholder="2026-10-17" />
          {err('date')}
        </label>
        <label>External key (optional)
          <input value={externalKey} onChange={(e) => setExternalKey(e.target.value)} />
          {err('externalKey')}
        </label>
        <label>Summary
          <textarea rows={2} value={summary} onChange={(e) => setSummary(e.target.value)} />
          {err('summary')}
        </label>
        <label>Description
          <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} />
        </label>

        <fieldset>
          <legend>Sources</legend>
          {sources.map((s, i) => (
            <div key={i}>
              <div className="source-row">
                <input value={s.url} placeholder="https://…" onChange={(e) => updateSource(i, { url: e.target.value })} />
                <select value={s.kind} onChange={(e) => updateSource(i, { kind: e.target.value })}>
                  {KINDS.map((k) => <option key={k}>{k}</option>)}
                </select>
                <button type="button" onClick={() => setSources((r) => r.filter((_, idx) => idx !== i))}>✕</button>
              </div>
              <input
                className="label-input"
                value={s.label}
                placeholder="label (optional)"
                onChange={(e) => updateSource(i, { label: e.target.value })}
              />
              {err(`sources[${i}].url`)}
            </div>
          ))}
          <button type="button" onClick={() => setSources((r) => [...r, newRow()])}>+ Add source</button>
        </fieldset>

        {networkError && <p className="error" role="alert">{networkError}</p>}
        <button type="submit" disabled={busy}>{busy ? 'Posting…' : 'Post event'}</button>
      </form>

      {result && <ResultBox result={result} />}
    </>
  )
}

function ResultBox({ result }: { result: PostResult }) {
  let tone: 'ok' | 'dup' | 'bad' = 'bad'
  let heading: string
  let detail: unknown

  switch (result.kind) {
    case 'created':
      tone = 'ok'; heading = 'Created (201)'; detail = result.data; break
    case 'duplicate':
      tone = 'dup'; heading = 'Duplicate (200): same externalKey, existing event returned'; detail = result.data; break
    case 'conflict': {
      tone = 'dup'
      const reason = (result.error.details as { reason?: string } | null | undefined)?.reason
      heading = `Duplicate (409): ${reason === 'url' ? 'a source URL is already on another event' : 'same title and date already exist'}`
      detail = result.error; break
    }
    case 'invalid':
      heading = 'Validation failed (400)'; detail = result.error; break
    case 'unauthorized':
      heading = 'API key required or wrong (401)'; detail = result.error; break
    default:
      heading = `Error (${result.status})`; detail = result.error
  }

  return (
    <div className={`result ${tone}`} role="status">
      <strong>{heading}</strong>
      <pre>{JSON.stringify(detail, null, 2)}</pre>
    </div>
  )
}

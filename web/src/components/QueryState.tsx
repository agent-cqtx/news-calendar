import type { ReactNode } from 'react'

type Props = {
  isLoading: boolean
  error: Error | null
  isEmpty?: boolean
  emptyText?: string
  children: ReactNode
}

// One place for loading / error / empty so every screen behaves the same.
export function QueryState({ isLoading, error, isEmpty, emptyText = 'Nothing here.', children }: Props) {
  if (isLoading) return <p className="muted">Loading…</p>
  if (error) return <p className="error" role="alert">{error.message}</p>
  if (isEmpty) return <p className="muted">{emptyText}</p>
  return <>{children}</>
}

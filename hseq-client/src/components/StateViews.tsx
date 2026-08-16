import type { ReactNode } from 'react'

interface StateViewProps {
  title: string
  description?: string
  action?: ReactNode
}

export function LoadingState({ title = 'در حال بارگذاری...' }: { title?: string }) {
  return (
    <div className="state-panel" role="status">
      <strong>{title}</strong>
    </div>
  )
}

export function EmptyState({ title, description, action }: StateViewProps) {
  return (
    <div className="state-panel">
      <strong>{title}</strong>
      {description && <span>{description}</span>}
      {action}
    </div>
  )
}

export function ErrorState({ title, description, action }: StateViewProps) {
  return (
    <div className="state-panel state-error" role="alert">
      <strong>{title}</strong>
      {description && <span>{description}</span>}
      {action}
    </div>
  )
}
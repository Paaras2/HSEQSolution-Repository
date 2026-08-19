import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'

interface ModalProps {
  title: ReactNode
  // Optional second line under the title - use it to say what the dialog is for,
  // so the title itself can stay short.
  subtitle?: ReactNode
  size?: 'md' | 'lg'
  onClose: () => void
  children: ReactNode
}

// Centered dialog shell shared by the document dialogs. Owns the behaviour a
// dialog is expected to have and that the previous side panel did not: Escape to
// dismiss, background scroll lock while open, initial focus moved into the
// dialog, and the dialog/labelled-by wiring assistive tech needs.
export function Modal({ title, subtitle, size = 'md', onClose, children }: ModalProps) {
  const titleId = useId()
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

  useEffect(() => {
    // Stops the page behind the overlay from scrolling while the dialog is open.
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [])

  useEffect(() => {
    // Prefer the first real control so the user can start typing immediately;
    // fall back to the panel itself when the dialog is read-only.
    const firstField = panelRef.current?.querySelector<HTMLElement>(
      'input:not([type="hidden"]):not([disabled]), select:not([disabled]), textarea:not([disabled])',
    )
    ;(firstField ?? panelRef.current)?.focus()
  }, [])

  return (
    <div
      className="modal-overlay"
      onMouseDown={(e) => e.target === e.currentTarget && onClose()}
    >
      <div
        ref={panelRef}
        className={`modal-panel modal-panel--${size}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="modal-header">
          <div className="modal-header__text">
            <h2 id={titleId}>{title}</h2>
            {subtitle && <p className="modal-header__subtitle">{subtitle}</p>}
          </div>
          <button type="button" className="modal-close" onClick={onClose} aria-label="بستن">
            <span aria-hidden="true">×</span>
          </button>
        </div>

        {children}
      </div>
    </div>
  )
}


import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import './AppModal.css'
type AppModalProps = {
  isOpen: boolean
  title: string
  children: ReactNode
  onClose: () => void
}

export default function AppModal({
  isOpen,
  title,
  children,
  onClose,
}: AppModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = dialogRef.current

    if (!dialog) return

    if (isOpen && !dialog.open) {
      dialog.showModal()
    }

    if (!isOpen && dialog.open) {
      dialog.close()
    }
  }, [isOpen])

  return (
    <dialog
      ref={dialogRef}
      onClose={onClose}
      aria-label={title}
      className="aura-modal"
    >
      <h2>{title}</h2>

      <div className="aura-modal-content">
        {children}
      </div>

      <button
        type="button"
        onClick={() => dialogRef.current?.close()}
      >
        Đóng
      </button>
    </dialog>
  )
}

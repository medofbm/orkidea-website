export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface ToastOptions {
  type?: ToastType
  /** Milliseconds before auto-dismiss. Default 3000. */
  duration?: number
  action?: { label: string; to: string }
}

export interface ToastItem {
  id: number
  message: string
  type: ToastType
  duration: number
  action?: { label: string; to: string }
}

const MAX_VISIBLE = 3

/** Shared toast state (useState, SSR-safe). Rendered by <AppToastHost />. */
export function useToast() {
  const items = useState<ToastItem[]>('app-toasts', () => [])
  const counter = useState<number>('app-toasts-counter', () => 0)

  function show(message: string, opts: ToastOptions = {}): number {
    counter.value += 1
    const id = counter.value
    const item: ToastItem = {
      id,
      message,
      type: opts.type ?? 'info',
      duration: opts.duration ?? 3000,
      action: opts.action,
    }
    // Newest first; keep only the newest MAX_VISIBLE.
    items.value = [item, ...items.value].slice(0, MAX_VISIBLE)
    return id
  }

  const success = (message: string, opts: Omit<ToastOptions, 'type'> = {}) => show(message, { ...opts, type: 'success' })
  const error = (message: string, opts: Omit<ToastOptions, 'type'> = {}) => show(message, { ...opts, type: 'error' })
  const dismiss = (id: number) => {
    items.value = items.value.filter(t => t.id !== id)
  }

  return { items, show, success, error, dismiss }
}

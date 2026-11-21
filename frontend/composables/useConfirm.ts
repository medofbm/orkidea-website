export interface ConfirmOptions {
  title: string
  message?: string
  confirmLabel?: string
  cancelLabel?: string
  tone?: 'danger' | 'default'
}

// Promise resolver lives outside useState (functions are not serialisable). Client only.
let pendingResolve: ((ok: boolean) => void) | null = null

/** Resolve the open confirm dialog. Used by <AppConfirmHost />. */
export function resolveConfirm(ok: boolean) {
  const r = pendingResolve
  pendingResolve = null
  r?.(ok)
}

/** `const ok = await useConfirm()({ title: 'حذف المنتج؟', tone: 'danger' })` */
export function useConfirm() {
  const state = useState<ConfirmOptions | null>('app-confirm', () => null)

  return (opts: ConfirmOptions): Promise<boolean> => {
    if (import.meta.server) return Promise.resolve(false)
    resolveConfirm(false) // a new request cancels any open one
    return new Promise<boolean>((resolve) => {
      pendingResolve = resolve
      state.value = { ...opts }
    })
  }
}

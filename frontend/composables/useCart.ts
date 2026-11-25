export interface CartState {
  items: { variantId: number; productId: number; name: string; size: string | null;
    image: string | null; quantity: number; price: string; subtotal: string; available: boolean }[]
  count: number
  productsTotal: string
  currency: string
}

export function useCart() {
  const cart = useState<CartState | null>('cart', () => null)
  const busy = useState('cart-busy', () => false)
  const message = useState('cart-message', () => '')
  const requestToken = useState('cart-token', () => '')
  /** Increments after every successful add (POST) so the header badge can animate. */
  const pulse = useState('cart-pulse', () => 0)

  const addKey = useIdempotencyKey()
  async function load() {
    cart.value = await $fetch<CartState>('/api/cart')
  }
  async function mutate(method: 'POST' | 'PATCH' | 'DELETE', variantId: number, quantity = 1): Promise<boolean> {
    if (busy.value) return false
    busy.value = true
    message.value = ''
    try {
      const session = await $fetch<{ requestToken: string }>('/api/session')
      requestToken.value = session.requestToken
      const path = method === 'POST' ? '/api/cart/items' : '/api/cart/items/' + variantId
      const headers: Record<string, string> = { RequestVerificationToken: requestToken.value }
      // Adding is not repeat-safe on its own (it increases the quantity), so it carries an Idempotency-Key.
      if (method === 'POST') headers['Idempotency-Key'] = addKey.get({ variantId, quantity })
      const result = await $fetch<{ success: boolean; message?: string }>(path, {
        method, headers,
        body: method === 'DELETE' ? undefined : { variantId, quantity }
      })
      if (!result.success) {
        message.value = result.message ?? 'تعذر تحديث السلة.'
        return false
      }
      if (method === 'POST') addKey.reset()
      await load()
      message.value = method === 'POST' ? 'تمت الإضافة للسلة.' : 'تم تحديث السلة.'
      if (method === 'POST') pulse.value += 1
      return true
    } catch {
      message.value = 'تعذر تحديث السلة. راجعي اتصال الإنترنت وحاولي مجددًا.'
      return false
    } finally {
      busy.value = false
    }
  }
  return { cart, busy, message, pulse, load, mutate }
}

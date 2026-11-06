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

  async function load() {
    cart.value = await $fetch<CartState>('/api/cart')
  }
  async function mutate(method: 'POST' | 'PATCH' | 'DELETE', variantId: number, quantity = 1) {
    if (busy.value) return
    busy.value = true
    message.value = ''
    try {
      const session = await $fetch<{ requestToken: string }>('/api/session')
      requestToken.value = session.requestToken
      const path = method === 'POST' ? '/api/cart/items' : '/api/cart/items/' + variantId
      const result = await $fetch<{ success: boolean; message?: string }>(path, {
        method, headers: { RequestVerificationToken: requestToken.value },
        body: method === 'DELETE' ? undefined : { variantId, quantity }
      })
      if (!result.success) {
        message.value = result.message ?? 'تعذر تحديث السلة.'
        return
      }
      await load()
      message.value = method === 'POST' ? 'تمت الإضافة للسلة.' : 'تم تحديث السلة.'
    } catch {
      message.value = 'تعذر تحديث السلة. راجعي اتصال الإنترنت وحاولي مجددًا.'
    } finally {
      busy.value = false
    }
  }
  return { cart, busy, message, load, mutate }
}

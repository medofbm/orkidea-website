import type { PromotionText } from '~/utils/deliveryPromotion'

/** Active free-delivery promotion for banners and hints. Client-side, best effort: null on any failure. */
export function useDeliveryPromotion() {
  const promotion = useState<PromotionText | null>('delivery-promotion', () => null)
  const loaded = useState('delivery-promotion-loaded', () => false)
  onMounted(async () => {
    if (loaded.value) return
    loaded.value = true
    try {
      const data = await $fetch<{ promotion: PromotionText | null }>('/api/delivery/promotion', { retry: 0 })
      promotion.value = data?.promotion ?? null
    } catch {
      promotion.value = null
    }
  })
  return { promotion }
}

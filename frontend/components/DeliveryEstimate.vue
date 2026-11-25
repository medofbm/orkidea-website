<script setup lang="ts">
// Delivery fee preview. Client-side only, never blocks the page, renders nothing if the request fails.
// Prices come from the server; this component only displays them.
interface DeliverySummary {
  mode: 'rates' | 'carrier'
  minPrice?: string
  maxPrice?: string
  cities?: number
  rates?: { city: string, price: string }[]
}
const summary = ref<DeliverySummary | null>(null)
onMounted(async () => {
  try {
    const data = await $fetch<DeliverySummary>('/api/delivery/summary')
    if (data?.mode === 'rates' && data.rates?.length && data.minPrice !== undefined) summary.value = data
    else if (data?.mode === 'carrier') summary.value = data
  } catch {
    summary.value = null
  }
})
</script>

<template>
  <div v-if="summary" class="mt-3 space-y-2 text-sm text-brand-deep">
    <template v-if="summary.mode === 'rates'">
      <p class="flex items-center gap-2">
        <AppIcon name="truck" :size="18" />
        <span>التوصيل من <bdi class="font-bold tabular-nums">{{ formatPrice(summary.minPrice) }}</bdi> د.ل حسب مدينتك</span>
      </p>
      <details class="rounded-lg bg-lavender/60 px-3 py-2">
        <summary class="cursor-pointer font-semibold">عرض المدن</summary>
        <ul class="mt-2 max-h-48 space-y-1 overflow-y-auto">
          <li v-for="rate in summary.rates" :key="rate.city" class="flex justify-between gap-2">
            <span>{{ rate.city }}</span>
            <span class="tabular-nums"><bdi>{{ formatPrice(rate.price) }}</bdi> د.ل</span>
          </li>
        </ul>
      </details>
    </template>
    <p v-else class="flex items-center gap-2">
      <AppIcon name="truck" :size="18" />
      <span>تظهر رسوم التوصيل حسب مدينتك عند إتمام الطلب.</span>
    </p>
    <p class="flex items-center gap-2">
      <AppIcon name="cash" :size="18" />
      <span>الدفع عند الاستلام</span>
    </p>
  </div>
</template>

<script setup lang="ts">
// Delivery fee preview. Client-side only, never blocks the page, renders nothing if the request fails.
// Prices come from the server; this component only displays them.
import type { DeliveryGroup, DeliverySummary } from '~/types/delivery'

const summary = ref<DeliverySummary | null>(null)
onMounted(async () => {
  try {
    const data = await $fetch<DeliverySummary>('/api/delivery/summary')
    if (data?.mode === 'rates' && (data.groups?.length || data.rates?.length) && data.minPrice !== undefined) summary.value = data
    else if (data?.mode === 'carrier') summary.value = data
  } catch {
    summary.value = null
  }
})

// Without `groups` (older API) every rate becomes its own city with a single area.
const groups = computed<DeliveryGroup[]>(() => {
  const data = summary.value
  if (!data || data.mode !== 'rates') return []
  if (data.groups?.length) return data.groups
  return (data.rates ?? []).map(rate => ({
    city: rate.city, minPrice: rate.price, maxPrice: rate.price,
    areas: [{ area: rate.city, price: rate.price }]
  }))
})
const cityCount = computed(() => summary.value?.cities || groups.value.length)

// Arabic-friendly matching: ignore diacritics and unify alef, taa marbuta and alef maqsura forms.
function normalise(text: string) {
  return text.toLowerCase().replace(/[ً-ْـ]/g, '')
    .replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي').replace(/\s+/g, ' ').trim()
}
const query = ref('')
const needle = computed(() => normalise(query.value))
const visible = computed(() => {
  const n = needle.value
  if (!n) return groups.value.map(group => ({ group, areas: group.areas, forced: false }))
  const result: { group: DeliveryGroup, areas: DeliveryGroup['areas'], forced: boolean }[] = []
  for (const group of groups.value) {
    if (normalise(group.city).includes(n)) result.push({ group, areas: group.areas, forced: false })
    else {
      const areas = group.areas.filter(area => normalise(area.area).includes(n))
      if (areas.length) result.push({ group, areas, forced: true })
    }
  }
  return result
})

const open = ref<Record<string, boolean>>({})
const isOpen = (city: string, forced: boolean) => forced || !!open.value[city]
function toggle(city: string) { open.value[city] = !open.value[city] }
// One city, one area: nothing to expand.
const expandable = (group: DeliveryGroup) => group.areas.length > 1 || group.areas[0]?.area !== group.city
const exact = (group: DeliveryGroup) => Number(group.minPrice) === Number(group.maxPrice)

const dialog = ref<HTMLDialogElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const headingId = useId()
function openPanel() {
  query.value = ''
  dialog.value?.showModal()
  document.documentElement.classList.add('overflow-hidden')
}
function closePanel() { dialog.value?.close() }
// Fires for Esc, the close button and backdrop clicks alike.
function onClosed() {
  document.documentElement.classList.remove('overflow-hidden')
  trigger.value?.focus()
}
function onBackdrop(event: MouseEvent) { if (event.target === dialog.value) closePanel() }
onBeforeUnmount(() => { if (import.meta.client) document.documentElement.classList.remove('overflow-hidden') })
const citiesLabel = computed(() => {
  const n = cityCount.value
  if (n === 1) return 'مدينة واحدة'
  if (n === 2) return 'مدينتين'
  return n <= 10 ? `${n} مدن` : `${n} مدينة`
})
</script>

<template>
  <div v-if="summary" class="mt-3 space-y-2 text-sm text-brand-deep">
    <template v-if="summary.mode === 'rates'">
      <p class="flex items-center gap-2">
        <AppIcon name="truck" :size="18" />
        <span>التوصيل من <bdi class="font-bold tabular-nums">{{ formatPrice(summary.minPrice) }}</bdi> د.ل<template v-if="cityCount"> · {{ citiesLabel }}</template></span>
      </p>
      <button ref="trigger" type="button" aria-haspopup="dialog"
        class="inline-flex min-h-11 w-full cursor-pointer items-center justify-between gap-2 rounded-xl border border-brand/30 bg-lavender/60 px-4 font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
        @click="openPanel">
        <span>أسعار التوصيل حسب منطقتك</span>
        <AppIcon name="chevron-left" :size="16" />
      </button>

      <dialog ref="dialog" :aria-labelledby="headingId" class="delivery-dialog" @close="onClosed" @click="onBackdrop">
        <div class="text-ink">
          <header class="sticky top-0 z-10 space-y-3 border-b border-lavender bg-white p-4">
            <div class="flex items-center justify-between gap-3">
              <h2 :id="headingId" class="text-lg font-bold text-brand-deep">أسعار التوصيل</h2>
              <button type="button" aria-label="إغلاق" class="grid size-11 cursor-pointer place-items-center rounded-full text-xl text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-brand" @click="closePanel">
                <span aria-hidden="true">✕</span>
              </button>
            </div>
            <input v-model="query" type="search" class="field block w-full" placeholder="ابحثي عن مدينتك أو منطقتك" aria-label="ابحثي عن مدينتك أو منطقتك" autocomplete="off">
          </header>

          <div class="p-4">
            <ul v-if="visible.length" class="space-y-2">
              <li v-for="item in visible" :key="item.group.city" class="overflow-hidden rounded-xl border border-lavender-deep bg-white">
                <component :is="expandable(item.group) ? 'button' : 'div'" :type="expandable(item.group) ? 'button' : undefined"
                  :aria-expanded="expandable(item.group) ? isOpen(item.group.city, item.forced) : undefined"
                  class="flex min-h-11 w-full items-center justify-between gap-3 px-4 py-2 text-start font-semibold"
                  :class="expandable(item.group) ? 'cursor-pointer hover:bg-lavender/60 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand' : ''"
                  @click="expandable(item.group) && toggle(item.group.city)">
                  <span>{{ item.group.city }}</span>
                  <span class="flex items-center gap-2 text-brand">
                    <span class="tabular-nums">
                      <template v-if="exact(item.group)"><bdi>{{ formatPrice(item.group.minPrice) }}</bdi> د.ل</template>
                      <template v-else>من <bdi>{{ formatPrice(item.group.minPrice) }}</bdi> د.ل</template>
                    </span>
                    <AppIcon v-if="expandable(item.group)" name="chevron-left" :size="16" class="transition-transform" :class="isOpen(item.group.city, item.forced) ? '-rotate-90' : ''" />
                  </span>
                </component>
                <ul v-if="expandable(item.group) && isOpen(item.group.city, item.forced)" class="divide-y divide-lavender border-t border-lavender bg-cream/60">
                  <li v-for="area in item.areas" :key="area.area" class="grid grid-cols-[1fr_auto] gap-3 px-4 py-2">
                    <span>{{ area.area }}</span>
                    <span class="tabular-nums text-ink-soft"><bdi>{{ formatPrice(area.price) }}</bdi> د.ل</span>
                  </li>
                </ul>
              </li>
            </ul>
            <p v-else role="status" class="py-6 text-center text-ink-soft">لا توجد نتائج مطابقة.</p>
            <p class="mt-4 text-xs text-ink-soft">السعر النهائي يظهر عند إتمام الطلب</p>
          </div>
        </div>
      </dialog>
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

<style scoped>
/* Bottom sheet on phones, centred modal from sm up. The dialog itself scrolls; no nested scroll boxes. */
.delivery-dialog {
  position: fixed; inset: auto 0 0 0; margin: 0; width: 100%; max-width: none;
  max-height: 85dvh; overflow-y: auto; overscroll-behavior: contain;
  border: 0; padding: 0; background: #fff;
  border-radius: 1.5rem 1.5rem 0 0; box-shadow: 0 -8px 30px rgb(48 8 26 / 0.18);
}
.delivery-dialog::backdrop { background: rgb(48 8 26 / 0.45); }
@media (min-width: 640px) {
  .delivery-dialog {
    inset: 0; margin: auto; width: min(32rem, calc(100vw - 2rem)); max-height: 80dvh;
    border-radius: 1.5rem; box-shadow: 0 20px 50px rgb(48 8 26 / 0.25);
  }
}
@media (prefers-reduced-motion: no-preference) {
  .delivery-dialog[open] { animation: sheet-in 220ms ease-out; }
  @keyframes sheet-in { from { opacity: 0; transform: translateY(1.5rem); } }
}
</style>

<script setup lang="ts">
// Delivery rate picker for checkout: one field-styled button opens a dialog (bottom sheet on phones).
// Step 1 lists cities, step 2 lists the areas of the chosen city. Rates without regions give one flat list.
// Prices come from the server; this component only displays them and reports the chosen rate id.
interface Rate { id: number, city: string, price: string, total?: string, region?: string | null, area?: string | null }
interface Area { id: number, name: string, price: string }
interface City { name: string, areas: Area[], min: string, max: string }

const props = defineProps<{ rates: Rate[], modelValue: number, error?: string }>()
const emit = defineEmits<{ 'update:modelValue': [id: number], invalid: [] }>()

// Tripoli first, then Arabic alphabetical order.
function arSort(a: string, b: string) {
  const first = (name: string) => normaliseArabic(name).startsWith('طرابلس') ? 0 : 1
  return first(a) - first(b) || a.localeCompare(b, 'ar')
}
// Cheapest first, ties by name (Tripoli first, then Arabic alphabetical).
const byPrice = (a: { name: string, price: string }, b: { name: string, price: string }) =>
  Number(a.price) - Number(b.price) || arSort(a.name, b.name)
function extreme(areas: Area[], pick: 'min' | 'max') {
  return areas.reduce((best, area) => (pick === 'min' ? Number(area.price) < Number(best.price)
    : Number(area.price) > Number(best.price)) ? area : best, areas[0]!).price
}

// Two steps only when every rate has a region and there is more than one region.
const grouped = computed(() => props.rates.length > 0 && props.rates.every(rate => rate.region)
  && new Set(props.rates.map(rate => rate.region)).size > 1)
const cities = computed<City[]>(() => {
  if (!grouped.value) return []
  const map = new Map<string, Area[]>()
  for (const rate of props.rates) {
    const list = map.get(rate.region as string) ?? []
    list.push({ id: rate.id, name: rate.area || rate.city, price: rate.price })
    map.set(rate.region as string, list)
  }
  return [...map.entries()].map(([name, areas]) => ({
    name, areas: areas.sort(byPrice),
    min: extreme(areas, 'min'), max: extreme(areas, 'max')
  })).sort((a, b) => Number(a.min) - Number(b.min) || arSort(a.name, b.name))
})
const flatRows = computed<Area[]>(() => grouped.value ? [] : props.rates
  .map(rate => ({ id: rate.id, name: rate.city, price: rate.price }))
  .sort(byPrice))

const selected = computed(() => props.rates.find(rate => rate.id === props.modelValue))
const selectedText = computed(() => {
  const rate = selected.value
  if (!rate) return ''
  return grouped.value ? `${rate.region} · ${rate.area || rate.city}` : rate.city
})

const query = ref('')
const needle = computed(() => normaliseArabic(query.value))
const activeCity = ref<City | null>(null)
const matches = (text: string) => !needle.value || normaliseArabic(text).includes(needle.value)

const cityRows = computed(() => cities.value.filter(city => matches(city.name)))
// While searching, areas of cities whose own name did not match are listed as «area — city».
const areaHits = computed(() => needle.value ? cities.value
  .filter(city => !matches(city.name))
  .flatMap(city => city.areas.filter(area => matches(area.name)).map(area => ({ ...area, city: city.name })))
  .sort(byPrice) : [])
const areaRows = computed(() => (activeCity.value?.areas ?? []).filter(area => matches(area.name)))
const flatVisible = computed(() => flatRows.value.filter(row => matches(row.name)))
const hasResults = computed(() => {
  if (!grouped.value) return flatVisible.value.length > 0
  if (activeCity.value) return areaRows.value.length > 0
  return cityRows.value.length > 0 || areaHits.value.length > 0
})

const dialog = ref<HTMLDialogElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const expanded = ref(false)
const headingId = useId()
const labelId = useId()
const valueId = useId()
const errorId = useId()

function openPanel() {
  query.value = ''
  activeCity.value = null
  expanded.value = true
  dialog.value?.showModal()
  document.documentElement.classList.add('overflow-hidden')
}
function closePanel() { dialog.value?.close() }
// Fires for Esc, the close button, a backdrop click and a selection alike.
function onClosed() {
  expanded.value = false
  document.documentElement.classList.remove('overflow-hidden')
  trigger.value?.focus()
}
function onBackdrop(event: MouseEvent) { if (event.target === dialog.value) closePanel() }
onBeforeUnmount(() => { if (import.meta.client) document.documentElement.classList.remove('overflow-hidden') })

function pick(id: number) {
  emit('update:modelValue', id)
  closePanel()
}
function scrollTop() { void nextTick(() => { if (dialog.value) dialog.value.scrollTop = 0 }) }
function chooseCity(city: City) {
  // A city with a single delivery area needs no second step.
  if (city.areas.length === 1) return pick(city.areas[0]!.id)
  activeCity.value = city
  query.value = ''
  scrollTop()
}
function backToCities() {
  activeCity.value = null
  query.value = ''
  scrollTop()
}
const isSelected = (id: number) => id === props.modelValue
const baseRow = 'flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-xl px-4 py-2 text-start hover:bg-lavender/60 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand'
const rowClass = (id: number) => [baseRow, isSelected(id) ? 'bg-lavender font-semibold text-brand-deep' : '']
const cityPrice = (city: City) => Number(city.min) === Number(city.max)

// Browsers focus the failing field themselves after this event, so move focus to the button a moment later.
function onInvalid() {
  emit('invalid')
  setTimeout(() => trigger.value?.focus(), 0)
}

defineExpose({ focus: () => trigger.value?.focus() })
</script>

<template>
  <div class="block">
    <span :id="labelId" class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="truck" :size="16" class="text-brand" />التوصيل إلى</span>
    <button ref="trigger" type="button" aria-haspopup="dialog" :aria-expanded="expanded"
      :aria-labelledby="`${labelId} ${valueId}`" :aria-invalid="error ? true : undefined" :aria-describedby="error ? errorId : undefined"
      class="field mt-1 flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 text-start"
      :class="error ? 'border-danger' : ''" @click="openPanel">
      <span :id="valueId" class="flex min-w-0 items-center gap-2" :class="selected ? 'font-semibold text-ink' : 'text-ink-soft'">
        <AppIcon v-if="!selected" name="truck" :size="18" class="shrink-0 text-brand" />
        <span class="truncate">{{ selected ? selectedText : 'اختاري مدينتك ومنطقتك' }}</span>
      </span>
      <span v-if="selected" class="flex shrink-0 items-center gap-2 text-brand">
        <span class="tabular-nums"><bdi>{{ formatPrice(selected.price) }}</bdi> د.ل</span>
        <span class="text-xs font-normal text-ink-soft">تغيير</span>
      </span>
      <AppIcon v-else name="chevron-left" :size="16" class="shrink-0 text-brand" />
    </button>
    <!-- Lets the browser's own form validation stop an empty choice and reach this field first. -->
    <input class="sr-only" tabindex="-1" aria-hidden="true" autocomplete="off" required :value="modelValue || ''" @invalid.prevent="onInvalid">
    <p v-if="error" :id="errorId" role="alert" class="mt-1 text-sm font-medium text-danger">{{ error }}</p>

    <dialog ref="dialog" :aria-labelledby="headingId" class="picker-dialog" @close="onClosed" @click="onBackdrop">
      <div class="text-ink">
        <header class="sticky top-0 z-10 space-y-3 border-b border-lavender bg-white p-4">
          <div class="flex items-center justify-between gap-3">
            <button v-if="activeCity" type="button" class="inline-flex min-h-11 shrink-0 cursor-pointer items-center gap-1 rounded-full pe-3 font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-brand" @click="backToCities">
              <AppIcon name="chevron-left" :size="16" class="rotate-180" />كل المدن
            </button>
            <h2 :id="headingId" class="min-w-0 flex-1 truncate text-lg font-bold text-brand-deep">{{ activeCity ? activeCity.name : 'اختاري مدينتك ومنطقتك' }}</h2>
            <button type="button" aria-label="إغلاق" class="grid size-11 shrink-0 cursor-pointer place-items-center rounded-full text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-brand" @click="closePanel">
              <AppIcon name="x" :size="20" />
            </button>
          </div>
          <input v-model="query" type="search" class="field block w-full" placeholder="ابحثي عن مدينتك أو منطقتك" aria-label="ابحثي عن مدينتك أو منطقتك" autocomplete="off">
        </header>

        <div class="p-3">
          <ul v-if="hasResults" class="space-y-1">
            <template v-if="!grouped">
              <li v-for="row in flatVisible" :key="row.id">
                <button type="button" :class="rowClass(row.id)" :aria-current="isSelected(row.id) || undefined" @click="pick(row.id)">
                  <span class="flex items-center gap-2"><AppIcon v-if="isSelected(row.id)" name="check" :size="18" class="text-brand" />{{ row.name }}</span>
                  <span class="tabular-nums text-brand"><bdi>{{ formatPrice(row.price) }}</bdi> د.ل</span>
                </button>
              </li>
            </template>
            <template v-else-if="activeCity">
              <li v-for="area in areaRows" :key="area.id">
                <button type="button" :class="rowClass(area.id)" :aria-current="isSelected(area.id) || undefined" @click="pick(area.id)">
                  <span class="flex items-center gap-2"><AppIcon v-if="isSelected(area.id)" name="check" :size="18" class="text-brand" />{{ area.name }}</span>
                  <span class="tabular-nums text-brand"><bdi>{{ formatPrice(area.price) }}</bdi> د.ل</span>
                </button>
              </li>
            </template>
            <template v-else>
              <li v-for="city in cityRows" :key="city.name">
                <button type="button" :class="baseRow" @click="chooseCity(city)">
                  <span>{{ city.name }}</span>
                  <span class="flex items-center gap-2 text-brand">
                    <span class="tabular-nums">
                      <template v-if="cityPrice(city)"><bdi>{{ formatPrice(city.min) }}</bdi> د.ل</template>
                      <template v-else>من <bdi>{{ formatPrice(city.min) }}</bdi> د.ل</template>
                    </span>
                    <AppIcon v-if="city.areas.length > 1" name="chevron-left" :size="16" />
                  </span>
                </button>
              </li>
              <li v-for="hit in areaHits" :key="hit.id">
                <button type="button" :class="rowClass(hit.id)" :aria-current="isSelected(hit.id) || undefined" @click="pick(hit.id)">
                  <span class="flex items-center gap-2"><AppIcon v-if="isSelected(hit.id)" name="check" :size="18" class="text-brand" />{{ hit.name }} — {{ hit.city }}</span>
                  <span class="tabular-nums text-brand"><bdi>{{ formatPrice(hit.price) }}</bdi> د.ل</span>
                </button>
              </li>
            </template>
          </ul>
          <p v-else role="status" class="py-6 text-center text-ink-soft">لا توجد نتائج مطابقة.</p>
          <p class="mt-3 px-1 text-xs text-ink-soft">السعر النهائي يظهر عند إتمام الطلب</p>
        </div>
      </div>
    </dialog>
  </div>
</template>

<style scoped>
/* Bottom sheet on phones, centred modal from sm up. The dialog itself scrolls; no nested scroll boxes. */
.picker-dialog {
  position: fixed; inset: auto 0 0 0; margin: 0; width: 100%; max-width: none;
  max-height: 85dvh; overflow-y: auto; overscroll-behavior: contain;
  border: 0; padding: 0; background: #fff;
  border-radius: 1.5rem 1.5rem 0 0; box-shadow: 0 -8px 30px rgb(48 8 26 / 0.18);
}
.picker-dialog::backdrop { background: rgb(48 8 26 / 0.45); }
@media (min-width: 640px) {
  .picker-dialog {
    inset: 0; margin: auto; width: min(32rem, calc(100vw - 2rem)); max-height: 80dvh;
    border-radius: 1.5rem; box-shadow: 0 20px 50px rgb(48 8 26 / 0.25);
  }
}
@media (prefers-reduced-motion: no-preference) {
  .picker-dialog[open] { animation: picker-in 220ms ease-out; }
  @keyframes picker-in { from { opacity: 0; transform: translateY(1.5rem); } }
}
</style>

<script setup lang="ts">
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'أسعار التوصيل' })

type Rate = { id: number, region: string, city: string, price: string }
type RatesPage = { readEnabled: boolean, total: number, rates: Rate[] }
type ImportResult = {
  applied: boolean, cities: number, areas: number, conflicts: number, skippedNoRate: number, skippedPickup: number, surcharge: string,
  sample: { region: string, area: string, price: string }[]
}

const toast = useToast()
const confirm = useConfirm()
const { data, status, error, refresh } = useFetch<RatesPage>('/api/admin/shipping-rates', { server: false, cache: 'no-store' })
const busy = ref(false)
const preview = ref<ImportResult | null>(null)
const previewSurcharge = ref<number | null>(null)
// Empty on purpose: the admin must choose (0 is allowed).
const surchargeInput = ref<number | string | null>(null)
const surcharge = computed(() => {
  const raw = surchargeInput.value
  if (raw === null || raw === '') return null
  const value = Number(raw)
  return Number.isFinite(value) && value >= 0 && value <= 100 && Math.round(value * 100) === value * 100 ? value : null
})
watch(surcharge, () => { preview.value = null; previewSurcharge.value = null })
const search = ref('')

const groups = computed(() => {
  const term = search.value.trim()
  const map = new Map<string, Rate[]>()
  for (const rate of data.value?.rates ?? []) {
    if (term && !rate.region.includes(term) && !rate.city.includes(term)) continue
    const list = map.get(rate.region) ?? []
    list.push(rate)
    map.set(rate.region, list)
  }
  return [...map.entries()].map(([region, rates]) => ({ region, rates }))
})

function areaLabel(rate: Rate) {
  const prefix = rate.region + ' — '
  return rate.city.startsWith(prefix) ? rate.city.slice(prefix.length) : rate.city
}

async function callImport(dryRun: boolean, extra: number) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  const response = await $fetch.raw<ImportResult & { error?: string }>('/api/admin/shipping-rates/import-sabil', {
    method: 'POST', retry: 0, ignoreResponseError: true, query: { dryRun, surcharge: extra },
    headers: { RequestVerificationToken: session.requestToken }
  })
  if (!response.ok || !response._data) throw new Error(response._data?.error || 'تعذر قراءة أسعار درب السبيل الآن.')
  return response._data
}

async function check() {
  const extra = surcharge.value
  if (busy.value || extra === null) return
  busy.value = true
  try {
    preview.value = await callImport(true, extra)
    previewSurcharge.value = extra
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    busy.value = false
  }
}

async function apply() {
  const extra = previewSurcharge.value
  if (busy.value || !preview.value || extra === null) return
  const ok = await confirm({
    title: 'اعتماد أسعار درب السبيل؟',
    message: `ستُستبدل كل أسعار التوصيل الحالية بـ ${preview.value.areas} منطقة في ${preview.value.cities} مدينة. الطلبات السابقة لا تتأثر.`,
    confirmLabel: 'اعتماد الأسعار'
  })
  if (!ok) return
  busy.value = true
  try {
    const result = await callImport(false, extra)
    preview.value = null
    previewSurcharge.value = null
    toast.success(`تم تحديث الأسعار: ${result.areas} منطقة في ${result.cities} مدينة.`)
    await refresh()
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <section class="card p-4 sm:p-5">
      <h2 class="text-lg font-bold">أسعار درب السبيل</h2>
      <p class="mt-1 text-sm text-gray-600">
        الأسعار تُقرأ من حساب المتجر في درب السبيل (التوصيل لباب البيت حسب المدينة والمنطقة) وتظهر للزبونة في صفحة المنتج وعند إتمام الطلب.
        أعيدي الاستيراد كل ما تغيّر درب السبيل أسعارها.
      </p>
      <p v-if="data && !data.readEnabled" class="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
        ربط درب السبيل غير مفعّل على الخادم بعد. لازم إضافة مفتاح الحساب على الخادم أولاً.
      </p>
      <div class="mt-4 max-w-sm">
        <label class="block text-sm font-semibold" for="surcharge">إضافة على كل سعر (د.ل)</label>
        <input id="surcharge" v-model="surchargeInput" type="number" inputmode="decimal" min="0" max="100" step="0.5" class="field mt-1">
        <p class="mt-1 text-xs text-gray-600">خدمة "نساء" في درب السبيل تضيف 10 د.ل على سعر التوصيل. اكتب 10 لو تبعث بخدمة النساء.</p>
      </div>
      <div class="mt-4 flex flex-wrap gap-2">
        <button type="button" class="btn-primary min-h-11 px-4" :disabled="busy || surcharge === null" @click="check">
          {{ busy && !preview ? 'جاري القراءة…' : 'استيراد أسعار درب السبيل' }}
        </button>
      </div>

      <div v-if="preview" class="mt-4 rounded-lg border border-gray-200 p-4">
        <p class="font-semibold">
          جاهزة للاعتماد: {{ preview.areas }} منطقة في {{ preview.cities }} مدينة (مع إضافة {{ formatPrice(preview.surcharge) }} د.ل على كل سعر)
        </p>
        <p v-if="preview.conflicts || preview.skippedNoRate" class="mt-1 text-sm text-gray-600">
          <span v-if="preview.conflicts">{{ preview.conflicts }} منطقة بسعرين مختلفين (اعتمدنا الأعلى). </span>
          <span v-if="preview.skippedNoRate">{{ preview.skippedNoRate }} منطقة بدون سعر توصيل (لم تُضف).</span>
        </p>
        <p v-if="preview.skippedPickup > 0" class="mt-1 text-sm text-gray-600">تم تجاهل {{ preview.skippedPickup }} نقطة استلام من المكتب</p>
        <ul class="mt-3 grid gap-1 text-sm sm:grid-cols-2">
          <li v-for="row in preview.sample" :key="row.region + row.area" class="flex justify-between gap-3 border-b border-gray-100 py-1">
            <span>{{ row.region }} — {{ row.area }}</span>
            <span class="font-semibold">{{ formatPrice(row.price) }} د.ل</span>
          </li>
        </ul>
        <div class="mt-4 flex flex-wrap gap-2">
          <button type="button" class="btn-primary min-h-11 px-4" :disabled="busy" @click="apply">اعتماد الأسعار</button>
          <button type="button" class="btn-secondary min-h-11 px-4" :disabled="busy" @click="preview = null">إلغاء</button>
        </div>
      </div>
    </section>

    <section class="card p-4 sm:p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 class="text-lg font-bold">الأسعار الحالية <span v-if="data" class="text-sm font-normal text-gray-500">({{ data.total }})</span></h2>
        <input v-model="search" type="search" class="field max-w-xs" placeholder="ابحث عن مدينة أو منطقة" aria-label="ابحث عن مدينة أو منطقة">
      </div>
      <p v-if="status === 'pending'" class="mt-4 text-sm text-gray-500">جاري التحميل…</p>
      <p v-else-if="error" class="mt-4 text-sm text-red-700">
        تعذر تحميل الأسعار. <button type="button" class="underline" @click="refresh()">إعادة المحاولة</button>
      </p>
      <p v-else-if="!groups.length" class="mt-4 text-sm text-gray-500">لا توجد أسعار.</p>
      <div v-else class="mt-4 space-y-4">
        <div v-for="group in groups" :key="group.region">
          <h3 class="font-semibold text-brand">{{ group.region }}</h3>
          <ul class="mt-1 grid gap-x-6 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <li v-for="rate in group.rates" :key="rate.id" class="flex justify-between gap-3 border-b border-gray-100 py-1">
              <span>{{ areaLabel(rate) }}</span>
              <span class="font-semibold">{{ formatPrice(rate.price) }} د.ل</span>
            </li>
          </ul>
        </div>
      </div>
    </section>
  </div>
</template>

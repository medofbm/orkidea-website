<script setup lang="ts">
import type { AdminStockDetails } from '~/types/admin'

definePageMeta({ layout: 'admin', middleware: 'admin', key: route => route.path })
useSeoMeta({ title: 'تفاصيل المخزون' })

const url = '/api/admin/inventory/' + useRoute().params.id
const page = ref(1)
const { data, status, error, refresh } = useFetch<AdminStockDetails>(url, {
  query: { page }, server: false, cache: 'no-store'
})
const account = computed(() => data.value?.account)
const unit = ref<'' | 'ml' | 'piece'>('')
const newUnit = ref<'' | 'ml' | 'piece'>('')
const confirmDialog = useConfirm()
// Suggest a unit from the category name; the admin can still change it.
function defaultUnit(category: string | null | undefined): '' | 'ml' | 'piece' {
  if (!category) return ''
  if (category.includes('زيت') || category.includes('زيوت')) return 'ml'
  if (category.includes('صابون')) return 'piece'
  return ''
}
watch(() => data.value?.category, (category) => {
  if (!unit.value && !data.value?.account) unit.value = defaultUnit(category)
}, { immediate: true })
const otherUnit = computed<'ml' | 'piece'>(() => account.value?.unit === 'ml' ? 'piece' : 'ml')
watch(otherUnit, value => { newUnit.value = value }, { immediate: true })
const unsetVariants = computed(() => account.value ? (data.value?.variants.filter(variant => variant.stockUnitsPerItem == null) ?? []) : [])
const step = computed(() => account.value?.unit === 'piece' ? 1 : 0.001)
const delta = ref<number | ''>('')
const reason = ref('')
const variantId = ref<number | ''>('')
const units = ref<number | ''>('')
const saving = ref(false)
const needsRefresh = ref(false)
const notice = ref('')
const failure = ref('')
const disabled = computed(() => saving.value || needsRefresh.value || status.value !== 'success')
const moveKinds: Record<string, string> = {
  Setup: 'تفعيل التتبع', Configure: 'استهلاك حجم', Adjust: 'إضافة أو تصحيح',
  Unit: 'تغيير الوحدة', Reserve: 'حجز', Consume: 'صرف', Release: 'تحرير الحجز'
}

watch(variantId, loadUnits)
function loadUnits() {
  units.value = data.value?.variants.find(variant => variant.productVariantId === variantId.value)?.stockUnitsPerItem ?? ''
}

async function reload() {
  await refresh()
  needsRefresh.value = !!error.value
  if (!needsRefresh.value) loadUnits()
}

async function recover() {
  await reload()
  if (needsRefresh.value) return
  delta.value = ''
  reason.value = ''
  failure.value = ''
  notice.value = 'تم تحديث الرصيد. راجع السجل قبل تسجيل حركة جديدة.'
}

async function save(action: 'open' | 'configure' | 'adjust' | 'unit', input: Record<string, string | number>) {
  if (disabled.value) return
  saving.value = true
  failure.value = ''
  notice.value = ''
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const result = await $fetch.raw<{ success?: boolean, message?: string }>(url + '/' + action, {
      method: 'POST', retry: 0, ignoreResponseError: true, body: input,
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!result.ok || result._data?.success !== true) {
      needsRefresh.value = result.status === 409 || result.status >= 500
      failure.value = result._data?.message ?? 'تعذر حفظ الإجراء. راجع الحقول وحساب الدخول.'
      return
    }
    notice.value = result._data.message ?? 'تم حفظ الإجراء.'
    if (action === 'adjust') {
      delta.value = ''
      reason.value = ''
    }
    await reload()
  } catch {
    needsRefresh.value = true
    failure.value = 'تعذر التحقق من الحفظ. حدّث الرصيد والسجل قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}

function adjust() {
  if (!account.value || disabled.value || typeof delta.value !== 'number' || !Number.isFinite(delta.value) ||
      delta.value === 0 || !reason.value.trim()) return
  void save('adjust', { delta: delta.value, reason: reason.value.trim(),
    expectedRevision: account.value.revision, requestId: crypto.randomUUID() })
}

function configure() {
  if (!account.value || typeof variantId.value !== 'number' ||
      typeof units.value !== 'number' || !Number.isFinite(units.value) || units.value <= 0) return
  void save('configure', { variantId: variantId.value, units: units.value, expectedRevision: account.value.revision })
}

async function changeUnit() {
  if (!account.value || disabled.value || !newUnit.value || newUnit.value === account.value.unit) return
  const from = adminStockUnit(account.value.unit)
  const to = adminStockUnit(newUnit.value)
  const ok = await confirmDialog({
    title: 'تغيير وحدة التتبع؟',
    message: 'ستتغير الوحدة من ' + from + ' إلى ' + to + '. سيُصفَّر الرصيد وتُمسح كمية الاستهلاك لكل حجم، وتحتاج إدخالهما من جديد.',
    confirmLabel: 'تغيير الوحدة', tone: 'danger'
  })
  if (!ok) return
  void save('unit', { unit: newUnit.value, expectedRevision: account.value.revision })
}

// History display helpers: sign, colour and Libya time.
function signed(value: string | number) {
  const n = Number(value)
  return n > 0 ? '+' + value : String(value)
}
function deltaClass(value: string | number) {
  const n = Number(value)
  return n > 0 ? 'text-success' : n < 0 ? 'text-danger' : 'text-ink-soft'
}
const dateFormat = new Intl.DateTimeFormat('ar-LY', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Tripoli' })
function adminDate(iso: string) {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : dateFormat.format(date)
}
</script>

<template>
  <section aria-labelledby="stock-title" class="mx-auto max-w-6xl">
    <NuxtLink to="/admin/inventory" class="mb-3 inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors"><AppIcon name="chevron-right" :size="16" />العودة للمخزون</NuxtLink>
    <h1 id="stock-title" class="mb-5 text-3xl font-bold">مخزون {{ data?.name }}</h1>
    <p v-if="notice" role="status" class="mb-3 text-success">{{ notice }}</p>
    <p v-if="failure" role="alert" class="mb-3 text-danger">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="saving || status === 'pending'" @click="recover"
      class="mb-4 min-h-11 rounded-lg border px-4">تحديث الرصيد والسجل</button>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل تفاصيل المخزون…</p>
    <p v-else-if="error?.statusCode === 404" role="alert">المنتج غير موجود.</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <form v-if="!account" class="card p-4 sm:p-6" @submit.prevent="unit && save('open', { unit })">
        <h2 class="mb-2 text-xl font-bold">تفعيل تتبّع المخزون</h2>
        <p class="mb-3">اختر وحدة القياس. يبدأ الرصيد من صفر، وبعدها سجّل الكمية الموجودة فعلاً.</p>
        <fieldset :disabled="disabled" class="space-y-3">
          <label class="block">وحدة القياس
            <select v-model="unit" required class="field mt-1 block min-h-11">
              <option value="" disabled>اختر الوحدة</option>
              <option value="ml">مل للزيوت السائبة</option>
              <option value="piece">قطعة للمنتجات الجاهزة</option>
            </select>
          </label>
          <button class="min-h-11 btn-primary disabled:opacity-60" type="submit" :disabled="!unit">تفعيل التتبّع</button>
        </fieldset>
      </form>
      <template v-else>
        <AdminStockBalances :account="account" />
        <div class="mt-6 grid items-start gap-6 lg:grid-cols-2">
        <div class="grid gap-6">
        <form class="card p-4 sm:p-6" @submit.prevent="adjust">
          <h2 class="mb-2 text-xl font-bold">إضافة أو تصحيح كمية</h2>
          <p class="mb-3 text-sm">الموجب للإضافة والسالب للنقص. الحد الأدنى للرصيد هو الكمية المحجوزة.</p>
          <fieldset :disabled="disabled" class="space-y-3">
            <label class="block">التغيّر ({{ adminStockUnit(account.unit) }})
              <input v-model.number="delta" type="number" :step="step" min="-1000000000" max="1000000000" required
                class="field mt-1 block min-h-11 w-full sm:w-auto">
            </label>
            <label class="block">سبب التعديل
              <textarea v-model="reason" required maxlength="500" rows="2" class="field mt-1 block w-full" />
            </label>
            <button :disabled="delta === '' || delta === 0 || !reason.trim()" type="submit"
              class="min-h-11 btn-primary disabled:opacity-60">حفظ حركة الكمية</button>
          </fieldset>
        </form>
        <section class="card p-4 sm:p-6" aria-labelledby="stock-unit">
          <h2 id="stock-unit" class="mb-2 text-xl font-bold">تغيير وحدة التتبع</h2>
          <p class="mb-3 text-sm">الوحدة الحالية: <bdi class="font-bold">{{ adminStockUnit(account.unit) }}</bdi></p>
          <p role="note" class="mb-3 rounded-xl border border-warning/40 bg-status-pending p-3 text-sm font-semibold text-warning">
            تنبيه: عند تغيير الوحدة يرجع الرصيد إلى صفر وتُمسح كمية الاستهلاك لكل حجم، ولازم تدخلهما من جديد. الأحجام تتوقف عن الطلب إلى أن تضبط الاستهلاك. ما يمكنش التغيير وفيه كمية محجوزة.
          </p>
          <form @submit.prevent="changeUnit">
            <fieldset :disabled="disabled" class="space-y-3">
              <label class="block">الوحدة الجديدة
                <select v-model="newUnit" required class="field mt-1 block min-h-11">
                  <option :value="otherUnit">{{ adminStockUnit(otherUnit) }}</option>
                </select>
              </label>
              <button type="submit" :disabled="!newUnit || newUnit === account.unit" class="min-h-11 rounded-lg border px-4 disabled:opacity-60">تغيير الوحدة</button>
            </fieldset>
          </form>
        </section>
        <section class="card p-4 sm:p-6" aria-labelledby="stock-variants">
          <h2 id="stock-variants" class="mb-2 text-xl font-bold">استهلاك كل حجم</h2>
          <p class="mb-3 text-sm">اضبط الكمية المستهلكة للوحدة المباعة. الإعداد يسري على الطلبات الجديدة.</p>
          <p v-if="unsetVariants.length" role="alert" class="mb-3 rounded-xl border border-warning/40 bg-status-pending p-3 text-sm font-semibold text-warning">
            لازم تحدد كمية الاستهلاك، الزبونة ما تقدرش تطلب {{ unsetVariants.length > 1 ? 'هذه الأحجام' : 'هذا الحجم' }}.
          </p>
          <p v-if="!data.variants.length">أضف حجم للمنتج قبل ضبط الاستهلاك.</p>
          <form v-else @submit.prevent="configure">
            <ul class="mb-4 grid gap-2 sm:grid-cols-2" aria-label="الاستهلاك الحالي لكل حجم">
              <li v-for="variant in data.variants" :key="variant.productVariantId"
                class="flex flex-wrap items-center justify-between gap-2 rounded-xl border px-3 py-2 text-sm"
                :class="variant.stockUnitsPerItem == null ? 'border-warning bg-status-pending/40' : 'border-lavender-deep'">
                <span class="font-semibold">{{ variant.size }}</span>
                <span v-if="variant.stockUnitsPerItem != null"><bdi class="font-bold">{{ variant.stockUnitsPerItem }}</bdi> {{ adminStockUnit(account.unit) }} للقطعة</span>
                <span v-else class="rounded-full bg-status-pending px-2 py-0.5 text-xs font-bold text-warning">غير مضبوط · غير متاح للطلب</span>
              </li>
            </ul>
            <fieldset :disabled="disabled" class="space-y-3">
              <label class="block">الحجم
                <select v-model="variantId" required class="field mt-1 block min-h-11 w-full sm:w-auto">
                  <option value="" disabled>اختر الحجم</option>
                  <option v-for="variant in data.variants" :key="variant.productVariantId" :value="variant.productVariantId">
                    {{ variant.size }} · {{ variant.stockUnitsPerItem ?? 'غير مضبوط' }}
                  </option>
                </select>
              </label>
              <label v-if="variantId !== ''" class="block">الاستهلاك ({{ adminStockUnit(account.unit) }})
                <input v-model.number="units" type="number" :min="step" :step="step" max="1000000" required
                  class="field mt-1 block min-h-11 w-full sm:w-auto">
              </label>
              <button :disabled="variantId === '' || units === ''" type="submit"
                class="min-h-11 btn-primary disabled:opacity-60">حفظ استهلاك الحجم</button>
            </fieldset>
          </form>
        </section>
        </div>
        <section class="card p-4 sm:p-6" aria-labelledby="stock-history">
          <h2 id="stock-history" class="mb-3 text-xl font-bold">سجل الحركات</h2>
          <p v-if="!data.movements.length">ما فيش حركات مسجّلة.</p>
          <ol v-else class="divide-y divide-lavender-deep">
            <li v-for="move in data.movements" :key="move.stockMovementId" class="py-3">
              <p class="font-semibold">{{ moveKinds[move.kind] ?? move.kind }}</p>
              <p>تغيّر الفعلي <bdi dir="ltr" class="font-semibold" :class="deltaClass(move.onHandDelta)">{{ signed(move.onHandDelta) }}</bdi> · تغيّر المحجوز <bdi dir="ltr" class="font-semibold" :class="deltaClass(move.reservedDelta)">{{ signed(move.reservedDelta) }}</bdi></p>
              <p class="text-sm text-ink-soft">
                {{ move.actorName ?? 'حساب إداري سابق' }} ·
                <time :datetime="move.createdAtUtc">{{ adminDate(move.createdAtUtc) }}</time>
              </p>
              <p class="whitespace-pre-line break-words">{{ move.reason }}</p>
              <NuxtLink v-if="move.orderId" :to="'/admin/orders/' + move.orderId"
                class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors">الطلب <bdi class="ms-1">#{{ move.orderId }}</bdi></NuxtLink>
            </li>
          </ol>
          <nav v-if="data.pages > 1" aria-label="صفحات سجل المخزون" class="mt-4 flex items-center justify-between gap-3">
            <button type="button" :disabled="disabled || data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
              @click="page = data.page - 1">السابق</button>
            <span>{{ data.page }} من {{ data.pages }}</span>
            <button type="button" :disabled="disabled || data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
              @click="page = data.page + 1">التالي</button>
          </nav>
        </section>
        </div>
      </template>
    </template>
  </section>
</template>

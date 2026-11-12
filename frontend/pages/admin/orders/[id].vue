<script setup lang="ts">
import type { AdminOrderDetails } from '~/types/admin'

definePageMeta({ layout: 'admin', key: route => route.path })
useSeoMeta({ title: 'تفاصيل الطلب' })

const route = useRoute()
const returnQuery = computed(() => {
  const { status, source, page } = route.query
  return {
    status: typeof status === 'string' && ['pending', 'processing', 'shipped', 'delivered', 'cancelled'].includes(status) ? status : undefined,
    source: typeof source === 'string' && ['Website', 'FacebookManual', 'LegacyUnknown'].includes(source) ? source : undefined,
    page: typeof page === 'string' && /^[1-9][0-9]*$/.test(page) && Number.isSafeInteger(Number(page)) &&
      Number(page) <= 2147483647 ? page : undefined
  }
})
const orderUrl = '/api/admin/orders/' + route.params.id
const { data, error, status, refresh } = useFetch<AdminOrderDetails>(orderUrl, {
  server: false, cache: 'no-store'
})
const statuses = ['Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled']
const selectedStatus = ref('')
const reason = ref('')
const saving = ref(false)
const needsRefresh = ref(false)
const notice = ref('')
const failure = ref('')
const tracked = computed(() => data.value?.stockState !== 'Untracked')
const editable = computed(() => {
  const order = data.value
  return !!order && statuses.includes(order.order.status) && !order.hasDelivery &&
    !(tracked.value && (order.stockState === 'Cancelled' || order.order.status === 'Cancelled'))
})
const allowedStatuses = computed(() => statuses.filter(value =>
  !tracked.value || value === data.value?.order.status || value === 'Cancelled' ||
  (value === 'Processing' && ['Reserved', 'Filled'].includes(data.value?.stockState ?? ''))))

watch(data, order => {
  if (!order) return
  selectedStatus.value = order.order.status
  reason.value = ''
}, { immediate: true })

async function refreshOrder() {
  await refresh()
  needsRefresh.value = !!error.value
}

async function updateStatus() {
  const order = data.value
  if (!order || !editable.value || selectedStatus.value === order.order.status) return

  await updateOrder('status', {
    status: selectedStatus.value,
    reason: selectedStatus.value === 'Cancelled' ? reason.value.trim() || null : null
  })
}

async function updateOrder(action: 'status' | 'stock' | 'delivery', input: Record<string, string | boolean | null>) {
  const order = data.value
  if (!order || saving.value || needsRefresh.value) return

  saving.value = true
  failure.value = ''
  notice.value = ''
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ success?: boolean, message?: string }>(orderUrl + '/' + action, {
      method: 'POST',
      headers: { RequestVerificationToken: session.requestToken },
      body: { ...input, expectedRevision: order.statusRevision },
      retry: 0,
      ignoreResponseError: true
    })
    if (!response.ok || response._data?.success !== true) {
      needsRefresh.value = response.status === 409 || response.status >= 500
      failure.value = response._data?.message ?? 'تعذّر حفظ الإجراء. راجع البيانات المدخلة.'
      return
    }
    notice.value = response._data.message ?? 'تم حفظ الإجراء.'
    await refreshOrder()
  } catch {
    needsRefresh.value = true
    failure.value = 'تعذّر التحقق من نتيجة الحفظ. حدّث تفاصيل الطلب قبل إعادة المحاولة.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section aria-labelledby="order-title" class="mx-auto max-w-3xl">
    <nav class="mb-3 flex flex-wrap gap-4" aria-label="تنقل الطلب">
      <NuxtLink :to="{ path: '/admin/orders', query: returnQuery }" class="inline-flex min-h-11 items-center text-purple-900 underline">الطلبات</NuxtLink>
      <NuxtLink to="/admin/delivery" class="inline-flex min-h-11 items-center text-purple-900 underline">التوصيل والرواجع</NuxtLink>
    </nav>
    <h1 id="order-title" class="mb-6 text-3xl font-bold">تفاصيل الطلب <span v-if="data">#{{ data.order.id }}</span></h1>
    <p v-if="notice" class="mb-3 text-emerald-800" role="status">{{ notice }}</p>
    <p v-if="failure" class="mb-3 text-red-800" role="alert">{{ failure }}</p>
    <button v-if="needsRefresh" type="button" :disabled="status === 'pending'" @click="refreshOrder"
      class="mb-4 min-h-11 rounded-lg border border-purple-900 px-4 text-purple-900">
      تحديث تفاصيل الطلب
    </button>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل التفاصيل…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <dl class="grid gap-4 rounded-xl bg-white p-4 sm:grid-cols-2">
        <div><dt class="font-semibold">الزبون</dt><dd>{{ data.order.customerName }}</dd></div>
        <div><dt class="font-semibold">الحالة</dt><dd>{{ adminOrderStatus(data.order.status) }}</dd></div>
        <div><dt class="font-semibold">الهاتف</dt><dd><bdi>{{ data.whatsAppNumber }}</bdi></dd></div>
        <div><dt class="font-semibold">المصدر</dt><dd>{{ data.order.source }}</dd></div>
        <div class="sm:col-span-2"><dt class="font-semibold">وجهة التسليم</dt>
          <dd class="whitespace-pre-line">{{ data.order.city }} — {{ data.shippingAddress }}</dd>
          <dd v-if="data.carrierChoice" class="mt-1 text-sm">
            {{ data.carrierChoice.area }}
            <template v-if="data.carrierChoice.serviceKind"> · {{ data.carrierChoice.serviceKind === 'women' ? 'توصيل نسائي' : 'توصيل رجالي' }}</template>
          </dd>
        </div>
      </dl>
      <h2 class="mb-3 mt-6 text-xl font-bold">المنتجات</h2>
      <ul class="space-y-2">
        <li v-for="item in data.items" :key="item.id" class="rounded-lg bg-white p-3">
          <span class="font-semibold">{{ item.productName ?? ('منتج #' + item.productId) }}</span>
          <span v-if="item.variantSize"> · {{ item.variantSize }}</span>
          <p class="mt-1">{{ item.quantity }} × {{ item.unitPrice }} د.ل</p>
        </li>
      </ul>
      <dl class="mt-6 space-y-2 rounded-xl bg-purple-50 p-4">
        <div class="flex justify-between"><dt>المنتجات</dt><dd>{{ data.productsTotal }} د.ل</dd></div>
        <div class="flex justify-between"><dt>التوصيل</dt><dd>{{ data.order.shippingFee }} د.ل</dd></div>
        <div class="flex justify-between font-bold"><dt>الإجمالي</dt><dd>{{ data.order.total }} د.ل</dd></div>
      </dl>
      <AdminOrderFulfillment :key="data.statusRevision" :order="data" :disabled="saving || needsRefresh"
        @stock="operation => updateOrder('stock', { operation })"
        @delivery="input => updateOrder('delivery', input)" />

      <section aria-labelledby="order-status-title" class="mt-6 rounded-xl bg-white p-4">
        <h2 id="order-status-title" class="mb-3 text-xl font-bold">تحديث الحالة</h2>
        <p v-if="data.hasDelivery">تحديث الحالة يتم من إجراءات التوصيل بعد تسجيل الشحنة.</p>
        <p v-else-if="!editable">الحالة الحالية لا تسمح بالتعديل من هنا.</p>
        <form v-else @submit.prevent="updateStatus">
          <p v-if="tracked" class="mb-3 text-sm text-slate-700">
            الحجز والتعبئة والشحن لها إجراءاتها الخاصة في إدارة الطلب.
          </p>
          <fieldset :disabled="saving || needsRefresh" class="space-y-3">
            <label class="block">
              <span class="block font-semibold">الحالة الجديدة</span>
              <select v-model="selectedStatus" class="min-h-11 w-full rounded-lg border border-slate-400 p-2 sm:w-auto">
                <option v-for="value in allowedStatuses" :key="value" :value="value">{{ adminOrderStatus(value) }}</option>
              </select>
            </label>
            <label v-if="selectedStatus === 'Cancelled' && selectedStatus !== data.order.status" class="block">
              <span class="block font-semibold">سبب الإلغاء {{ tracked ? '(مطلوب)' : '(اختياري)' }}</span>
              <textarea v-model="reason" :required="tracked" maxlength="500" rows="3"
                class="w-full rounded-lg border border-slate-400 p-2" />
            </label>
            <button type="submit" :disabled="selectedStatus === data.order.status || (tracked && selectedStatus === 'Cancelled' && !reason.trim())"
              class="min-h-11 rounded-lg bg-purple-900 px-4 font-semibold text-white disabled:opacity-60">
              {{ saving ? 'جاري الحفظ…' : 'حفظ الحالة' }}
            </button>
          </fieldset>
        </form>
      </section>

      <section aria-labelledby="order-history-title" class="mt-6 rounded-xl bg-white p-4">
        <h2 id="order-history-title" class="mb-3 text-xl font-bold">سجل الحالة</h2>
        <p v-if="!data.history.length">لا توجد تغييرات حالة مسجلة لهذا الطلب.</p>
        <ol v-else class="divide-y divide-slate-200">
          <li v-for="change in data.history" :key="change.revision" class="py-3">
            <p>من {{ adminOrderStatus(change.previousStatus) }} إلى {{ adminOrderStatus(change.newStatus) }}</p>
            <p class="text-sm text-slate-700">
              <bdi>{{ change.actorName ?? 'مدير المتجر' }}</bdi> ·
              <time :datetime="change.changedAtUtc" dir="ltr">{{ change.changedAtUtc.replace('T', ' ').slice(0, 16) }} UTC</time>
            </p>
            <p v-if="change.reason" class="whitespace-pre-line break-words">{{ change.reason }}</p>
          </li>
        </ol>
        <p v-if="data.history.length === 50" class="mt-2 text-sm text-slate-700">يعرض السجل آخر 50 تغييراً.</p>
      </section>
    </template>
  </section>
</template>

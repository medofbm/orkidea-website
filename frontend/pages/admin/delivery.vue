<script setup lang="ts">
import type { AdminDeliveriesPage } from '~/types/admin'

definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'التوصيل والرواجع' })

const states = ['HandedOver', 'Returning', 'Returned', 'Delivered']
const fields = reactive({ state: 'active', search: '' })
const filters = ref({ ...fields, page: 1 })
const { data, status, error, refresh } = useFetch<AdminDeliveriesPage>('/api/admin/delivery', {
  query: filters, server: false, cache: 'no-store'
})

function apply() {
  filters.value = { state: fields.state, search: fields.search.trim(), page: 1 }
}

function reset() {
  fields.state = 'active'
  fields.search = ''
  apply()
}

function deliveryBadge(state: string) {
  if (state === 'Delivered') return 'badge-status-delivered'
  if (state === 'Returned') return 'badge-status-cancelled'
  if (state === 'Returning') return 'badge-status-pending'
  return 'badge-status-shipped'
}

function changePage(page: number) {
  filters.value = { ...filters.value, page }
}
</script>

<template>
  <section aria-labelledby="delivery-title" class="mx-auto max-w-6xl">
    <h1 id="delivery-title" class="text-3xl font-bold">التوصيل والرواجع</h1>
    <p class="mb-6 mt-2 text-ink-soft">تابع الشحنات المفتوحة والرواجع. تسوية المحفظة في تطبيق شركة التوصيل.</p>

    <form class="card mb-6 flex flex-wrap items-end gap-3 p-4 sm:p-6" @submit.prevent="apply">
      <label class="min-w-52 flex-1">
        <span class="block font-semibold">بحث عن شحنة</span>
        <input v-model="fields.search" type="search" maxlength="100"
          placeholder="رقم الطلب أو العميل أو المدينة أو المرجع أو الشركة"
          class="mt-1 block min-h-11 w-full field">
      </label>
      <label class="w-full sm:w-auto">
        <span class="block font-semibold">حالة التوصيل</span>
        <select v-model="fields.state" class="mt-1 min-h-11 w-full field">
          <option value="active">الشحنات المفتوحة</option>
          <option value="all">كل الشحنات</option>
          <option v-for="state in states" :key="state" :value="state">{{ adminDeliveryState(state) }}</option>
        </select>
      </label>
      <div class="flex w-full gap-3 sm:w-auto">
        <button type="submit" class="min-h-11 flex-1 btn-primary sm:flex-none">بحث</button>
        <button type="button" class="min-h-11 flex-1 btn-secondary sm:flex-none" @click="reset">
          إعادة ضبط
        </button>
      </div>
    </form>

    <p v-if="status === 'idle' || status === 'pending'" role="status" class="text-ink-soft">جاري تحميل الشحنات…</p>
    <template v-else-if="error">
      <p v-if="error.statusCode === 400" role="alert">راجع حالة التوصيل أو نص البحث وأعد المحاولة.</p>
      <AdminReadError v-else :status-code="error.statusCode" />
      <button v-if="!error.statusCode || error.statusCode >= 500" type="button"
        class="mt-3 min-h-11 btn-secondary" @click="refresh()">إعادة المحاولة</button>
    </template>
    <template v-else-if="data">
      <p class="mb-3 font-semibold text-ink-soft" role="status">{{ data.total }} شحنة مطابقة</p>
      <div v-if="!data.items.length" class="card flex flex-col items-center p-6 text-center sm:p-10">
        <span class="flex size-16 items-center justify-center rounded-full bg-lavender text-brand" aria-hidden="true">
          <AppIcon name="truck" :size="28" />
        </span>
        <p class="mt-4 max-w-md">
          لا توجد شحنات مطابقة. تقدر تسجّل الشحنة من تفاصيل الطلب بعد تجهيزه.
        </p>
        <NuxtLink to="/admin/orders?status=processing" class="mt-4 inline-flex min-h-11 items-center btn-secondary">
          الطلبات قيد التجهيز
        </NuxtLink>
      </div>
      <ul v-else class="grid gap-3 lg:grid-cols-2">
        <li v-for="shipment in data.items" :key="shipment.orderId" class="card min-w-0 p-4 sm:p-6">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <NuxtLink :to="'/admin/orders/' + shipment.orderId"
              class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors font-bold">
              الطلب <bdi class="ms-1">#{{ shipment.orderId }}</bdi>
            </NuxtLink>
            <span class="badge" :class="deliveryBadge(shipment.state)">
              {{ adminDeliveryState(shipment.state) }}
            </span>
          </div>
          <dl class="mt-2 space-y-2 text-sm">
            <div class="flex flex-wrap gap-x-2"><dt class="font-semibold">العميل</dt>
              <dd class="break-words">{{ shipment.customerName }} · {{ shipment.city }}</dd></div>
            <div class="flex flex-wrap gap-x-2"><dt class="font-semibold">جهة التوصيل</dt>
              <dd class="break-words">{{ shipment.mode === 'Self' ? 'توصيل ذاتي' : shipment.carrierName }}</dd></div>
            <div v-if="shipment.mode === 'Company'" class="flex flex-wrap gap-x-2"><dt class="font-semibold">تسليم الطلب للشركة</dt>
              <dd>{{ adminHandoffMode(shipment.handoffMode) }}</dd></div>
            <div v-if="shipment.shipmentReference" class="flex flex-wrap gap-x-2"><dt class="font-semibold">مرجع الشحنة</dt>
              <dd class="break-all"><bdi>{{ shipment.shipmentReference }}</bdi></dd></div>
            <div class="flex flex-wrap gap-x-2 text-ink-soft"><dt class="font-semibold">آخر تحديث</dt>
              <dd><time :datetime="shipment.updatedAtUtc">{{ adminOrderDate(shipment.updatedAtUtc) }}</time></dd></div>
          </dl>
        </li>
      </ul>
      <nav v-if="data.pages > 1" class="mt-5 flex flex-wrap items-center justify-between gap-3" aria-label="صفحات الشحنات">
        <button type="button" :disabled="data.page <= 1" class="min-h-11 btn-secondary disabled:opacity-50"
          @click="changePage(data.page - 1)">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="data.page >= data.pages" class="min-h-11 btn-secondary disabled:opacity-50"
          @click="changePage(data.page + 1)">التالي</button>
      </nav>
    </template>
  </section>
</template>

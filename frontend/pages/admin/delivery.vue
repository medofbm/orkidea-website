<script setup lang="ts">
import type { AdminDeliveriesPage } from '~/types/admin'

definePageMeta({ layout: 'admin' })
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

function changePage(page: number) {
  filters.value = { ...filters.value, page }
}
</script>

<template>
  <section aria-labelledby="delivery-title">
    <h1 id="delivery-title" class="text-3xl font-bold">التوصيل والرواجع</h1>
    <p class="mb-6 mt-2 text-slate-700">تابع الشحنات المفتوحة والرواجع. تسوية المحفظة في تطبيق شركة التوصيل.</p>

    <form class="mb-6 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4" @submit.prevent="apply">
      <label class="min-w-52 flex-1">
        <span class="block font-semibold">بحث عن شحنة</span>
        <input v-model="fields.search" type="search" maxlength="100"
          placeholder="رقم الطلب أو العميل أو المدينة أو المرجع أو الشركة"
          class="mt-1 block min-h-11 w-full rounded-lg border border-slate-400 px-3">
      </label>
      <label class="w-full sm:w-auto">
        <span class="block font-semibold">حالة التوصيل</span>
        <select v-model="fields.state" class="mt-1 min-h-11 w-full rounded-lg border border-slate-400 px-3">
          <option value="active">الشحنات المفتوحة</option>
          <option value="all">كل الشحنات</option>
          <option v-for="state in states" :key="state" :value="state">{{ adminDeliveryState(state) }}</option>
        </select>
      </label>
      <button type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 font-semibold text-white">بحث</button>
      <button type="button" class="min-h-11 rounded-lg border border-purple-900 px-4 text-purple-900" @click="reset">
        إعادة ضبط
      </button>
    </form>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الشحنات…</p>
    <template v-else-if="error">
      <p v-if="error.statusCode === 400" role="alert">راجع حالة التوصيل أو نص البحث وأعد المحاولة.</p>
      <AdminReadError v-else :status-code="error.statusCode" />
      <button v-if="!error.statusCode || error.statusCode >= 500" type="button"
        class="mt-3 min-h-11 rounded-lg border px-4" @click="refresh()">إعادة المحاولة</button>
    </template>
    <template v-else-if="data">
      <p class="mb-3" role="status">{{ data.total }} شحنة مطابقة</p>
      <p v-if="!data.items.length" class="rounded-xl bg-white p-4">
        لا توجد شحنات مطابقة. تقدر تسجّل الشحنة من تفاصيل الطلب بعد تجهيزه.
      </p>
      <ul v-else class="grid gap-3 lg:grid-cols-2">
        <li v-for="shipment in data.items" :key="shipment.orderId" class="min-w-0 rounded-xl bg-white p-4">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <NuxtLink :to="'/admin/orders/' + shipment.orderId"
              class="inline-flex min-h-11 items-center font-bold text-purple-900 underline">
              الطلب <bdi class="ms-1">#{{ shipment.orderId }}</bdi>
            </NuxtLink>
            <span class="rounded-full bg-purple-50 px-3 py-1 text-sm font-semibold text-purple-950">
              {{ adminDeliveryState(shipment.state) }}
            </span>
          </div>
          <dl class="mt-2 space-y-2 text-sm">
            <div class="flex flex-wrap gap-x-2"><dt class="font-semibold">العميل</dt>
              <dd class="break-words">{{ shipment.customerName }} · {{ shipment.city }}</dd></div>
            <div class="flex flex-wrap gap-x-2"><dt class="font-semibold">جهة التوصيل</dt>
              <dd class="break-words">{{ shipment.mode === 'Self' ? 'توصيل ذاتي' : shipment.carrierName }}</dd></div>
            <div v-if="shipment.shipmentReference" class="flex flex-wrap gap-x-2"><dt class="font-semibold">مرجع الشحنة</dt>
              <dd class="break-all"><bdi>{{ shipment.shipmentReference }}</bdi></dd></div>
            <div class="flex flex-wrap gap-x-2"><dt class="font-semibold">آخر تحديث</dt>
              <dd><time :datetime="shipment.updatedAtUtc" dir="ltr">{{ shipment.updatedAtUtc.replace('T', ' ').slice(0, 16) }} UTC</time></dd></div>
          </dl>
        </li>
      </ul>
      <nav v-if="data.pages > 1" class="mt-5 flex flex-wrap items-center justify-between gap-3" aria-label="صفحات الشحنات">
        <button type="button" :disabled="data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="changePage(data.page - 1)">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="changePage(data.page + 1)">التالي</button>
      </nav>
    </template>
  </section>
</template>

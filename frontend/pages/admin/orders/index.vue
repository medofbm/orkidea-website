<script setup lang="ts">
import type { AdminOrdersPage } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'طلبات أوركيديا' })

const fields = reactive({ status: 'all', search: '', source: 'all' })
const filters = ref({ ...fields, page: 1 })
const { data, error, status } = useFetch<AdminOrdersPage>('/api/admin/orders', {
  query: filters, server: false, cache: 'no-store'
})
function apply() {
  filters.value = { ...fields, search: fields.search.trim(), page: 1 }
}
function changePage(page: number) {
  filters.value = { ...filters.value, page }
}
</script>

<template>
  <section aria-labelledby="orders-title">
    <h1 id="orders-title" class="mb-6 text-3xl font-bold">الطلبات</h1>
    <form class="mb-6 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4" @submit.prevent="apply">
      <label class="min-w-40 flex-1">البحث
        <input v-model="fields.search" type="search" maxlength="100" placeholder="رقم الطلب أو اسم الزبون أو المدينة"
          class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <label>الحالة
        <select v-model="fields.status" class="mt-1 block min-h-11 rounded-lg border px-3">
          <option value="all">كل الحالات</option>
          <option value="pending">جديد</option>
          <option value="processing">قيد التجهيز</option>
          <option value="shipped">قيد التوصيل</option>
          <option value="delivered">مسلّم</option>
          <option value="cancelled">ملغي</option>
        </select>
      </label>
      <label>المصدر
        <select v-model="fields.source" class="mt-1 block min-h-11 rounded-lg border px-3">
          <option value="all">كل المصادر</option>
          <option value="Website">الموقع</option>
          <option value="FacebookManual">فيسبوك</option>
          <option value="LegacyUnknown">طلب قديم</option>
        </select>
      </label>
      <button type="submit" class="min-h-11 rounded-lg bg-purple-800 px-4 text-white">بحث</button>
    </form>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الطلبات…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <p class="mb-3">{{ data.total }} طلب</p>
      <AdminOrderList :orders="data.items" />
      <nav v-if="data.pages > 1" class="mt-5 flex flex-wrap items-center justify-between gap-3" aria-label="صفحات الطلبات">
        <button type="button" :disabled="data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="changePage(data.page - 1)">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="changePage(data.page + 1)">التالي</button>
      </nav>
    </template>
  </section>
</template>

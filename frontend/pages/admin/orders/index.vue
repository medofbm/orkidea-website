<script setup lang="ts">
import type { AdminOrdersPage } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'طلبات أوركيديا' })

const route = useRoute()
const router = useRouter()
const statuses = {
  all: 'كل الحالات', pending: 'جديد', processing: 'قيد التجهيز',
  shipped: 'قيد التوصيل', delivered: 'مسلّم', cancelled: 'ملغي'
}
const sources = { all: 'كل المصادر', Website: 'الموقع', FacebookManual: 'فيسبوك', LegacyUnknown: 'طلب قديم' }

function filterValue(value: unknown, choices: Record<string, string>) {
  return typeof value === 'string' && Object.hasOwn(choices, value) ? value : 'all'
}

const routeFilters = computed(() => {
  const value = route.query.page
  const page = typeof value === 'string' && /^[1-9][0-9]*$/.test(value) ? Number(value) : 1
  return {
    status: filterValue(route.query.status, statuses),
    source: filterValue(route.query.source, sources),
    page: Number.isSafeInteger(page) && page <= 2147483647 ? page : 1
  }
})
const fields = reactive({ status: routeFilters.value.status, search: '', source: routeFilters.value.source })
// Customer names and phone numbers stay out of navigational URLs and browser history.
const appliedSearch = ref('')
watch(() => routeFilters.value.status, value => { fields.status = value })
watch(() => routeFilters.value.source, value => { fields.source = value })
const filters = computed(() => ({ ...routeFilters.value, search: appliedSearch.value }))
const { data, error, status } = useFetch<AdminOrdersPage>('/api/admin/orders', {
  query: filters, server: false, cache: 'no-store'
})
const exportUrl = computed(() => {
  const { status, search, source } = filters.value
  return '/api/admin/orders/export?' + new URLSearchParams({ status, search, source })
})
function goTo(status: string, source: string, page: number) {
  return router.push({
    query: {
      status: status === 'all' ? undefined : status,
      source: source === 'all' ? undefined : source,
      page: page > 1 ? String(page) : undefined
    }
  })
}
async function apply() {
  await goTo(fields.status, fields.source, 1)
  appliedSearch.value = fields.search.trim()
}
function changePage(page: number) {
  const { status, source } = routeFilters.value
  return goTo(status, source, page)
}
</script>

<template>
  <section aria-labelledby="orders-title">
    <div class="mb-6 flex flex-wrap items-center justify-between gap-3">
      <h1 id="orders-title" class="text-3xl font-bold">الطلبات</h1>
      <NuxtLink to="/admin/orders/manual" class="inline-flex min-h-11 items-center rounded-lg bg-purple-800 px-4 text-white">
        تسجيل طلب من فيسبوك
      </NuxtLink>
    </div>
    <form class="mb-6 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4" @submit.prevent="apply">
      <label class="min-w-40 flex-1">البحث
        <input v-model="fields.search" type="search" maxlength="100" placeholder="رقم الطلب أو اسم الزبون أو المدينة"
          class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <label>الحالة
        <select v-model="fields.status" class="mt-1 block min-h-11 rounded-lg border px-3">
          <option v-for="(label, value) in statuses" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <label>المصدر
        <select v-model="fields.source" class="mt-1 block min-h-11 rounded-lg border px-3">
          <option v-for="(label, value) in sources" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <button type="submit" class="min-h-11 rounded-lg bg-purple-800 px-4 text-white">بحث</button>
    </form>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الطلبات…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
        <p>{{ data.total }} طلب</p>
        <a v-if="data.total > 0" :href="exportUrl" class="inline-flex min-h-11 items-center rounded-lg border px-4">
          تصدير نتائج البحث إلى Excel
        </a>
      </div>
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

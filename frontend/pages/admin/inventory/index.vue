<script setup lang="ts">
import type { AdminStockPage } from '~/types/admin'

definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'المخزون والتعبئة' })

const search = ref('')
const filters = ref({ search: '', page: 1 })
const { data, status, error } = useFetch<AdminStockPage>('/api/admin/inventory', {
  query: filters, server: false, cache: 'no-store'
})

function apply() {
  filters.value = { search: search.value.trim(), page: 1 }
}
</script>

<template>
  <section aria-labelledby="inventory-title">
    <h1 id="inventory-title" class="mb-2 text-3xl font-bold">المخزون والتعبئة</h1>
    <p class="mb-6">راجع الرصيد واضبط استهلاك الأحجام من تفاصيل كل منتج.</p>
    <form class="mb-6 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4" @submit.prevent="apply">
      <label class="min-w-40 flex-1">اسم المنتج
        <input v-model="search" type="search" maxlength="100" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <button type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 text-white">بحث</button>
    </form>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل المخزون…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <p class="mb-3" role="status">{{ data.total ? data.total + ' منتج' : 'ما فيش منتجات مطابقة.' }}</p>
      <ul class="grid gap-3 lg:grid-cols-2">
        <li v-for="product in data.items" :key="product.productId" class="min-w-0 rounded-xl bg-white p-4">
          <NuxtLink :to="'/admin/inventory/' + product.productId"
            class="mb-2 inline-flex min-h-11 items-center break-words font-bold text-purple-900 underline">
            {{ product.name }}
          </NuxtLink>
          <AdminStockBalances v-if="product.account" :account="product.account" />
          <p v-else>تتبّع المخزون غير مفعّل لهذا المنتج.</p>
        </li>
      </ul>
      <nav v-if="data.pages > 1" aria-label="صفحات المخزون" class="mt-5 flex items-center justify-between gap-3">
        <button type="button" :disabled="data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="filters.page = data.page - 1">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="filters.page = data.page + 1">التالي</button>
      </nav>
    </template>
  </section>
</template>

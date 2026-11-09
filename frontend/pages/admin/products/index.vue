<script setup lang="ts">
import type { AdminProductsPage } from '~/types/admin'

definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'إدارة المنتجات' })

const search = ref('')
const query = ref({ search: '', page: 1 })
const { data, status, error } = useFetch<AdminProductsPage>('/api/admin/products', {
  query, server: false, cache: 'no-store'
})
</script>

<template>
  <section aria-labelledby="products-title">
    <div class="mb-5 flex flex-wrap items-center justify-between gap-3">
      <h1 id="products-title" class="text-3xl font-bold">إدارة المنتجات</h1>
      <div class="flex flex-wrap gap-2">
        <NuxtLink to="/admin/categories" class="inline-flex min-h-11 items-center rounded-lg border px-4">إدارة الفئات</NuxtLink>
        <NuxtLink to="/admin/products/new" class="inline-flex min-h-11 items-center rounded-lg bg-purple-900 px-4 text-white">إضافة منتج</NuxtLink>
      </div>
    </div>
    <form class="mb-5 flex flex-wrap items-end gap-3 rounded-xl bg-white p-4"
      @submit.prevent="query = { search: search.trim(), page: 1 }">
      <label class="min-w-40 flex-1">اسم المنتج
        <input v-model="search" type="search" maxlength="100" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <button type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 text-white">بحث</button>
    </form>
    <p v-if="status === 'pending' || status === 'idle'" role="status">جاري تحميل المنتجات…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <p class="mb-3" role="status">{{ data.total ? data.total + ' منتج' : 'ما فيش منتجات مطابقة.' }}</p>
      <ul class="grid gap-3 sm:grid-cols-2">
        <li v-for="product in data.items" :key="product.productId" class="rounded-xl bg-white p-4">
          <NuxtLink :to="'/admin/products/' + product.productId"
            class="inline-flex min-h-11 items-center font-semibold text-purple-900 underline">{{ product.name }}</NuxtLink>
          <p class="text-sm">{{ product.category ?? 'بدون فئة' }} · {{ product.variants }} حجم</p>
        </li>
      </ul>
      <nav v-if="data.pages > 1" class="mt-4 flex items-center justify-between gap-3" aria-label="صفحات المنتجات">
        <button type="button" :disabled="data.page <= 1" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="query.page = data.page - 1">السابق</button>
        <span>{{ data.page }} من {{ data.pages }}</span>
        <button type="button" :disabled="data.page >= data.pages" class="min-h-11 rounded-lg border px-4 disabled:opacity-50"
          @click="query.page = data.page + 1">التالي</button>
      </nav>
    </template>
  </section>
</template>

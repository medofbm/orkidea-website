<script setup lang="ts">
import type { CataloguePage } from '~/types/catalogue'
const route = useRoute()
const page = computed(() => Math.max(1, Number(route.query.page) || 1))
const { data, status, error, refresh } = await useFetch<CataloguePage>(
  () => useRuntimeConfig().public.apiBase + '/products',
  { query: { page } }
)
useSeoMeta({ title: 'منتجات أوركيديا', description: 'تصفّح منتجات أوركيديا والأحجام المتاحة.' })
</script>

<template>
  <section aria-labelledby="catalogue-heading">
    <h1 id="catalogue-heading" class="mb-6 text-3xl font-bold">منتجات أوركيديا</h1>
    <p v-if="status === 'pending'" role="status">جاري تحميل المنتجات…</p>
    <div v-else-if="error" role="alert" class="rounded-xl border border-red-200 p-4">
      <p>تعذر تحميل المنتجات.</p>
      <button class="mt-3 min-h-11 rounded-lg bg-purple-800 px-4 text-white" @click="refresh()">إعادة المحاولة</button>
    </div>
    <template v-else>
      <p v-if="!data?.items.length">لا توجد منتجات لعرضها حاليًا.</p>
      <div class="grid grid-cols-2 gap-4 md:grid-cols-3">
        <NuxtLink v-for="product in data?.items" :key="product.id" :to="'/products/' + product.id"
          class="overflow-hidden rounded-2xl border border-purple-100 bg-white p-3">
          <img v-if="product.images[0]" :src="product.images[0]" :alt="product.name"
            class="aspect-square w-full object-contain" loading="lazy" width="320" height="320">
          <h2 class="mt-3 font-semibold">{{ product.name }}</h2>
          <p v-if="product.variants[0]" class="mt-2 text-purple-900">من {{ product.variants[0].price }} د.ل</p>
        </NuxtLink>
      </div>
      <nav v-if="data && data.total > data.pageSize" class="mt-8 flex justify-between" aria-label="صفحات المنتجات">
        <NuxtLink v-if="page > 1" :to="{ query: { page: page - 1 } }">السابق</NuxtLink>
        <NuxtLink v-if="page * data.pageSize < data.total" :to="{ query: { page: page + 1 } }">التالي</NuxtLink>
      </nav>
    </template>
  </section>
</template>

<script setup lang="ts">
import type { AdminDashboard } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'لوحة إدارة أوركيديا' })
const { data, error, status } = useFetch<AdminDashboard>('/api/admin/dashboard', { server: false, cache: 'no-store' })
</script>

<template>
  <section aria-labelledby="dashboard-title">
    <h1 id="dashboard-title" class="mb-6 text-3xl font-bold">لوحة الإدارة</h1>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الملخص…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <dl class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded-xl bg-white p-4">
          <dt>قيمة المنتجات المطلوبة خلال 30 يومًا</dt>
          <dd class="mt-2 text-xl font-bold">{{ data.productsValueLast30Days }} د.ل</dd>
        </div>
        <div class="rounded-xl bg-white p-4">
          <dt>طلبات جديدة</dt><dd class="mt-2 text-xl font-bold">{{ data.pendingOrders }}</dd>
        </div>
        <div class="rounded-xl bg-white p-4">
          <dt>منتجات بدون أحجام</dt><dd class="mt-2 text-xl font-bold">{{ data.productsWithoutVariants }}</dd>
        </div>
        <div class="rounded-xl bg-white p-4">
          <dt>الحسابات</dt><dd class="mt-2 text-xl font-bold">{{ data.accounts }}</dd>
        </div>
      </dl>
      <div class="mb-3 mt-8 flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-xl font-semibold">أحدث الطلبات</h2>
        <NuxtLink to="/admin/orders" class="min-h-11 py-2 text-purple-900 underline">كل الطلبات</NuxtLink>
      </div>
      <AdminOrderList :orders="data.recentOrders" />
    </template>
  </section>
</template>

<script setup lang="ts">
import type { AdminOrderDetails } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'تفاصيل الطلب' })
const route = useRoute()
const { data, error, status } = useFetch<AdminOrderDetails>(() => '/api/admin/orders/' + route.params.id, {
  server: false, cache: 'no-store'
})
</script>

<template>
  <section aria-labelledby="order-title" class="mx-auto max-w-3xl">
    <NuxtLink to="/admin/orders" class="mb-3 inline-block min-h-11 text-purple-900 underline">رجوع للطلبات</NuxtLink>
    <h1 id="order-title" class="mb-6 text-3xl font-bold">تفاصيل الطلب <span v-if="data">#{{ data.order.id }}</span></h1>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل التفاصيل…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <dl class="grid gap-4 rounded-xl bg-white p-4 sm:grid-cols-2">
        <div><dt class="font-semibold">الزبون</dt><dd>{{ data.order.customerName }}</dd></div>
        <div><dt class="font-semibold">الحالة</dt><dd>{{ adminOrderStatus(data.order.status) }}</dd></div>
        <div><dt class="font-semibold">الهاتف</dt><dd><bdi>{{ data.whatsAppNumber }}</bdi></dd></div>
        <div><dt class="font-semibold">المصدر</dt><dd>{{ data.order.source }}</dd></div>
        <div class="sm:col-span-2"><dt class="font-semibold">عنوان التوصيل</dt>
          <dd class="whitespace-pre-line">{{ data.order.city }} — {{ data.shippingAddress }}</dd>
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
    </template>
  </section>
</template>

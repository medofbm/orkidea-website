<script setup lang="ts">
import type { CheckoutReceipt } from '~/types/checkout'

const receipt = ref<CheckoutReceipt | null>(null)
const loading = ref(true)
onMounted(async () => {
  try { receipt.value = await $fetch<CheckoutReceipt>('/api/checkout/receipt') }
  catch { receipt.value = null }
  finally { loading.value = false }
})
useSeoMeta({ title: 'إيصال الطلب', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section class="mx-auto max-w-lg rounded-xl bg-white p-6">
    <h1 class="mb-4 text-2xl font-bold">إيصال الطلب</h1>
    <p v-if="loading" role="status">جاري تحميل الإيصال…</p>
    <template v-else-if="receipt">
      <p class="font-semibold">طلب رقم {{ receipt.orderId }}</p>
      <p class="mt-2">{{ receipt.statusLabel }}</p>
      <dl class="my-4 space-y-2">
        <div class="flex justify-between"><dt>القطع</dt><dd>{{ receipt.quantity }}</dd></div>
        <div class="flex justify-between"><dt>المنتجات</dt><dd>{{ receipt.productsTotal }} د.ل</dd></div>
        <div class="flex justify-between"><dt>التوصيل</dt><dd>{{ receipt.shippingFee }} د.ل</dd></div>
        <div class="flex justify-between font-bold"><dt>الإجمالي</dt><dd>{{ receipt.total }} د.ل</dd></div>
      </dl>
      <p>{{ receipt.paymentMethod }}</p>
    </template>
    <p v-else role="alert">الإيصال غير متاح في الجلسة الحالية.</p>
    <NuxtLink to="/" class="mt-5 inline-block min-h-11 text-purple-900 underline">متابعة التسوق</NuxtLink>
  </section>
</template>

<script setup lang="ts">
import type { CheckoutReceipt } from '~/types/checkout'

const receipt = ref<CheckoutReceipt | null>(null)
const loading = ref(true)
const failed = ref(false)

async function loadReceipt() {
  loading.value = true
  failed.value = false
  try {
    receipt.value = await $fetch<CheckoutReceipt>('/api/checkout/receipt')
  } catch (error) {
    receipt.value = null
    failed.value = !(error instanceof Error && 'statusCode' in error && error.statusCode === 404)
  } finally {
    loading.value = false
  }
}

onMounted(loadReceipt)
useSeoMeta({ title: 'إيصال الطلب', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section aria-labelledby="receipt-heading" class="mx-auto max-w-3xl">
    <h1 id="receipt-heading" class="mb-5 text-3xl font-bold text-purple-950 sm:text-4xl">إيصال الطلب</h1>
    <p v-if="loading" role="status" class="rounded-2xl border border-purple-100 bg-white p-5 text-purple-900">جاري تحميل الإيصال…</p>
    <template v-else-if="receipt">
      <div class="rounded-2xl border border-purple-100 bg-white p-4 shadow-sm sm:p-6">
        <p role="status" class="mb-4 font-semibold text-purple-800">تم استلام طلبك</p>
        <div class="grid gap-4 sm:grid-cols-2">
          <section aria-label="بيانات الطلب" class="min-w-0 rounded-xl bg-purple-50 p-4 text-purple-950">
            <h2 class="text-xl font-bold wrap-anywhere">طلب رقم <bdi>{{ receipt.orderId }}</bdi></h2>
            <dl class="mt-4 space-y-3 text-sm">
              <div class="flex flex-wrap justify-between gap-2"><dt>حالة الطلب</dt><dd class="font-semibold">{{ receipt.statusLabel }}</dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>القطع</dt><dd><bdi>{{ receipt.quantity }}</bdi></dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>طريقة الدفع</dt><dd>{{ receipt.paymentMethod }}</dd></div>
            </dl>
          </section>
          <section aria-labelledby="receipt-summary" class="min-w-0 rounded-xl border border-purple-100 p-4 text-purple-950">
            <h2 id="receipt-summary" class="mb-4 text-lg font-bold">ملخص المبلغ</h2>
            <dl class="space-y-3">
              <div class="flex flex-wrap justify-between gap-2"><dt>المنتجات</dt><dd><bdi>{{ receipt.productsTotal }}</bdi> د.ل</dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>التوصيل</dt><dd><bdi>{{ receipt.shippingFee }}</bdi> د.ل</dd></div>
              <div class="flex flex-wrap justify-between gap-2 border-t border-purple-100 pt-3 text-lg font-bold">
                <dt>الإجمالي</dt><dd><bdi>{{ receipt.total }}</bdi> د.ل</dd>
              </div>
            </dl>
          </section>
        </div>
      </div>
    </template>
    <div v-else-if="failed" class="rounded-2xl border border-red-200 bg-white p-5">
      <p role="alert">تعذر تحميل الإيصال. حاولي فتحه مرة أخرى.</p>
      <button type="button" class="mt-3 min-h-11 rounded-xl border border-purple-300 bg-white px-4 font-semibold text-purple-950 hover:bg-purple-50"
        @click="loadReceipt">إعادة المحاولة</button>
    </div>
    <p v-else role="status" class="rounded-2xl border border-purple-100 bg-white p-5">الإيصال غير متاح في الجلسة الحالية.</p>
    <NuxtLink to="/" class="storefront-action mt-5 inline-flex min-h-11 items-center rounded-xl bg-purple-900 px-5 font-bold text-white hover:bg-purple-800">
      متابعة التسوق
    </NuxtLink>
  </section>
</template>

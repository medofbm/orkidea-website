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
  <section aria-labelledby="receipt-heading" class="relative mx-auto max-w-3xl">
    <div aria-hidden="true" class="pointer-events-none absolute -top-6 -z-10 h-72 w-full rounded-3xl bg-gradient-hero" />
    <p v-if="loading" role="status" class="card-glass mb-5 p-5 text-brand-deep">جاري تحميل الإيصال…</p>
    <template v-else-if="receipt">
      <div class="mb-6 flex flex-col items-center pt-4 text-center">
        <div class="relative grid size-24 place-items-center">
          <span aria-hidden="true" class="absolute inset-0 rounded-full bg-brand/30 motion-safe:animate-confirm-ring" />
          <span class="relative grid size-24 place-items-center rounded-full bg-gradient-brand text-white shadow-lift motion-safe:animate-confirm-bounce">
            <AppIcon name="check" :size="48" />
          </span>
        </div>
        <h1 id="receipt-heading" class="mt-5 text-3xl font-bold text-ink sm:text-4xl">إيصال الطلب</h1>
        <p role="status" class="mt-1 font-semibold text-brand-deep">تم استلام طلبك</p>
        <span class="badge mt-3"><AppIcon name="cash" :size="14" /> الدفع عند الاستلام</span>
      </div>
      <div class="card-glass animate-fade-in-up p-4 sm:p-6">
        <div class="grid gap-4 sm:grid-cols-2">
          <section aria-label="بيانات الطلب" class="min-w-0 rounded-2xl bg-cream p-4 text-ink">
            <h2 class="text-xl font-bold wrap-anywhere">طلب رقم <bdi>{{ receipt.orderId }}</bdi></h2>
            <dl class="mt-4 space-y-3 text-sm">
              <div class="flex flex-wrap justify-between gap-2"><dt>حالة الطلب</dt><dd class="font-semibold">{{ receipt.statusLabel }}</dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>القطع</dt><dd><bdi>{{ receipt.quantity }}</bdi></dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>طريقة الدفع</dt><dd>{{ receipt.paymentMethod }}</dd></div>
            </dl>
          </section>
          <section aria-labelledby="receipt-summary" class="min-w-0 rounded-2xl border border-lavender-deep bg-white p-4 text-ink">
            <h2 id="receipt-summary" class="mb-4 text-lg font-bold">ملخص المبلغ</h2>
            <dl class="space-y-3">
              <div class="flex flex-wrap justify-between gap-2"><dt>المنتجات</dt><dd class="font-semibold"><bdi>{{ receipt.productsTotal }}</bdi> د.ل</dd></div>
              <div class="flex flex-wrap justify-between gap-2"><dt>التوصيل</dt><dd class="font-semibold"><bdi>{{ receipt.shippingFee }}</bdi> د.ل</dd></div>
              <div class="flex flex-wrap justify-between gap-2 border-t border-lavender-deep pt-3 text-lg font-bold">
                <dt>الإجمالي</dt><dd class="price"><bdi>{{ receipt.total }}</bdi> د.ل</dd>
              </div>
            </dl>
          </section>
        </div>
      </div>
    </template>
    <template v-else>
      <h1 id="receipt-heading" class="mb-5 text-3xl font-bold text-ink sm:text-4xl">إيصال الطلب</h1>
      <div v-if="failed" class="card-glass border-danger/30 p-5">
        <p role="alert" class="text-danger">تعذر تحميل الإيصال. حاولي فتحه مرة أخرى.</p>
        <button type="button" class="btn-secondary mt-3" @click="loadReceipt">إعادة المحاولة</button>
      </div>
      <p v-else role="status" class="card-glass p-5">الإيصال غير متاح في الجلسة الحالية.</p>
    </template>
    <div class="mt-6 flex justify-center">
      <NuxtLink to="/" class="storefront-action btn-primary">
        تسوقي من جديد
      </NuxtLink>
    </div>
  </section>
</template>

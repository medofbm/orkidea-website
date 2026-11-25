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

// Order help goes through WhatsApp with the order number filled in; hidden when no number is configured.
const whatsappDigits = String(useRuntimeConfig().public.whatsappNumber ?? '').replace(/\D/g, '')
const whatsappLink = computed(() => whatsappDigits && receipt.value
  ? 'https://wa.me/' + whatsappDigits + '?text=' + encodeURIComponent('السلام عليكم، عندي سؤال على طلب رقم ' + receipt.value.orderId)
  : '')
const copied = ref(false)
async function copyOrderId() {
  if (!receipt.value) return
  try {
    await navigator.clipboard.writeText(String(receipt.value.orderId))
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  } catch {
    copied.value = false
  }
}
const nextSteps = [
  { icon: 'shield', title: 'احتفظي برقم الطلب', text: 'تحتاجيه لو بغيتي تسألي على طلبك أو تعدّليه.' },
  { icon: 'truck', title: 'نجهزوا طلبك ونوصلوه', text: 'حسب الوجهة وطريقة التوصيل اللي اخترتيها.' },
  { icon: 'cash', title: 'الدفع عند الاستلام', text: 'تدفعي لما يوصلك الطلب، بدون دفع مسبق.' }
] as const

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
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h2 class="text-xl font-bold wrap-anywhere">طلب رقم <bdi>{{ receipt.orderId }}</bdi></h2>
              <button type="button" class="btn-ghost min-h-11 gap-1.5 text-sm" @click="copyOrderId">
                <AppIcon v-if="copied" name="check" :size="16" />{{ copied ? 'تم النسخ' : 'نسخ الرقم' }}
              </button>
            </div>
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
        <section v-if="receipt.lines?.length" aria-labelledby="receipt-lines" class="mt-4 min-w-0 rounded-2xl bg-white p-4 text-ink">
          <h2 id="receipt-lines" class="mb-3 text-lg font-bold">منتجات طلبك</h2>
          <ul class="divide-y divide-lavender-deep">
            <li v-for="(line, index) in receipt.lines" :key="index" class="flex min-h-11 flex-wrap items-center justify-between gap-2 py-3">
              <span class="min-w-0 flex-1">
                <span class="block font-semibold wrap-anywhere">{{ line.productName }}</span>
                <span class="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-soft">
                  <span v-if="line.size" class="chip">{{ line.size }}</span>
                  <span>{{ 'الكمية ' + line.quantity }}</span>
                </span>
              </span>
              <span class="font-semibold">{{ line.subtotal + ' د.ل' }}</span>
            </li>
          </ul>
        </section>
      </div>
      <section aria-labelledby="next-steps" class="card-glass mt-5 p-4 sm:p-6">
        <h2 id="next-steps" class="mb-4 text-lg font-bold text-ink">شن الخطوة الجاية؟</h2>
        <ol class="grid gap-3 sm:grid-cols-3">
          <li v-for="(step, index) in nextSteps" :key="step.title" class="flex gap-3 rounded-2xl bg-white p-4 sm:flex-col">
            <span class="grid size-10 shrink-0 place-items-center rounded-full bg-lavender text-brand" aria-hidden="true"><AppIcon :name="step.icon" :size="20" /></span>
            <span>
              <span class="block font-bold text-ink"><span class="sr-only">{{ index + 1 }}. </span>{{ step.title }}</span>
              <span class="text-sm text-ink-soft">{{ step.text }}</span>
            </span>
          </li>
        </ol>
        <div class="mt-4 flex flex-wrap gap-3">
          <a v-if="whatsappLink" :href="whatsappLink" target="_blank" rel="noopener noreferrer" class="btn-secondary gap-2">
            <AppIcon name="whatsapp" :size="18" />سؤال على طلبك؟ كلّمينا
          </a>
          <NuxtLink to="/order-and-delivery" class="btn-ghost underline underline-offset-4">سياسة الطلب والتوصيل</NuxtLink>
        </div>
      </section>
      <OrderRatingCard />
    </template>
    <template v-else>
      <h1 id="receipt-heading" class="mb-5 text-3xl font-bold text-ink sm:text-4xl">إيصال الطلب</h1>
      <div v-if="failed" class="card-glass border-danger/30 p-5">
        <p role="alert" class="text-danger">تعذر تحميل الإيصال. حاولي فتحه مرة أخرى.</p>
        <button type="button" class="btn-secondary mt-3" @click="loadReceipt">إعادة المحاولة</button>
      </div>
      <div v-else role="status" class="card-glass p-5">
        <p class="font-semibold text-ink">الإيصال يظهر على نفس الجهاز بعد إتمام الطلب مباشرة.</p>
        <p class="mt-1 text-sm text-ink-soft">لو عندك سؤال على طلب سابق، تواصلي معنا واذكري رقم الطلب.</p>
        <NuxtLink to="/#contact" class="btn-secondary mt-3">تواصلي معنا</NuxtLink>
      </div>
    </template>
    <div class="mt-6 flex justify-center">
      <NuxtLink to="/" class="storefront-action btn-primary">
        تسوقي من جديد
      </NuxtLink>
    </div>
  </section>
</template>

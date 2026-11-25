<script setup lang="ts">
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'تسجيل طلب من فيسبوك' })

interface Options {
  variants: { id: number; product: string; size: string; price: string }[]
  rates: { id: number; city: string; price: string }[]
}
interface Review {
  reviewToken: string
  message?: string
  city: string
  shippingCost: string
  productsTotal: string
  total: string
  lines: { productName: string; size: string; quantity: number; unitPrice: string }[]
}

const { data: options, error: optionsError, status } = useFetch<Options>('/api/admin/orders/manual', {
  server: false, cache: 'no-store'
})
const fields = reactive({ customerName: '', phone: '', address: '', shippingRateId: '' as number | '' })
let nextRowId = 0
const newRow = () => ({ key: nextRowId++, variantId: '' as number | '', quantity: 1 })
const lines = ref([newRow()])
const review = ref<Review>()
const savedId = ref<number>()
const pending = ref(false)
const error = ref('')
const feedback = ref<HTMLElement | null>(null)
const customerNameInput = ref<HTMLInputElement | null>(null)
let requestId: string | undefined

function input() {
  requestId ??= crypto.randomUUID()
  return { ...fields, requestId, lines: lines.value.map(({ variantId, quantity }) => ({ variantId, quantity })) }
}

async function submit() {
  if (pending.value || savedId.value) return
  const current = review.value
  pending.value = true
  error.value = ''
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const result = await $fetch<Review | { orderId: number }>(
      '/api/admin/orders/manual/' + (current ? 'confirm' : 'review'), {
        method: 'POST', retry: 0,
        headers: { RequestVerificationToken: session.requestToken },
        body: current ? { input: input(), reviewToken: current.reviewToken } : input()
      }
    )
    if ('orderId' in result) savedId.value = result.orderId
    else review.value = result
  } catch (failure) {
    const response = failure as { data?: { message?: string }; statusCode?: number }
    error.value = response.data?.message ?? (response.statusCode === 401 || response.statusCode === 403
      ? 'انتهت جلسة الإدارة أو ما عندكش صلاحية. سجّل الدخول وراجع الطلب.'
      : current ? 'تعذر تأكيد الحفظ. أعد المحاولة من نفس المعاينة.' : 'تعذرت مراجعة الطلب. تأكد من الحقول وحاول من جديد.')
  } finally {
    pending.value = false
    await nextTick()
    feedback.value?.focus()
  }
}

async function edit() {
  review.value = undefined
  error.value = ''
  await nextTick()
  customerNameInput.value?.focus()
}
</script>

<template>
  <section aria-labelledby="manual-order-title" class="mx-auto max-w-6xl">
    <NuxtLink to="/admin/orders" class="mb-3 inline-flex min-h-11 items-center text-brand underline">العودة للطلبات</NuxtLink>
    <h1 id="manual-order-title" class="mb-4 text-3xl font-bold">تسجيل طلب من فيسبوك</h1>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل المنتجات ومدن التوصيل…</p>
    <AdminReadError v-else-if="optionsError" :status-code="optionsError.statusCode" />
    <template v-else-if="options">
      <p v-if="error" ref="feedback" role="alert" tabindex="-1" class="mb-4 rounded-lg bg-status-cancelled p-3 text-danger">{{ error }}</p>
      <div v-if="savedId" class="card mx-auto max-w-2xl p-4 sm:p-6">
        <h2 ref="feedback" tabindex="-1" class="mb-3 text-xl font-bold text-success">تم تسجيل الطلب رقم {{ savedId }}</h2>
        <p>الطلب جديد وبانتظار مراجعته وتأكيد توفر المنتجات.</p>
        <NuxtLink :to="`/admin/orders/${savedId}`" class="mt-4 inline-flex min-h-11 w-full items-center justify-center btn-primary sm:w-auto">
          فتح تفاصيل الطلب
        </NuxtLink>
      </div>
      <form v-else-if="review" class="card mx-auto max-w-2xl space-y-4 p-4 sm:p-6" @submit.prevent="submit">
        <h2 :ref="error ? undefined : 'feedback'" tabindex="-1" class="text-xl font-bold">مراجعة الطلب</h2>
        <p v-if="review.message" role="status" class="rounded-lg bg-status-pending p-3">{{ review.message }}</p>
        <div class="space-y-1 rounded-xl bg-lavender p-3">
          <p class="font-semibold">{{ fields.customerName }} · {{ review.city }}</p>
          <p>{{ fields.address }}</p>
          <p dir="ltr" class="text-right">{{ fields.phone }}</p>
        </div>
        <ul class="divide-y">
          <li v-for="(line, index) in review.lines" :key="index" class="py-2">
            {{ line.productName }} · {{ line.size }} · {{ line.quantity }} × {{ line.unitPrice }} د.ل
          </li>
        </ul>
        <dl class="grid grid-cols-2 gap-2 border-t pt-3">
          <dt>المنتجات</dt><dd>{{ review.productsTotal }} د.ل</dd>
          <dt>التوصيل</dt><dd>{{ review.shippingCost }} د.ل</dd>
          <dt class="font-bold">الإجمالي</dt><dd class="font-bold">{{ review.total }} د.ل</dd>
        </dl>
        <div class="flex flex-col gap-3 sm:flex-row">
          <button type="submit" :disabled="pending" class="min-h-11 w-full btn-primary disabled:opacity-50 sm:w-auto">حفظ الطلب</button>
          <button type="button" :disabled="pending" class="min-h-11 w-full btn-secondary sm:w-auto" @click="edit">تعديل البيانات</button>
        </div>
      </form>
      <form v-else @submit.prevent="submit">
        <p v-if="!options.variants.length || !options.rates.length" class="mb-4 rounded-lg bg-status-pending p-3">أضف حجماً بسعر صالح ومدينة توصيل قبل تسجيل طلب.</p>
        <fieldset :disabled="pending || !options.variants.length || !options.rates.length" class="grid gap-4 lg:grid-cols-2 lg:items-start">
          <div class="card space-y-4 p-4 sm:p-6">
            <h2 class="text-lg font-bold">بيانات الزبون</h2>
            <label class="block font-semibold">اسم الزبون
              <input ref="customerNameInput" v-model.trim="fields.customerName" required maxlength="100"
                class="field mt-1 block min-h-11 w-full font-normal">
            </label>
            <label class="block font-semibold">رقم الهاتف
              <input v-model.trim="fields.phone" required type="tel" maxlength="20" dir="ltr" class="field mt-1 block min-h-11 w-full font-normal">
            </label>
            <label class="block font-semibold">عنوان التوصيل
              <textarea v-model.trim="fields.address" required maxlength="200" rows="2" class="field mt-1 block w-full font-normal"></textarea>
            </label>
            <label class="block font-semibold">مدينة التوصيل
              <select v-model="fields.shippingRateId" required class="field mt-1 block min-h-11 w-full font-normal">
                <option value="" disabled>اختر المدينة</option>
                <option v-for="rate in options.rates" :key="rate.id" :value="rate.id">{{ rate.city }} — {{ rate.price }} د.ل</option>
              </select>
            </label>
          </div>
          <div class="card space-y-4 p-4 sm:p-6 lg:sticky lg:top-20">
            <fieldset class="space-y-3">
              <legend class="mb-1 text-lg font-bold">بنود الطلب</legend>
              <div v-for="(line, index) in lines" :key="line.key" class="grid gap-2 rounded-xl bg-lavender p-3 sm:grid-cols-[minmax(0,1fr)_6rem_auto] sm:items-end">
                <label class="min-w-0 font-semibold">المنتج والحجم {{ index + 1 }}
                  <select v-model="line.variantId" required class="field mt-1 block min-h-11 w-full font-normal">
                    <option value="" disabled>اختر المنتج والحجم</option>
                    <option v-for="variant in options.variants" :key="variant.id" :value="variant.id">
                      {{ variant.product }} — {{ variant.size }} — {{ variant.price }} د.ل
                    </option>
                  </select>
                </label>
                <label class="font-semibold">الكمية {{ index + 1 }}
                  <input v-model.number="line.quantity" required type="number" min="1" max="99" class="field mt-1 block min-h-11 w-full font-normal">
                </label>
                <button type="button" :disabled="lines.length === 1" :aria-label="`إزالة البند ${index + 1}`"
                  class="min-h-11 btn-ghost text-danger disabled:opacity-50" @click="lines.splice(index, 1)">إزالة</button>
              </div>
              <button type="button" :disabled="lines.length >= 30" class="min-h-11 w-full btn-secondary disabled:opacity-50 sm:w-auto"
                @click="lines.push(newRow())">إضافة بند</button>
            </fieldset>
            <div class="space-y-3 border-t pt-4">
              <p class="text-sm text-ink-soft">سيتم احتساب الأسعار والتوصيل من الخادم عند مراجعة الطلب.</p>
              <button type="submit" class="min-h-11 w-full btn-primary disabled:opacity-50">مراجعة الطلب</button>
            </div>
          </div>
        </fieldset>
      </form>
    </template>
  </section>
</template>

<script setup lang="ts">
import type { CheckoutReceipt, CheckoutState } from '~/types/checkout'

const router = useRouter()
const { cart } = useCart()
const state = ref<CheckoutState | null>(null)
const loading = ref(true)
const busy = ref(false)
const errors = ref<string[]>([])
const fields = reactive({ firstName: '', lastName: '', whatsAppNumber: '', shippingAddress: '',
  shippingRateId: 0, carrierDestination: '' })
const rate = computed(() => state.value?.shippingRates.find(item => item.id === fields.shippingRateId))
const shippingFee = computed(() => state.value?.destinations ? state.value.shippingFee : rate.value?.price)
const total = computed(() => state.value?.destinations ? state.value.total : rate.value?.total)

watch(() => fields.carrierDestination, () => {
  if (state.value) {
    state.value.quoteToken = null
    state.value.shippingFee = null
    state.value.total = null
  }
})

async function load() {
  loading.value = true
  errors.value = []
  try {
    state.value = await $fetch<CheckoutState>('/api/checkout')
  } catch {
    errors.value = ['تعذر فتح إتمام الطلب. راجعي السلة وحاولي مرة أخرى.']
  } finally {
    loading.value = false
  }
}

async function send(action: 'shipping' | 'orders') {
  const checkout = state.value
  if (!checkout || busy.value || (action === 'orders' && shippingFee.value == null)) return
  busy.value = true
  errors.value = []
  const input = { ...fields, checkoutToken: checkout.checkoutToken, quoteToken: checkout.quoteToken }
  const body = action === 'shipping' ? input :
    { input, review: { cartRevision: checkout.cartRevision, shippingFee: shippingFee.value } }
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<CheckoutState | CheckoutReceipt | { message?: string }>(
      '/api/checkout/' + action, {
        method: 'POST', body, retry: 0, ignoreResponseError: true,
        headers: { RequestVerificationToken: session.requestToken }
      })
    const result = typeof response._data === 'object' ? response._data : undefined
    if (result && 'orderId' in result && response.ok) {
      cart.value = null
      await router.push('/checkout/receipt')
    } else if (result && 'cartRevision' in result) {
      state.value = result
      errors.value = result.errors
    } else {
      errors.value = [result && 'message' in result && result.message
        ? result.message : 'تعذر إتمام العملية. راجعي السلة وحاولي مرة أخرى.']
    }
  } catch {
    errors.value = ['انقطع الاتصال. حاولي مرة أخرى من نفس الصفحة للتأكد من النتيجة.']
  } finally {
    busy.value = false
  }
}

onMounted(load)
useSeoMeta({ title: 'إتمام الطلب', robots: 'noindex, nofollow' })
</script>

<template>
  <section aria-labelledby="checkout-heading">
    <NuxtLink to="/cart" class="mb-3 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-purple-900 hover:bg-purple-50">
      <span aria-hidden="true">→</span> رجوع للسلة
    </NuxtLink>
    <h1 id="checkout-heading" class="mb-5 text-3xl font-bold text-purple-950 sm:text-4xl">إتمام الطلب</h1>
    <p v-if="loading" role="status">جاري تجهيز الطلب…</p>
    <ul v-if="errors.length" role="alert" class="mb-4 list-inside list-disc rounded-xl bg-red-50 p-4 text-red-800">
      <li v-for="(error, index) in errors" :key="index">{{ error }}</li>
    </ul>
    <button v-if="!state && !loading && errors.length" type="button"
      class="min-h-11 rounded-xl bg-purple-900 px-4 font-semibold text-white" @click="load">إعادة المحاولة</button>
    <form v-if="state" class="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,0.65fr)]"
      @submit.prevent="send('orders')">
      <section aria-labelledby="delivery-heading" class="min-w-0 rounded-2xl border border-purple-100 bg-white p-4 sm:p-6">
        <h2 id="delivery-heading" class="mb-4 text-lg font-bold text-purple-950">بيانات التوصيل</h2>
        <fieldset :disabled="busy" aria-labelledby="delivery-heading" class="min-w-0 space-y-4">
          <div class="grid gap-4 sm:grid-cols-2">
            <label class="block">الاسم الأول
              <input v-model="fields.firstName" required maxlength="50" autocomplete="given-name" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
            </label>
            <label class="block">اسم العائلة
              <input v-model="fields.lastName" required maxlength="50" autocomplete="family-name" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
            </label>
          </div>
          <label class="block">رقم الواتساب
            <input v-model="fields.whatsAppNumber" required maxlength="20" type="tel" dir="ltr" autocomplete="tel" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
          </label>
          <label class="block">العنوان بالتفصيل
            <textarea v-model="fields.shippingAddress" required maxlength="200" autocomplete="street-address" rows="3" class="mt-1 block w-full rounded-lg border p-3" />
          </label>
          <template v-if="state.destinations">
            <label class="block">المدينة والمنطقة
              <select v-model="fields.carrierDestination" required class="mt-1 block min-h-11 w-full rounded-lg border px-3">
                <option value="" disabled>اختاري المنطقة</option>
                <option v-for="destination in state.destinations" :key="destination.key" :value="destination.key">
                  {{ destination.city }} — {{ destination.area }}
                </option>
              </select>
            </label>
            <button type="button" :disabled="!fields.carrierDestination" class="min-h-11 rounded-xl border border-purple-300 px-4 font-semibold text-purple-950 hover:bg-purple-50 disabled:opacity-50" @click="send('shipping')">احسبي التوصيل</button>
          </template>
          <label v-else class="block">مدينة التوصيل
            <select v-model.number="fields.shippingRateId" required class="mt-1 block min-h-11 w-full rounded-lg border px-3">
              <option :value="0" disabled>اختاري المدينة</option>
              <option v-for="shippingRate in state.shippingRates" :key="shippingRate.id" :value="shippingRate.id">
                {{ shippingRate.city }} — {{ shippingRate.price }} د.ل
              </option>
            </select>
          </label>
        </fieldset>
      </section>
      <section aria-labelledby="order-summary" class="min-w-0 rounded-2xl border border-purple-100 bg-purple-50 p-4 text-purple-950 sm:p-6">
        <h2 id="order-summary" class="mb-4 text-lg font-bold">ملخص الطلب</h2>
        <dl aria-live="polite" class="space-y-3">
          <div class="flex flex-wrap justify-between gap-2"><dt>المنتجات</dt><dd><bdi>{{ state.productsTotal }}</bdi> د.ل</dd></div>
          <div class="flex flex-wrap justify-between gap-2"><dt>التوصيل</dt><dd>
            <template v-if="shippingFee != null"><bdi>{{ shippingFee }}</bdi> د.ل</template>
            <template v-else-if="state.destinations && fields.carrierDestination">بانتظار حساب التوصيل</template>
            <template v-else>بانتظار اختيار الوجهة</template>
          </dd></div>
          <div v-if="total != null" class="flex flex-wrap justify-between gap-2 border-t border-purple-200 pt-3 text-lg font-bold">
            <dt>الإجمالي</dt><dd><bdi>{{ total }}</bdi> د.ل</dd>
          </div>
        </dl>
        <button type="submit" :disabled="busy || shippingFee == null" class="mt-5 min-h-12 w-full rounded-xl bg-purple-900 px-4 py-2 font-bold text-white hover:bg-purple-800 disabled:opacity-50">
          تأكيد الطلب والدفع عند الاستلام
        </button>
        <p role="status" :class="{ 'mt-2': busy }" class="text-sm">{{ busy ? 'جاري المعالجة…' : '' }}</p>
      </section>
    </form>
  </section>
</template>

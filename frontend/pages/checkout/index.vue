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
const serviceLabels = { women: 'توصيل نسائي', men: 'توصيل رجالي', office: 'استلام من المكتب' }
const service = ref<keyof typeof serviceLabels | ''>('')
const city = ref('')
const destinationKey = ref('')
const destinations = computed(() => state.value?.destinations ?? [])
function choice(destination: NonNullable<CheckoutState['destinations']>[number]) {
  return destination.isPickup ? (destination.service === 'men' ? 'office' : undefined) : destination.service
}
const services = computed(() => (['women', 'men', 'office'] as const)
  .filter(kind => destinations.value.some(destination => choice(destination) === kind)))
const selectedService = computed(() => services.value.length === 1 ? services.value[0] : service.value)
const serviceDestinations = computed(() => destinations.value.filter(destination =>
  services.value.length ? choice(destination) === selectedService.value : !destination.service && !destination.isPickup))
const cities = computed(() => [...new Set(serviceDestinations.value.map(destination => destination.city))])
const options = computed(() => serviceDestinations.value.filter(destination => destination.city === city.value))
const selectedDestination = computed(() => options.value.length === 1 ? options.value[0] :
  options.value.find(destination => destination.key === destinationKey.value))
const rate = computed(() => state.value?.shippingRates.find(item => item.id === fields.shippingRateId))
const hasDeliveryOptions = computed(() => state.value?.destinations
  ? services.value.length > 0 || serviceDestinations.value.length > 0
  : (state.value?.shippingRates.length ?? 0) > 0)
const shippingFee = computed(() => state.value?.destinations ? state.value.shippingFee : rate.value?.price)
const total = computed(() => state.value?.destinations ? state.value.total : rate.value?.total)

function chooseService(kind: keyof typeof serviceLabels) {
  city.value = ''
  resetMethod()
  service.value = kind
}

function resetMethod() {
  destinationKey.value = ''
}

function chooseCity(event: Event) {
  resetMethod()
  city.value = (event.target as HTMLSelectElement).value
}

watch(() => selectedDestination.value?.key ?? '', key => {
  fields.carrierDestination = key
  if (state.value) {
    state.value.quoteToken = null
    state.value.shippingFee = null
    state.value.total = null
  }
  if (key) void send('shipping')
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
  const selection = { checkoutToken: checkout.checkoutToken, carrierDestination: fields.carrierDestination }
  const input = { ...fields, ...selection, quoteToken: checkout.quoteToken,
    shippingAddress: selectedDestination.value?.isPickup ? '' : fields.shippingAddress }
  const body = action === 'shipping' ? selection :
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
    <p v-if="loading" role="status" class="rounded-2xl border border-purple-100 bg-white p-5 text-purple-900">جاري تجهيز الطلب…</p>
    <ul v-if="errors.length" role="alert" class="mb-4 list-inside list-disc rounded-xl bg-red-50 p-4 text-red-800">
      <li v-for="(error, index) in errors" :key="index">{{ error }}</li>
    </ul>
    <div v-if="state && !loading && !hasDeliveryOptions" role="status" class="mb-4 rounded-2xl border border-purple-100 bg-white p-5">
      <p class="font-semibold text-purple-950">التوصيل غير متاح حاليًا.</p>
      <p class="mt-2 text-sm text-purple-900">منتجاتك ما زالت في السلة. تقدري تعيدي التحقق من خيارات التوصيل.</p>
    </div>
    <button v-if="!loading && !hasDeliveryOptions && (state || errors.length)" type="button"
      class="storefront-action min-h-11 rounded-xl bg-purple-900 px-4 font-semibold text-white hover:bg-purple-800" @click="load">إعادة المحاولة</button>
    <form v-if="state && hasDeliveryOptions" class="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,0.65fr)]"
      @submit.prevent="send('orders')">
      <section aria-labelledby="delivery-heading" class="min-w-0 rounded-2xl border border-purple-100 bg-white p-4 shadow-sm sm:p-6">
        <h2 id="delivery-heading" class="mb-4 text-lg font-bold text-purple-950">خيارات التوصيل</h2>
        <fieldset :disabled="busy" aria-labelledby="delivery-heading" class="min-w-0 space-y-4">
          <template v-if="state.destinations">
            <fieldset v-if="services.length > 1">
              <legend class="mb-2 font-semibold">اختاري نوع التوصيل</legend>
              <div class="grid gap-2 min-[360px]:grid-cols-2">
                <label v-for="kind in services" :key="kind"
                  class="flex min-h-11 items-center gap-2 rounded-xl border border-purple-200 px-3 py-2 has-checked:border-purple-800 has-checked:bg-purple-50 has-checked:font-semibold has-checked:ring-1 has-checked:ring-purple-800 has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-purple-800">
                  <input :checked="service === kind" name="carrierService" type="radio" :value="kind"
                    class="accent-purple-800" @change="chooseService(kind)">
                  {{ serviceLabels[kind] }}
                </label>
              </div>
            </fieldset>
            <p v-else-if="selectedService" class="font-semibold text-purple-950">{{ serviceLabels[selectedService] }}</p>
            <label v-if="cities.length" class="block">مدينة التوصيل
              <select :value="city" class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950" @change="chooseCity">
                <option value="" disabled>اختاري المدينة</option>
                <option v-for="name in cities" :key="name" :value="name">{{ name }}</option>
              </select>
            </label>
            <label v-if="options.length > 1" class="block">{{ selectedService === 'office' ? 'مكتب الاستلام' : 'المنطقة' }}
              <select v-model="destinationKey" class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950">
                <option value="" disabled>{{ selectedService === 'office' ? 'اختاري المكتب' : 'اختاري المنطقة' }}</option>
                <option v-for="destination in options" :key="destination.key" :value="destination.key">{{ destination.area }}</option>
              </select>
            </label>
            <p v-else-if="selectedDestination" class="text-sm text-purple-950">
              {{ selectedDestination.isPickup ? 'مكتب الاستلام' : 'المنطقة' }} · {{ selectedDestination.area }}
            </p>
            <button v-if="selectedDestination && !busy && shippingFee == null" type="button"
              class="min-h-11 rounded-xl border border-purple-300 bg-white px-4 font-semibold text-purple-950 hover:bg-purple-50"
              @click="send('shipping')">إعادة حساب التوصيل</button>
          </template>
          <label v-else class="block">مدينة التوصيل
            <select v-model.number="fields.shippingRateId" required class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950">
              <option :value="0" disabled>اختاري المدينة</option>
              <option v-for="shippingRate in state.shippingRates" :key="shippingRate.id" :value="shippingRate.id">
                {{ shippingRate.city }} — {{ shippingRate.price }} د.ل
              </option>
            </select>
          </label>
        </fieldset>
        <fieldset v-if="!state.destinations || shippingFee != null" :disabled="busy" class="mt-6 min-w-0 space-y-4 border-t border-purple-100 pt-4">
          <legend class="pe-2 font-semibold text-purple-950">بيانات الطلب</legend>
          <div class="grid gap-4 sm:grid-cols-2">
            <label class="block">الاسم الأول
              <input v-model="fields.firstName" required maxlength="50" autocomplete="given-name" class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950">
            </label>
            <label class="block">اسم العائلة
              <input v-model="fields.lastName" required maxlength="50" autocomplete="family-name" class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950">
            </label>
          </div>
          <label class="block">رقم الواتساب
            <input v-model="fields.whatsAppNumber" required maxlength="20" type="tel" dir="ltr" autocomplete="tel" class="mt-1 block min-h-11 w-full rounded-lg border border-purple-200 bg-white px-3 text-purple-950">
          </label>
          <label v-if="!selectedDestination?.isPickup" class="block">العنوان بالتفصيل
            <textarea v-model="fields.shippingAddress" required maxlength="200" autocomplete="street-address" rows="3" class="mt-1 block w-full rounded-lg border border-purple-200 bg-white p-3 text-purple-950" />
          </label>
        </fieldset>
      </section>
      <section aria-labelledby="order-summary" class="min-w-0 rounded-2xl border border-purple-100 bg-purple-50 p-4 text-purple-950 shadow-sm sm:p-6">
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
        <button type="submit" :disabled="busy || shippingFee == null" class="storefront-action mt-5 min-h-12 w-full rounded-xl bg-purple-900 px-4 py-2 font-bold text-white hover:bg-purple-800 disabled:opacity-50">
          تأكيد الطلب والدفع عند الاستلام
        </button>
        <p role="status" :class="{ 'mt-2': busy }" class="text-sm">{{ busy ? 'جاري المعالجة…' : '' }}</p>
      </section>
    </form>
  </section>
</template>

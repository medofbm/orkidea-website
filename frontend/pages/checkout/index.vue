<script setup lang="ts">
import type { CheckoutReceipt, CheckoutState } from '~/types/checkout'

const router = useRouter()
const { cart, load: loadCart } = useCart()
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
    // An empty cart is not an error for the customer: send her to the cart, which shows the empty state.
    if (await cartIsEmpty()) {
      await router.replace('/cart')
      return
    }
    errors.value = ['تعذر فتح إتمام الطلب. راجعي السلة وحاولي مرة أخرى.']
  } finally {
    loading.value = false
  }
}

async function cartIsEmpty() {
  try {
    await loadCart()
    return !cart.value?.items.length
  } catch {
    return false
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

onMounted(() => {
  void load()
  // The summary lists the cart items; a failure here only hides the list.
  void loadCart().catch(() => undefined)
})
useSeoMeta({ title: 'إتمام الطلب', robots: 'noindex, nofollow' })
</script>

<template>
  <section aria-labelledby="checkout-heading" class="relative">
    <div aria-hidden="true" class="pointer-events-none absolute -top-6 -z-10 h-44 w-full rounded-3xl bg-gradient-hero" />
    <NuxtLink to="/cart" class="mb-3 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-brand-deep hover:bg-cream">
      <span aria-hidden="true">→</span> رجوع للسلة
    </NuxtLink>
    <h1 id="checkout-heading" class="mb-5 text-3xl font-bold text-ink sm:text-4xl">إتمام الطلب</h1>
    <p v-if="loading" role="status" class="card-glass p-5 text-brand-deep">جاري تجهيز الطلب…</p>
    <ul v-if="errors.length" role="alert" class="mb-4 list-inside list-disc rounded-2xl border border-danger/30 bg-danger/10 p-4 font-medium text-danger">
      <li v-for="(error, index) in errors" :key="index">{{ error }}</li>
    </ul>
    <div v-if="state && !loading && !hasDeliveryOptions" role="status" class="mb-4 card-glass p-5">
      <p class="font-semibold text-ink">التوصيل غير متاح حاليًا.</p>
      <p class="mt-2 text-sm text-brand-deep">منتجاتك ما زالت في السلة. تقدري تعيدي التحقق من خيارات التوصيل.</p>
    </div>
    <button v-if="!loading && !hasDeliveryOptions && (state || errors.length)" type="button"
      class="storefront-action btn-primary" @click="load">إعادة المحاولة</button>
    <form v-if="state && hasDeliveryOptions" class="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(19rem,0.6fr)]"
      @submit.prevent="send('orders')">
      <section aria-labelledby="delivery-heading" class="min-w-0 card-glass p-4 sm:p-6">
        <h2 id="delivery-heading" class="mb-4 text-lg font-bold text-ink"><span aria-hidden="true" class="me-2 inline-grid size-7 place-items-center rounded-full bg-gradient-brand text-sm text-white">1</span>خيارات التوصيل</h2>
        <fieldset :disabled="busy" aria-labelledby="delivery-heading" class="min-w-0 space-y-4">
          <template v-if="state.destinations">
            <fieldset v-if="services.length > 1">
              <legend class="mb-2 font-semibold">اختاري نوع التوصيل</legend>
              <div class="grid gap-2 min-[360px]:grid-cols-2">
                <label v-for="kind in services" :key="kind"
                  class="flex min-h-11 items-center gap-2 rounded-2xl border border-lavender-deep bg-white px-3 py-2 has-checked:border-brand-deep has-checked:bg-cream has-checked:font-semibold has-checked:ring-1 has-checked:ring-brand-deep has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-brand-deep">
                  <input :checked="service === kind" name="carrierService" type="radio" :value="kind"
                    class="accent-brand-deep" @change="chooseService(kind)">
                  {{ serviceLabels[kind] }}
                </label>
              </div>
            </fieldset>
            <p v-else-if="selectedService" class="font-semibold text-ink">{{ serviceLabels[selectedService] }}</p>
            <label v-if="cities.length" class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="truck" :size="16" class="text-brand" />مدينة التوصيل</span>
              <select :value="city" class="mt-1 block w-full field" @change="chooseCity">
                <option value="" disabled>اختاري المدينة</option>
                <option v-for="name in cities" :key="name" :value="name">{{ name }}</option>
              </select>
            </label>
            <label v-if="options.length > 1" class="block">{{ selectedService === 'office' ? 'مكتب الاستلام' : 'المنطقة' }}
              <select v-model="destinationKey" class="mt-1 block w-full field">
                <option value="" disabled>{{ selectedService === 'office' ? 'اختاري المكتب' : 'اختاري المنطقة' }}</option>
                <option v-for="destination in options" :key="destination.key" :value="destination.key">{{ destination.area }}</option>
              </select>
            </label>
            <p v-else-if="selectedDestination" class="text-sm text-ink">
              {{ selectedDestination.isPickup ? 'مكتب الاستلام' : 'المنطقة' }} · {{ selectedDestination.area }}
            </p>
            <button v-if="selectedDestination && !busy && shippingFee == null" type="button"
              class="btn-secondary"
              @click="send('shipping')">إعادة حساب التوصيل</button>
          </template>
          <label v-else class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="truck" :size="16" class="text-brand" />مدينة التوصيل</span>
            <select v-model.number="fields.shippingRateId" required class="mt-1 block w-full field">
              <option :value="0" disabled>اختاري المدينة</option>
              <option v-for="shippingRate in state.shippingRates" :key="shippingRate.id" :value="shippingRate.id">
                {{ shippingRate.city }} — {{ shippingRate.price }} د.ل
              </option>
            </select>
          </label>
        </fieldset>
        <fieldset v-if="!state.destinations || shippingFee != null" :disabled="busy" class="mt-6 min-w-0 space-y-4 border-t border-lavender-deep pt-4">
          <legend class="pe-2 font-semibold text-ink"><span aria-hidden="true" class="me-2 inline-grid size-7 place-items-center rounded-full bg-gradient-brand text-sm text-white">2</span>بيانات الطلب</legend>
          <div class="grid gap-4 sm:grid-cols-2">
            <label class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="user" :size="16" class="text-brand" />الاسم الأول</span>
              <input v-model="fields.firstName" required maxlength="50" autocomplete="given-name" class="mt-1 block w-full field">
            </label>
            <label class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="user" :size="16" class="text-brand" />اسم العائلة</span>
              <input v-model="fields.lastName" required maxlength="50" autocomplete="family-name" class="mt-1 block w-full field">
            </label>
          </div>
          <label class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="whatsapp" :size="16" class="text-brand" />رقم الواتساب</span>
            <input v-model="fields.whatsAppNumber" required maxlength="20" type="tel" dir="ltr" autocomplete="tel" inputmode="tel" placeholder="09XXXXXXXX" class="mt-1 block w-full field">
          </label>
          <label v-if="!selectedDestination?.isPickup" class="block"><span class="inline-flex items-center gap-1.5 font-medium text-ink"><AppIcon name="home" :size="16" class="text-brand" />العنوان بالتفصيل</span>
            <textarea v-model="fields.shippingAddress" required maxlength="200" autocomplete="street-address" rows="3" class="mt-1 block w-full field" />
          </label>
        </fieldset>
      </section>
      <section aria-labelledby="order-summary" class="min-w-0 card-glass p-4 text-ink sm:p-6 lg:sticky lg:top-24">
        <h2 id="order-summary" class="mb-4 text-lg font-bold"><span aria-hidden="true" class="me-2 inline-grid size-7 place-items-center rounded-full bg-gradient-brand text-sm text-white">3</span>ملخص الطلب</h2>
        <ul v-if="cart?.items.length" class="mb-4 space-y-3 border-b border-lavender-deep pb-4" aria-label="منتجات الطلب">
          <li v-for="item in cart.items" :key="item.variantId" class="flex items-center gap-3">
            <span class="grid size-12 shrink-0 place-items-center overflow-hidden rounded-xl bg-lavender">
              <img v-if="item.image" :src="item.image" alt="" width="48" height="48" class="size-full object-cover">
              <img v-else src="/brand/flower-purple.png" alt="" width="24" height="24" class="size-6 opacity-80">
            </span>
            <span class="min-w-0 flex-1 text-sm">
              <span class="block font-semibold wrap-anywhere">{{ item.name }}</span>
              <span class="text-ink-soft"><span v-if="item.size">{{ item.size }} · </span>الكمية {{ item.quantity }}</span>
            </span>
            <span class="shrink-0 text-sm font-semibold">{{ item.subtotal }} د.ل</span>
          </li>
        </ul>
        <dl aria-live="polite" class="space-y-3">
          <div class="flex flex-wrap justify-between gap-2"><dt>المنتجات</dt><dd class="font-semibold"><bdi>{{ state.productsTotal }}</bdi> د.ل</dd></div>
          <div class="flex flex-wrap justify-between gap-2"><dt>التوصيل</dt><dd>
            <template v-if="shippingFee != null"><bdi>{{ shippingFee }}</bdi> د.ل</template>
            <template v-else-if="state.destinations && fields.carrierDestination">بانتظار حساب التوصيل</template>
            <template v-else>بانتظار اختيار الوجهة</template>
          </dd></div>
          <div v-if="total != null" class="flex flex-wrap justify-between gap-2 border-t border-lavender-deep pt-3 text-lg font-bold">
            <dt>الإجمالي</dt><dd class="price"><bdi>{{ total }}</bdi> د.ل</dd>
          </div>
        </dl>
        <button type="submit" :disabled="busy || shippingFee == null" class="storefront-action btn-primary mt-5 w-full">
          تأكيد الطلب والدفع عند الاستلام
        </button>
        <p v-if="shippingFee == null && !busy" class="mt-2 text-center text-sm text-brand-deep">اختاري مدينة التوصيل باش يظهر الإجمالي ويتفعّل التأكيد.</p>
        <p class="mt-3 flex items-center justify-center gap-1.5 text-xs text-ink-soft"><AppIcon name="cash" :size="14" /> الدفع عند الاستلام، بدون دفع مسبق</p>
        <p role="status" :class="{ 'mt-2': busy }" class="text-sm">{{ busy ? 'جاري المعالجة…' : '' }}</p>
      </section>
    </form>
  </section>
</template>

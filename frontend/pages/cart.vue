<script setup lang="ts">
const { cart, busy, message, load, mutate } = useCart()
const loading = ref(true)
const failed = ref(false)
async function reload() {
  loading.value = true
  failed.value = false
  try { await load() } catch { failed.value = true } finally { loading.value = false }
}
onMounted(reload)
const allAvailable = computed(() => !!cart.value?.items.every(item => item.available))
useSeoMeta({ title: 'سلة أوركيديا', robots: 'noindex, nofollow' })
</script>

<template>
  <section aria-labelledby="cart-title" class="relative pb-24 lg:pb-0">
    <div aria-hidden="true" class="pointer-events-none absolute -top-6 -z-10 h-56 w-full overflow-hidden rounded-3xl bg-gradient-hero">
      <span class="absolute -top-10 end-6 size-40 rounded-full bg-lilac/40 motion-safe:animate-float" />
      <span class="absolute start-10 top-16 size-20 rounded-full bg-blush motion-safe:animate-float" />
    </div>
    <NuxtLink to="/" class="mb-3 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-brand-deep hover:bg-cream">
      <span aria-hidden="true">→</span> كل المنتجات
    </NuxtLink>
    <h1 id="cart-title" class="mb-5 flex items-center gap-3 text-3xl font-bold text-ink sm:text-4xl">
      <AppIcon name="cart" :size="30" class="text-brand" /> سلة التسوق
    </h1>
    <p v-if="loading" role="status" class="card-glass p-5 text-brand-deep">جاري تحميل السلة…</p>
    <div v-else-if="failed" role="alert" class="card-glass border-danger/30 p-5">
      <p class="text-danger">تعذر تحميل السلة.</p><button class="btn-secondary mt-2" @click="reload()">إعادة المحاولة</button>
    </div>
    <template v-else-if="cart">
      <div v-if="!cart.items.length" class="card-glass animate-fade-in-up flex flex-col items-center p-8 text-center sm:p-12">
        <div class="mb-4 grid size-28 place-items-center rounded-full bg-lavender">
          <img src="/brand/flower-purple.png" alt="" width="72" height="72" class="size-16 motion-safe:animate-float">
        </div>
        <p class="text-xl font-bold text-ink">سلتك فارغة.</p>
        <p class="mt-1 text-sm text-ink-soft">أضيفي منتجاتك المفضلة وابدئي رحلة العناية.</p>
        <NuxtLink to="/#catalogue-heading" class="storefront-action btn-primary mt-5">تسوقي الآن</NuxtLink>
      </div>
      <div v-else class="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,0.48fr)]">
        <ul class="min-w-0 space-y-3">
          <li v-for="item in cart.items" :key="item.variantId" class="card-glass animate-fade-in-up p-3 sm:p-4">
            <div class="flex gap-3 sm:gap-4">
              <div class="grid size-20 shrink-0 place-items-center overflow-hidden rounded-2xl bg-lavender sm:size-24">
                <img v-if="item.image" :src="item.image" alt="" class="size-full object-cover">
                <img v-else src="/brand/flower-purple.png" alt="" class="size-10 opacity-80">
              </div>
              <div class="min-w-0 flex-1">
                <NuxtLink :to="'/products/' + item.productId" class="inline-flex min-h-11 items-center rounded-md font-bold leading-snug wrap-anywhere text-ink hover:text-brand focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand">{{ item.name }}</NuxtLink>
                <p class="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-ink-soft">
                  <span v-if="item.size" class="badge">{{ item.size }}</span>
                  <span><bdi>{{ formatPrice(item.price) }}</bdi> د.ل للوحدة</span>
                </p>
                <p v-if="!item.available" class="mt-1 text-sm font-semibold text-danger">غير متاح للطلب. احذفيه لمتابعة الطلب.</p>
              </div>
            </div>
            <div class="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 border-t border-lavender pt-3">
              <div role="group" :aria-label="'كمية ' + item.name" class="inline-flex items-center rounded-full border border-lavender-deep bg-white p-0.5">
                <button type="button" :disabled="busy || item.quantity <= 1" :aria-label="'تقليل كمية ' + item.name"
                  class="storefront-action grid size-11 place-items-center rounded-full text-lg font-bold text-brand hover:enabled:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity - 1)"><span aria-hidden="true">−</span></button>
                <span role="status" :aria-label="'الكمية ' + item.quantity" class="min-w-10 text-center text-base font-bold tabular-nums text-ink">{{ item.quantity }}</span>
                <button type="button" :disabled="busy || item.quantity >= 99" :aria-label="'زيادة كمية ' + item.name"
                  class="storefront-action grid size-11 place-items-center rounded-full text-lg font-bold text-brand hover:enabled:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity + 1)"><span aria-hidden="true">+</span></button>
              </div>
              <button type="button" :disabled="busy" :aria-label="'حذف ' + item.name"
                class="storefront-action inline-flex min-h-11 items-center gap-1.5 rounded-full border border-danger/30 bg-white px-3 text-sm font-semibold text-danger hover:enabled:bg-danger/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-danger disabled:opacity-50" @click="mutate('DELETE', item.variantId)">
                <AppIcon name="trash" :size="16" aria-hidden="true" /> حذف
              </button>
              <p class="ms-auto text-end leading-tight"><span class="block text-xs text-ink-soft">الإجمالي</span><span class="price text-lg sm:text-xl"><bdi>{{ formatPrice(item.subtotal) }}</bdi> د.ل</span></p>
            </div>
          </li>
        </ul>
        <section aria-labelledby="cart-summary" class="card-glass p-5 lg:sticky lg:top-24">
          <h2 id="cart-summary" class="mb-3 text-lg font-bold text-ink">ملخص السلة</h2>
          <dl class="text-ink"><div class="flex flex-wrap justify-between gap-2">
            <dt>إجمالي المنتجات</dt><dd class="price"><bdi>{{ formatPrice(cart.productsTotal) }}</bdi> د.ل</dd>
          </div></dl>
          <DeliveryEstimate />
          <p v-if="!allAvailable" role="alert" class="mt-4 rounded-xl bg-danger/10 p-3 text-sm font-semibold text-danger">في سلتك حجم غير متاح للطلب. احذفيه من السلة لتقدري تكملي «إتمام الطلب».</p>
          <NuxtLink v-if="allAvailable" to="/checkout"
            class="storefront-action btn-primary mt-4 hidden w-full lg:flex">إتمام الطلب</NuxtLink>
        </section>
        <div v-if="allAvailable" class="glass fixed inset-x-0 bottom-0 z-30 flex items-center justify-between gap-3 border-t border-lavender-deep px-4 py-3 lg:hidden">
          <p class="text-sm text-ink-soft">الإجمالي <span class="price block text-lg"><bdi>{{ formatPrice(cart.productsTotal) }}</bdi> د.ل</span></p>
          <NuxtLink to="/checkout" class="storefront-action btn-primary">إتمام الطلب</NuxtLink>
        </div>
      </div>
    </template>
  </section>
</template>

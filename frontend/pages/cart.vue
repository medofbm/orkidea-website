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
      <p role="status" :class="{ 'mb-4': message }" class="text-sm font-medium text-brand-deep">{{ message }}</p>
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
          <li v-for="item in cart.items" :key="item.variantId" class="card-glass animate-fade-in-up flex gap-3 p-3 sm:gap-4 sm:p-4">
            <div class="grid size-20 shrink-0 place-items-center overflow-hidden rounded-2xl bg-lavender sm:size-24">
              <img v-if="item.image" :src="item.image" alt="" class="size-full object-cover">
              <img v-else src="/brand/flower-purple.png" alt="" class="size-10 opacity-80">
            </div>
            <div class="min-w-0 flex-1">
              <NuxtLink :to="'/products/' + item.productId" class="inline-flex min-h-11 items-center font-bold wrap-anywhere text-ink hover:underline">{{ item.name }}</NuxtLink>
              <p class="text-sm text-brand-deep"><span v-if="item.size">{{ item.size }} · </span><bdi>{{ formatPrice(item.price) }}</bdi> د.ل للوحدة</p>
              <p v-if="!item.available" class="mt-1 text-sm text-danger">هذا الحجم غير متاح حاليًا. احذفيه لمتابعة الطلب.</p>
              <div class="mt-2 flex flex-wrap items-center gap-2 sm:gap-3">
                <div class="inline-flex items-center rounded-full border border-lavender-deep bg-white">
                  <button :disabled="busy || item.quantity >= 99" :aria-label="'زيادة كمية ' + item.name"
                    class="min-h-11 min-w-11 rounded-full font-semibold text-ink hover:bg-lavender disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity + 1)">+</button>
                  <span :aria-label="'الكمية ' + item.quantity" class="min-w-8 text-center font-bold tabular-nums">{{ item.quantity }}</span>
                  <button :disabled="busy || item.quantity <= 1" :aria-label="'تقليل كمية ' + item.name"
                    class="min-h-11 min-w-11 rounded-full font-semibold text-ink hover:bg-lavender disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity - 1)">−</button>
                </div>
                <span class="price ms-auto"><bdi>{{ formatPrice(item.subtotal) }}</bdi> د.ل</span>
                <button :disabled="busy" :aria-label="'حذف ' + item.name"
                  class="btn-ghost !text-danger hover:!bg-danger/10" @click="mutate('DELETE', item.variantId)">
                  <AppIcon name="x" :size="16" /> حذف
                </button>
              </div>
            </div>
          </li>
        </ul>
        <section aria-labelledby="cart-summary" class="card-glass p-5 lg:sticky lg:top-24">
          <h2 id="cart-summary" class="mb-3 text-lg font-bold text-ink">ملخص السلة</h2>
          <dl class="text-ink"><div class="flex flex-wrap justify-between gap-2">
            <dt>إجمالي المنتجات</dt><dd class="price"><bdi>{{ formatPrice(cart.productsTotal) }}</bdi> د.ل</dd>
          </div></dl>
          <DeliveryEstimate />
          <NuxtLink v-if="cart.items.every(item => item.available)" to="/checkout"
            class="storefront-action btn-primary mt-4 hidden w-full lg:flex">إتمام الطلب</NuxtLink>
        </section>
        <div v-if="cart.items.every(item => item.available)" class="glass fixed inset-x-0 bottom-0 z-30 flex items-center justify-between gap-3 border-t border-lavender-deep px-4 py-3 lg:hidden">
          <p class="text-sm text-ink-soft">الإجمالي <span class="price block text-lg"><bdi>{{ formatPrice(cart.productsTotal) }}</bdi> د.ل</span></p>
          <NuxtLink to="/checkout" class="storefront-action btn-primary">إتمام الطلب</NuxtLink>
        </div>
      </div>
    </template>
  </section>
</template>

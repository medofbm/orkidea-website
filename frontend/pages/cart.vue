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
  <section aria-labelledby="cart-title">
    <NuxtLink to="/" class="mb-3 inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-purple-900 hover:bg-purple-50">
      <span aria-hidden="true">→</span> كل المنتجات
    </NuxtLink>
    <h1 id="cart-title" class="mb-5 text-3xl font-bold text-purple-950 sm:text-4xl">سلة التسوق</h1>
    <p v-if="loading" role="status" class="rounded-2xl border border-purple-100 bg-white p-5 text-purple-900">جاري تحميل السلة…</p>
    <div v-else-if="failed" role="alert" class="rounded-2xl border border-red-200 bg-white p-5">
      <p>تعذر تحميل السلة.</p><button class="min-h-11 underline" @click="reload()">إعادة المحاولة</button>
    </div>
    <template v-else-if="cart">
      <p role="status" :class="{ 'mb-4': message }" class="text-sm font-medium text-purple-900">{{ message }}</p>
      <div v-if="!cart.items.length" class="rounded-2xl border border-purple-100 bg-white p-5 shadow-sm sm:p-8">
        <p class="font-semibold text-purple-950">سلتك فارغة.</p>
        <NuxtLink to="/" class="storefront-action mt-3 inline-flex min-h-11 items-center rounded-xl bg-purple-900 px-4 font-semibold text-white hover:bg-purple-800">تصفّحي المنتجات</NuxtLink>
      </div>
      <div v-else class="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,0.48fr)]">
        <ul class="min-w-0 space-y-3">
          <li v-for="item in cart.items" :key="item.variantId" class="rounded-2xl border border-purple-100 bg-white p-4 shadow-sm sm:p-5">
            <NuxtLink :to="'/products/' + item.productId" class="inline-flex min-h-11 items-center font-bold wrap-anywhere text-purple-950 hover:underline">{{ item.name }}</NuxtLink>
            <p class="text-sm text-purple-900"><span v-if="item.size">{{ item.size }} · </span><bdi>{{ item.price }}</bdi> د.ل للوحدة</p>
            <p v-if="!item.available" class="text-red-700">هذا الحجم غير متاح حاليًا. احذفيه لمتابعة الطلب.</p>
            <div class="mt-3 flex flex-wrap items-center gap-2 border-t border-purple-100 pt-3 sm:gap-3">
              <button :disabled="busy || item.quantity >= 99" :aria-label="'زيادة كمية ' + item.name"
                class="min-h-11 min-w-11 rounded-lg border border-purple-200 bg-white font-semibold text-purple-950 hover:bg-purple-50 disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity + 1)">+</button>
              <span :aria-label="'الكمية ' + item.quantity">{{ item.quantity }}</span>
              <button :disabled="busy || item.quantity <= 1" :aria-label="'تقليل كمية ' + item.name"
                class="min-h-11 min-w-11 rounded-lg border border-purple-200 bg-white font-semibold text-purple-950 hover:bg-purple-50 disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity - 1)">−</button>
              <span class="ms-auto font-semibold text-purple-950"><bdi>{{ item.subtotal }}</bdi> د.ل</span>
              <button :disabled="busy" :aria-label="'حذف ' + item.name"
                class="min-h-11 rounded-lg px-3 font-medium text-red-700 hover:bg-red-50 disabled:opacity-40" @click="mutate('DELETE', item.variantId)">حذف</button>
            </div>
          </li>
        </ul>
        <section aria-labelledby="cart-summary" class="rounded-2xl border border-purple-100 bg-purple-50 p-5 shadow-sm">
          <h2 id="cart-summary" class="mb-3 text-lg font-bold text-purple-950">ملخص السلة</h2>
          <dl class="text-purple-950"><div class="flex flex-wrap justify-between gap-2">
            <dt>إجمالي المنتجات</dt><dd class="font-bold"><bdi>{{ cart.productsTotal }}</bdi> د.ل</dd>
          </div></dl>
          <p class="mt-2 text-sm text-purple-900">تُضاف تكلفة التوصيل عند إتمام الطلب.</p>
          <NuxtLink v-if="cart.items.every(item => item.available)" to="/checkout"
            class="storefront-action mt-4 flex min-h-12 items-center justify-center rounded-xl bg-purple-900 px-5 font-bold text-white hover:bg-purple-800">إتمام الطلب</NuxtLink>
        </section>
      </div>
    </template>
  </section>
</template>

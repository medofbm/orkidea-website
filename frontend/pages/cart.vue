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
    <h1 id="cart-title" class="mb-6 text-3xl font-bold">سلة التسوق</h1>
    <p v-if="loading" role="status">جاري تحميل السلة…</p>
    <div v-else-if="failed" role="alert">
      <p>تعذر تحميل السلة.</p><button class="min-h-11 underline" @click="reload()">إعادة المحاولة</button>
    </div>
    <template v-else-if="cart">
      <p role="status" class="mb-4">{{ message }}</p>
      <p v-if="!cart.items.length">سلتك فارغة. <NuxtLink to="/" class="underline">تصفّحي المنتجات</NuxtLink></p>
      <ul v-else class="space-y-4">
        <li v-for="item in cart.items" :key="item.variantId" class="rounded-xl border border-purple-100 bg-white p-4">
          <NuxtLink :to="'/products/' + item.productId" class="font-semibold">{{ item.name }}</NuxtLink>
          <p>{{ item.size }} — {{ item.price }} د.ل</p>
          <p v-if="!item.available" class="text-red-700">هذا الحجم غير متاح حاليًا. احذفيه لمتابعة الطلب.</p>
          <div class="mt-3 flex flex-wrap items-center gap-3">
            <button :disabled="busy || item.quantity >= 99" :aria-label="'زيادة كمية ' + item.name"
              class="min-h-11 min-w-11 rounded border disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity + 1)">+</button>
            <span :aria-label="'الكمية ' + item.quantity">{{ item.quantity }}</span>
            <button :disabled="busy || item.quantity <= 1" :aria-label="'تقليل كمية ' + item.name"
              class="min-h-11 min-w-11 rounded border disabled:opacity-40" @click="mutate('PATCH', item.variantId, item.quantity - 1)">−</button>
            <span class="font-semibold">{{ item.subtotal }} د.ل</span>
            <button :disabled="busy" :aria-label="'حذف ' + item.name"
              class="min-h-11 rounded px-3 text-red-700" @click="mutate('DELETE', item.variantId)">حذف</button>
          </div>
        </li>
      </ul>
      <div v-if="cart.items.length" class="mt-6 rounded-xl bg-purple-50 p-5">
        <p class="text-lg font-semibold">إجمالي المنتجات {{ cart.productsTotal }} د.ل</p>
        <p class="mt-2">تُضاف تكلفة التوصيل عند إتمام الطلب.</p>
      </div>
    </template>
  </section>
</template>

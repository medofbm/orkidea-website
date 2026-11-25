<script setup lang="ts">
interface OrderRow { id: number; orderDate: string; status: string; statusLabel: string; total: string; itemsCount: number }
interface OrderLine { productName: string; size: string | null; quantity: number; unitPrice: string; subtotal: string }
interface OrderDetails {
  id: number; city: string; productsTotal: string; shippingFee: string; total: string; lines: OrderLine[]
}

const orders = ref<OrderRow[] | null>(null)
const failed = ref(false)
const openId = ref<number | null>(null)
const details = reactive<Record<number, OrderDetails | 'loading' | 'error'>>({})

const badge: Record<string, string> = {
  Pending: 'badge-status-pending', Processing: 'badge-status-processing', Shipped: 'badge-status-shipped',
  Delivered: 'badge-status-delivered', Cancelled: 'badge-status-cancelled'
}
const dateFormat = new Intl.DateTimeFormat('ar-LY', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Tripoli' })
const formatDate = (iso: string) => dateFormat.format(new Date(iso))

async function load() {
  failed.value = false
  try {
    orders.value = (await $fetch<{ items: OrderRow[] }>('/api/account/orders')).items
  } catch {
    failed.value = true
  }
}

async function toggle(id: number) {
  if (openId.value === id) { openId.value = null; return }
  openId.value = id
  if (details[id] && details[id] !== 'error') return
  details[id] = 'loading'
  try {
    details[id] = await $fetch<OrderDetails>('/api/account/orders/' + id)
  } catch {
    details[id] = 'error'
  }
}

onMounted(load)
</script>

<template>
  <section class="mt-6 space-y-3" aria-labelledby="my-orders-title">
    <h2 id="my-orders-title" class="text-lg font-bold text-ink">طلباتي</h2>
    <p v-if="orders === null && !failed" role="status" class="text-ink-soft">جاري تحميل طلباتك…</p>
    <div v-else-if="failed" role="alert" class="card space-y-2 p-4">
      <p class="text-danger">تعذر تحميل طلباتك.</p>
      <button type="button" class="btn-secondary" @click="load()">إعادة المحاولة</button>
    </div>
    <div v-else-if="orders && orders.length === 0" class="card space-y-3 p-4 text-center">
      <p>ما عندكش طلبات مسجّلة بحسابك لين الحين.</p>
      <NuxtLink to="/#catalogue-heading" class="btn-primary w-full">تسوقي الآن</NuxtLink>
      <p class="text-xs text-ink-soft">الطلبات اللي تمت كزائرة أو قبل تسجيل الدخول ما تظهرش هنا.</p>
    </div>
    <template v-else-if="orders">
      <ul class="space-y-3">
        <li v-for="order in orders" :key="order.id" class="card p-3">
          <button type="button" class="flex min-h-11 w-full flex-wrap items-center justify-between gap-2 text-start"
            :aria-expanded="openId === order.id" :aria-controls="'order-' + order.id" @click="toggle(order.id)">
            <span class="font-semibold">طلب رقم {{ order.id }}</span>
            <span class="badge" :class="badge[order.status]">{{ order.statusLabel }}</span>
            <span class="w-full text-sm text-ink-soft">{{ formatDate(order.orderDate) }}</span>
            <span class="text-sm">{{ order.total }} د.ل</span>
            <span class="text-sm text-ink-soft">{{ order.itemsCount }} قطعة</span>
          </button>
          <div v-if="openId === order.id" :id="'order-' + order.id" class="mt-3 border-t border-ink/10 pt-3 text-sm">
            <p v-if="details[order.id] === 'loading'" role="status" class="text-ink-soft">جاري التحميل…</p>
            <div v-else-if="details[order.id] === 'error'" role="alert" class="space-y-2">
              <p class="text-danger">تعذر تحميل تفاصيل الطلب.</p>
              <button type="button" class="btn-secondary" @click="toggle(order.id); toggle(order.id)">إعادة المحاولة</button>
            </div>
            <template v-else-if="details[order.id]">
              <ul class="space-y-2">
                <li v-for="(line, index) in (details[order.id] as OrderDetails).lines" :key="index"
                  class="flex flex-wrap justify-between gap-x-3 gap-y-1">
                  <span class="min-w-0 break-words">{{ line.productName }}<template v-if="line.size"> ({{ line.size }})</template> × {{ line.quantity }}</span>
                  <span>{{ line.subtotal }} د.ل</span>
                </li>
              </ul>
              <dl class="mt-3 space-y-1 border-t border-ink/10 pt-3">
                <div class="flex justify-between"><dt class="text-ink-soft">المنتجات</dt><dd>{{ (details[order.id] as OrderDetails).productsTotal }} د.ل</dd></div>
                <div class="flex justify-between"><dt class="text-ink-soft">التوصيل ({{ (details[order.id] as OrderDetails).city }})</dt><dd>{{ (details[order.id] as OrderDetails).shippingFee }} د.ل</dd></div>
                <div class="flex justify-between font-bold"><dt>الإجمالي</dt><dd>{{ (details[order.id] as OrderDetails).total }} د.ل</dd></div>
              </dl>
            </template>
          </div>
        </li>
      </ul>
      <p class="text-xs text-ink-soft">الطلبات اللي تمت كزائرة أو قبل تسجيل الدخول ما تظهرش هنا.</p>
    </template>
  </section>
</template>

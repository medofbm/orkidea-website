<script setup lang="ts">
import type { AdminDashboard } from '~/types/admin'
import type { AppIconName } from '~/components/AppIcon.vue'
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'لوحة إدارة أوركيديا' })

const { data, error, status } = useFetch<AdminDashboard>('/api/admin/dashboard', { server: false, cache: 'no-store' })
// Best effort: reviews waiting for approval. A failure here never breaks the dashboard.
const { data: reviews } = useFetch<{ items: { isVisible?: boolean }[] }>('/api/admin/reviews', { server: false, cache: 'no-store', retry: 0 })

const todayLabel = new Intl.DateTimeFormat('ar-LY', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Africa/Tripoli' }).format(new Date())
const greeting = (() => {
  const hour = Number(new Intl.DateTimeFormat('en-GB', { hour: 'numeric', hour12: false, timeZone: 'Africa/Tripoli' }).format(new Date()))
  return hour < 12 ? 'صباح الخير' : 'مساء الخير'
})()

type Attention = { key: string, text: string, short: string, action: string, to: string, icon: AppIconName, count: number, tone: 'warning' | 'brand' }

// «يحتاج انتباهك»: only things with a real number above zero, each with a direct link.
const attention = computed<Attention[]>(() => {
  const d = data.value
  if (!d) return []
  const hidden = reviews.value?.items.filter(item => item.isVisible === false).length ?? 0
  const outOfStock = d.lowestStock?.filter(row => Number(row.available) <= 0).length ?? 0
  const unorderable = d.unorderableSizes?.length ?? 0
  const rows: Attention[] = [
    { key: 'orders', count: d.pendingOrders, text: 'طلبات جديدة تنتظر المراجعة', short: 'طلبات جديدة', tone: 'warning', action: 'راجع الطلبات', to: '/admin/orders?status=pending', icon: 'cart' },
    { key: 'messages', count: d.unreadMessages ?? 0, text: 'رسائل من الزبائن لم تُقرأ', short: 'رسائل لم تُقرأ', tone: 'brand', action: 'اقرأ الرسائل', to: '/admin/messages', icon: 'mail' },
    { key: 'reviews', count: hidden, text: 'تقييمات بانتظار موافقتك', short: 'تقييمات بانتظار الموافقة', tone: 'brand', action: 'راجع التقييمات', to: '/admin/reviews', icon: 'star' },
    { key: 'stock', count: outOfStock, text: 'منتجات خلص مخزونها', short: 'منتجات خلص مخزونها', tone: 'warning', action: 'افتح المخزون', to: '/admin/inventory', icon: 'droplet' },
    { key: 'sizes', count: unorderable, text: 'أحجام لا يقدر الزبون يطلبها، حدد كمية الاستهلاك لها', short: 'أحجام لا تُطلب', tone: 'warning', action: 'اضبط الأحجام', to: '/admin/inventory/' + (d.unorderableSizes?.[0]?.productId ?? ''), icon: 'bag' },
    { key: 'variants', count: d.productsWithoutVariants, text: 'منتجات بدون أحجام', short: 'منتجات بدون أحجام', tone: 'warning', action: 'راجع المنتجات', to: '/admin/products', icon: 'flower' }
  ]
  return rows.filter(row => row.count > 0)
})

// Main figures. Money stays as the server's string; the UI never recalculates it.
const kpis = computed(() => {
  const d = data.value
  if (!d) return []
  return [
    { label: 'مبيعات اليوم', value: `${d.valueToday ?? '0.00'} د.ل`, hint: `من ${d.ordersToday ?? 0} طلب اليوم`, icon: 'sparkle', tone: 'text-brand bg-lavender' },
    { label: 'مبيعات آخر 30 يومًا', value: `${d.productsValueLast30Days} د.ل`, hint: 'قيمة المنتجات بدون التوصيل والملغي', icon: 'cash', tone: 'text-success bg-status-delivered' },
    { label: 'متوسط الطلب', value: d.averageOrderValue30Days ? `${d.averageOrderValue30Days} د.ل` : '—', hint: 'كم يدفع الزبون في الطلب الواحد (30 يومًا)', icon: 'bag', tone: 'text-info bg-status-processing' },
    { label: 'عدد الطلبات', value: String(d.ordersLast30Days ?? 0), hint: 'آخر 30 يومًا بدون الملغي', icon: 'cart', tone: 'text-warning bg-status-pending' }
  ] as const
})

const weekly = computed(() => {
  const change = data.value?.weeklyChangePercent
  if (change == null) return null
  return { up: change >= 0, text: `${change >= 0 ? '+' : ''}${change}% عن الأسبوع الماضي` }
})

// Order journey: new -> preparing -> on the way -> delivered, and cancelled on the side.
const flow = [
  { status: 'Pending', query: 'pending', label: 'جديد', hint: 'ينتظر مراجعتك' },
  { status: 'Processing', query: 'processing', label: 'قيد التجهيز', hint: 'يتجهز عندك' },
  { status: 'Shipped', query: 'shipped', label: 'قيد التوصيل', hint: 'مع شركة التوصيل' },
  { status: 'Delivered', query: 'delivered', label: 'مسلّم', hint: 'وصل للزبون' }
] as const
const statusRow = computed(() => {
  const counts = data.value?.statusCounts
  if (!counts) return []
  return flow.map(step => ({ ...step, count: counts[step.status] ?? 0 }))
})
const cancelled = computed(() => data.value?.statusCounts?.Cancelled ?? 0)

const statusClasses: Record<string, string> = {
  Pending: 'badge-status-pending', Processing: 'badge-status-processing', Shipped: 'badge-status-shipped',
  Delivered: 'badge-status-delivered', Cancelled: 'badge-status-cancelled'
}

function stockTone(available: string) {
  return Number(available) <= 0 ? 'bg-status-cancelled text-danger' : 'bg-lavender text-brand-deep'
}
</script>

<template>
  <section aria-labelledby="dashboard-title">
    <!-- Greeting -->
    <div class="bg-gradient-brand mb-6 flex flex-wrap items-center justify-between gap-3 rounded-3xl p-5 text-white shadow-soft sm:p-6">
      <div>
        <h1 id="dashboard-title" class="text-2xl font-extrabold sm:text-3xl">{{ greeting }}، أهلاً بك</h1>
        <p class="mt-1 text-sm text-white/85">{{ todayLabel }}</p>
      </div>
      <NuxtLink to="/" class="inline-flex min-h-11 items-center gap-1.5 rounded-xl bg-white/15 px-4 text-sm font-semibold text-white ring-1 ring-white/30 hover:bg-white/25 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white">
        <AppIcon name="sparkle" :size="16" />عرض المتجر
      </NuxtLink>
    </div>

    <p v-if="status === 'idle' || status === 'pending'" role="status" class="card p-6 text-center text-ink-soft motion-safe:animate-pulse">جاري تحميل الملخص…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <!-- 1. What needs you now -->
      <section aria-labelledby="attention-title" class="mb-8">
        <h2 id="attention-title" class="mb-3 text-xl font-bold">يحتاج انتباهك</h2>
        <div v-if="!attention.length" class="flex items-center gap-3 rounded-2xl border border-success/30 bg-status-delivered p-4 text-success">
          <AppIcon name="check-circle" :size="24" />
          <p class="font-semibold">كل شيء تمام، ما فيش شيء ينتظرك الآن.</p>
        </div>
        <ul v-else class="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          <li v-for="item in attention" :key="item.key">
            <NuxtLink :to="item.to" :aria-label="item.count + ' ' + item.text + '، ' + item.action"
              class="group flex min-h-11 items-center gap-2.5 rounded-xl border px-3 py-1.5 motion-safe:transition hover:shadow-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
              :class="item.tone === 'warning' ? 'border-warning/30 bg-status-pending' : 'border-brand/20 bg-lavender'">
              <span class="grid size-8 shrink-0 place-items-center rounded-lg bg-white" :class="item.tone === 'warning' ? 'text-warning' : 'text-brand'" aria-hidden="true"><AppIcon :name="item.icon" :size="17" /></span>
              <bdi class="text-lg font-extrabold leading-none text-ink">{{ item.count }}</bdi>
              <span class="min-w-0 flex-1 text-sm font-semibold leading-snug text-ink" :title="item.text">{{ item.short }}</span>
              <AppIcon name="chevron-left" :size="16" class="shrink-0 text-ink-soft group-hover:text-brand-deep" aria-hidden="true" />
            </NuxtLink>
          </li>
        </ul>
        <details v-if="data.unorderableSizes?.length" class="mt-3 rounded-2xl border border-warning/30 bg-white p-4">
          <summary class="min-h-11 cursor-pointer py-2 font-semibold text-warning">عرض الأحجام التي لا يقدر الزبون يطلبها</summary>
          <ul class="mt-2 grid gap-2 sm:grid-cols-2">
            <li v-for="row in data.unorderableSizes" :key="row.productId + '-' + row.size">
              <NuxtLink :to="'/admin/inventory/' + row.productId"
                class="flex min-h-11 items-center justify-between gap-2 rounded-xl border border-lavender-deep bg-white px-3 py-2 text-sm font-semibold text-ink hover:bg-lavender">
                <span class="min-w-0 truncate">{{ row.name }}</span>
                <bdi class="shrink-0 rounded-full bg-status-pending px-2 py-0.5 text-xs text-warning">{{ row.size }}</bdi>
              </NuxtLink>
            </li>
          </ul>
        </details>
      </section>

      <!-- 2. Main figures -->
      <section aria-labelledby="kpi-title" class="mb-8">
        <h2 id="kpi-title" class="mb-3 text-xl font-bold">أرقام المبيعات</h2>
        <dl class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <div v-for="kpi in kpis" :key="kpi.label" class="card flex flex-col gap-1 p-4">
            <span class="grid size-10 place-items-center rounded-xl" :class="kpi.tone" aria-hidden="true"><AppIcon :name="kpi.icon" :size="20" /></span>
            <dt class="mt-2 text-sm font-semibold text-ink-soft">{{ kpi.label }}</dt>
            <dd class="text-xl font-extrabold text-ink sm:text-2xl"><bdi>{{ kpi.value }}</bdi></dd>
            <dd class="text-xs leading-5 text-ink-soft">{{ kpi.hint }}</dd>
            <dd v-if="kpi.label === 'مبيعات آخر 30 يومًا' && weekly" class="mt-1 inline-flex w-fit items-center rounded-full px-2 py-0.5 text-xs font-semibold"
              :class="weekly.up ? 'bg-status-delivered text-success' : 'bg-status-cancelled text-danger'">
              <span aria-hidden="true" class="me-1">{{ weekly.up ? '▲' : '▼' }}</span>{{ weekly.text }}
            </dd>
          </div>
        </dl>
      </section>

      <!-- 3. Orders pipeline -->
      <section v-if="statusRow.length" aria-labelledby="status-strip" class="card mb-8 p-4 sm:p-5">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-2">
          <h2 id="status-strip" class="text-xl font-bold">رحلة الطلبات</h2>
          <NuxtLink :to="{ path: '/admin/orders', query: { status: 'cancelled' } }"
            class="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-semibold text-ink-soft hover:bg-lavender focus-visible:outline-2 focus-visible:outline-brand">
            <span class="badge badge-status-cancelled">ملغي</span><bdi class="font-bold text-ink">{{ cancelled }}</bdi>
          </NuxtLink>
        </div>
        <ol class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <li v-for="(row, index) in statusRow" :key="row.status" class="relative">
            <NuxtLink :to="{ path: '/admin/orders', query: { status: row.query } }"
              class="flex h-full min-h-11 flex-col gap-1 rounded-2xl border border-lavender-deep bg-white p-4 motion-safe:transition hover:-translate-y-0.5 hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand">
              <span class="badge w-fit" :class="statusClasses[row.status]">{{ row.label }}</span>
              <bdi class="mt-1 text-3xl font-extrabold text-ink">{{ row.count }}</bdi>
              <span class="text-xs text-ink-soft">{{ row.hint }}</span>
            </NuxtLink>
            <AppIcon v-if="index < statusRow.length - 1" name="chevron-left" :size="18"
              class="absolute top-1/2 -translate-y-1/2 text-lilac max-lg:hidden ltr:-right-3.5 rtl:-left-3.5 z-10" aria-hidden="true" />
          </li>
        </ol>
      </section>

      <!-- 4. Chart with best sellers and stock beside it -->
      <div class="grid items-start gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <AdminSalesChart />
        <div class="grid gap-6 sm:grid-cols-2 xl:grid-cols-1">
          <section aria-labelledby="top-products" class="card p-4">
            <h2 id="top-products" class="mb-3 text-lg font-bold">الأكثر طلبًا (30 يومًا)</h2>
            <p v-if="!data.topProducts?.length" class="text-sm text-ink-soft">ما فيش طلبات في آخر 30 يومًا.</p>
            <ol v-else class="space-y-2">
              <li v-for="(product, index) in data.topProducts" :key="product.productId" class="flex items-center gap-3">
                <span class="grid size-7 shrink-0 place-items-center rounded-full bg-gradient-brand text-xs font-bold text-white" aria-hidden="true">{{ index + 1 }}</span>
                <NuxtLink :to="'/admin/products/' + product.productId" class="min-w-0 flex-1 truncate py-2 text-sm font-semibold text-ink hover:text-brand">{{ product.name }}</NuxtLink>
                <span class="shrink-0 text-end text-xs text-ink-soft"><bdi class="font-bold text-ink">{{ product.quantity }}</bdi> قطعة<br><bdi>{{ product.value }}</bdi> د.ل</span>
              </li>
            </ol>
          </section>
          <section aria-labelledby="lowest-stock" class="card p-4">
            <div class="mb-3 flex items-center justify-between gap-2">
              <h2 id="lowest-stock" class="text-lg font-bold">أقل مخزون متاح</h2>
              <NuxtLink to="/admin/inventory" class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors">المخزون</NuxtLink>
            </div>
            <p v-if="!data.lowestStock?.length" class="text-sm text-ink-soft">تتبّع المخزون غير مفعّل لأي منتج.</p>
            <ul v-else class="space-y-2">
              <li v-for="row in data.lowestStock" :key="row.productId" class="flex items-center gap-3">
                <NuxtLink :to="'/admin/inventory/' + row.productId" class="min-w-0 flex-1 truncate py-2 text-sm font-semibold text-ink hover:text-brand">{{ row.name }}</NuxtLink>
                <span class="shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold" :class="stockTone(row.available)">
                  <bdi>{{ row.available }}</bdi> {{ row.unit === 'ml' ? 'مل' : 'قطعة' }}
                </span>
              </li>
            </ul>
          </section>
        </div>
      </div>

      <!-- 5. Latest orders -->
      <div class="mb-3 mt-8 flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-xl font-bold">أحدث الطلبات</h2>
        <NuxtLink to="/admin/orders" class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors">كل الطلبات</NuxtLink>
      </div>
      <AdminOrderList :orders="data.recentOrders" />
    </template>
  </section>
</template>

<script setup lang="ts">
import type { AdminDashboard } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'لوحة إدارة أوركيديا' })
const shortcuts = [
  { to: '/admin/orders', label: 'مراجعة الطلبات', icon: 'cart' },
  { to: '/admin/orders/manual', label: 'تسجيل طلب من فيسبوك', icon: 'facebook' },
  { to: '/admin/inventory', label: 'إدارة المخزون', icon: 'droplet' },
  { to: '/admin/delivery', label: 'التوصيل والرواجع', icon: 'truck' },
  { to: '/admin/products', label: 'مراجعة المنتجات', icon: 'bag' }
] as const
const { data, error, status } = useFetch<AdminDashboard>('/api/admin/dashboard', { server: false, cache: 'no-store' })

const todayLabel = new Intl.DateTimeFormat('ar-LY', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'Africa/Tripoli' }).format(new Date())

// Main figures. Money stays as the server's string; the UI never recalculates it.
const kpis = computed(() => {
  const d = data.value
  if (!d) return []
  return [
    { label: 'مبيعات اليوم', value: `${d.valueToday ?? '0.00'} د.ل`, hint: `${d.ordersToday ?? 0} طلب اليوم`, icon: 'sparkle', tone: 'text-brand bg-lavender' },
    { label: 'قيمة آخر 30 يومًا', value: `${d.productsValueLast30Days} د.ل`, hint: `${d.ordersLast30Days ?? 0} طلب (بدون الملغي)`, icon: 'cash', tone: 'text-success bg-status-delivered' },
    { label: 'متوسط قيمة الطلب', value: d.averageOrderValue30Days ? `${d.averageOrderValue30Days} د.ل` : '—', hint: 'آخر 30 يومًا، بدون التوصيل', icon: 'bag', tone: 'text-info bg-status-processing' },
    { label: 'طلبات جديدة تنتظرك', value: String(d.pendingOrders), hint: 'افتح الطلبات الجديدة', icon: 'cart', tone: 'text-warning bg-status-pending', to: '/admin/orders?status=pending' }
  ] as const
})

const weekly = computed(() => {
  const change = data.value?.weeklyChangePercent
  if (change == null) return null
  return { up: change >= 0, text: `${change >= 0 ? '+' : ''}${change}% عن الأسبوع الماضي` }
})

const statusRow = computed(() => {
  const counts = data.value?.statusCounts
  if (!counts) return []
  return ([
    ['Pending', 'pending'], ['Processing', 'processing'], ['Shipped', 'shipped'], ['Delivered', 'delivered'], ['Cancelled', 'cancelled']
  ] as const).map(([status, query]) => ({ status, query, count: counts[status] ?? 0 }))
})
const statusTotal = computed(() => statusRow.value.reduce((sum, row) => sum + row.count, 0))

const extras = computed(() => {
  const d = data.value
  if (!d) return []
  return [
    { label: 'رسائل غير مقروءة', value: String(d.unreadMessages ?? 0), icon: 'mail', to: '/admin/messages', alert: (d.unreadMessages ?? 0) > 0 },
    { label: 'تقييمات الزبائن', value: d.reviewsAverage != null ? `${d.reviewsAverage} ★` : '—', sub: `${d.reviewsCount ?? 0} تقييم ظاهر`, icon: 'star', to: '/admin/reviews', alert: false },
    { label: 'الحسابات', value: String(d.accounts), icon: 'user', to: undefined, alert: false },
    { label: 'منتجات بدون أحجام', value: String(d.productsWithoutVariants), icon: 'bag', to: '/admin/products', alert: d.productsWithoutVariants > 0 }
  ] as const
})

// Same badge colours as the orders list.
const statusClasses: Record<string, string> = {
  Pending: 'badge-status-pending', Processing: 'badge-status-processing', Shipped: 'badge-status-shipped',
  Delivered: 'badge-status-delivered', Cancelled: 'badge-status-cancelled'
}
const statusClass = (status: string) => statusClasses[status] ?? ''

function stockTone(available: string) {
  const value = Number(available)
  return value <= 0 ? 'bg-status-cancelled text-danger' : 'bg-lavender text-brand-deep'
}
</script>

<template>
  <section aria-labelledby="dashboard-title">
    <div class="mb-6 flex flex-wrap items-end justify-between gap-2">
      <div>
        <h1 id="dashboard-title" class="text-3xl font-extrabold">لوحة الإدارة</h1>
        <p class="mt-1 text-sm text-ink-soft">{{ todayLabel }}</p>
      </div>
      <NuxtLink to="/" class="btn-ghost min-h-11 gap-1.5 text-sm"><AppIcon name="sparkle" :size="16" />عرض المتجر</NuxtLink>
    </div>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الملخص…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <nav aria-labelledby="dashboard-shortcuts" class="mb-6">
        <h2 id="dashboard-shortcuts" class="mb-3 text-xl font-semibold">إجراءات الإدارة</h2>
        <ul class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <li v-for="shortcut in shortcuts" :key="shortcut.to" class="last:col-span-2 sm:last:col-span-1">
            <NuxtLink :to="shortcut.to"
              class="flex h-full min-h-11 items-center gap-2 rounded-2xl border border-lavender-deep bg-white p-3 text-start text-sm font-semibold sm:flex-col sm:p-4 sm:text-center text-brand-deep shadow-soft motion-safe:transition hover:-translate-y-0.5 hover:bg-lavender focus-visible:bg-lavender">
              <span class="grid size-9 shrink-0 place-items-center rounded-full bg-lavender text-brand sm:size-10" aria-hidden="true"><AppIcon :name="shortcut.icon" :size="20" /></span>
              {{ shortcut.label }}
            </NuxtLink>
          </li>
        </ul>
      </nav>

      <!-- Sizes customers cannot order until their stock consumption is set -->
      <section v-if="data.unorderableSizes?.length" aria-labelledby="unorderable-sizes" role="alert"
        class="mb-6 rounded-2xl border border-warning/40 bg-status-pending p-4">
        <h2 id="unorderable-sizes" class="text-lg font-bold text-warning">أحجام غير متاحة للطلب: حدد كمية الاستهلاك لكل حجم</h2>
        <ul class="mt-3 grid gap-2 sm:grid-cols-2">
          <li v-for="row in data.unorderableSizes" :key="row.productId + '-' + row.size">
            <NuxtLink :to="'/admin/inventory/' + row.productId"
              class="flex min-h-11 items-center justify-between gap-2 rounded-xl border border-warning/30 bg-white px-3 py-2 text-sm font-semibold text-ink hover:bg-lavender">
              <span class="min-w-0 truncate">{{ row.name }}</span>
              <bdi class="shrink-0 rounded-full bg-status-pending px-2 py-0.5 text-xs text-warning">{{ row.size }}</bdi>
            </NuxtLink>
          </li>
        </ul>
      </section>

      <!-- Main figures -->
      <h2 class="sr-only">الأرقام الرئيسية</h2>
      <dl class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <div v-for="kpi in kpis" :key="kpi.label" class="card relative flex flex-col gap-1 p-4">
          <span class="grid size-10 place-items-center rounded-xl" :class="kpi.tone" aria-hidden="true"><AppIcon :name="kpi.icon" :size="20" /></span>
          <dt class="mt-2 text-sm text-ink-soft">{{ kpi.label }}</dt>
          <dd class="text-2xl font-extrabold text-ink"><bdi>{{ kpi.value }}</bdi></dd>
          <dd class="text-xs text-ink-soft">
            <NuxtLink v-if="'to' in kpi" :to="kpi.to" class="font-semibold text-brand underline after:absolute after:inset-0 after:content-['']">{{ kpi.hint }}</NuxtLink>
            <template v-else>{{ kpi.hint }}</template>
          </dd>
          <dd v-if="kpi.label === 'قيمة آخر 30 يومًا' && weekly" class="mt-1 inline-flex w-fit items-center rounded-full px-2 py-0.5 text-xs font-semibold"
            :class="weekly.up ? 'bg-status-delivered text-success' : 'bg-status-cancelled text-danger'">
            <span aria-hidden="true" class="me-1">{{ weekly.up ? '▲' : '▼' }}</span>{{ weekly.text }}
          </dd>
        </div>
      </dl>

      <!-- Orders by status -->
      <section v-if="statusRow.length" aria-labelledby="status-strip" class="card mt-6 p-4">
        <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 id="status-strip" class="text-lg font-semibold">الطلبات حسب الحالة</h2>
          <span class="text-sm text-ink-soft">الإجمالي <bdi class="font-bold text-ink">{{ statusTotal }}</bdi></span>
        </div>
        <div v-if="statusTotal" class="mb-3 flex h-2.5 overflow-hidden rounded-full bg-lavender" aria-hidden="true">
          <span v-for="row in statusRow" :key="row.status" :class="statusClass(row.status)"
            :style="{ width: (row.count / statusTotal * 100) + '%' }" />
        </div>
        <ul class="grid grid-cols-2 gap-2 sm:grid-cols-5">
          <li v-for="row in statusRow" :key="row.status">
            <NuxtLink :to="{ path: '/admin/orders', query: { status: row.query } }"
              class="flex min-h-11 items-center justify-between gap-2 rounded-xl border border-lavender-deep bg-white px-3 py-2 hover:bg-lavender">
              <span class="badge" :class="statusClass(row.status)">{{ adminOrderStatus(row.status) }}</span>
              <bdi class="text-lg font-bold">{{ row.count }}</bdi>
            </NuxtLink>
          </li>
        </ul>
      </section>

      <!-- Chart with best sellers and stock beside it -->
      <div class="mt-6 grid items-start gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <AdminSalesChart />
        <div class="grid gap-6 sm:grid-cols-2 xl:grid-cols-1">
          <section aria-labelledby="top-products" class="card p-4">
            <h2 id="top-products" class="mb-3 text-lg font-semibold">الأكثر طلبًا (30 يومًا)</h2>
            <p v-if="!data.topProducts?.length" class="text-sm text-ink-soft">ما فيش طلبات في آخر 30 يومًا.</p>
            <ol v-else class="space-y-2">
              <li v-for="(product, index) in data.topProducts" :key="product.productId" class="flex items-center gap-3">
                <span class="grid size-7 shrink-0 place-items-center rounded-full bg-gradient-brand text-xs font-bold text-white" aria-hidden="true">{{ index + 1 }}</span>
                <NuxtLink :to="'/admin/products/' + product.productId" class="min-w-0 flex-1 truncate text-sm font-semibold text-ink hover:underline">{{ product.name }}</NuxtLink>
                <span class="shrink-0 text-end text-xs text-ink-soft"><bdi class="font-bold text-ink">{{ product.quantity }}</bdi> قطعة<br><bdi>{{ product.value }}</bdi> د.ل</span>
              </li>
            </ol>
          </section>
          <section aria-labelledby="lowest-stock" class="card p-4">
            <div class="mb-3 flex items-center justify-between gap-2">
              <h2 id="lowest-stock" class="text-lg font-semibold">أقل مخزون متاح</h2>
              <NuxtLink to="/admin/inventory" class="text-sm text-brand underline">المخزون</NuxtLink>
            </div>
            <p v-if="!data.lowestStock?.length" class="text-sm text-ink-soft">تتبّع المخزون غير مفعّل لأي منتج.</p>
            <ul v-else class="space-y-2">
              <li v-for="row in data.lowestStock" :key="row.productId" class="flex items-center gap-3">
                <NuxtLink :to="'/admin/inventory/' + row.productId" class="min-w-0 flex-1 truncate text-sm font-semibold text-ink hover:underline">{{ row.name }}</NuxtLink>
                <span class="shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold" :class="stockTone(row.available)">
                  <bdi>{{ row.available }}</bdi> {{ row.unit === 'ml' ? 'مل' : 'قطعة' }}
                </span>
              </li>
            </ul>
          </section>
        </div>
      </div>

      <!-- Store health -->
      <dl class="mt-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <div v-for="item in extras" :key="item.label" class="card relative flex items-center gap-3 p-4">
          <span class="grid size-10 shrink-0 place-items-center rounded-xl" :class="item.alert ? 'bg-status-pending text-warning' : 'bg-lavender text-brand'" aria-hidden="true">
            <AppIcon :name="item.icon" :size="20" />
          </span>
          <div class="min-w-0">
            <dt class="text-sm text-ink-soft">
              <NuxtLink v-if="item.to" :to="item.to" class="hover:underline after:absolute after:inset-0 after:content-['']">{{ item.label }}</NuxtLink>
              <template v-else>{{ item.label }}</template>
            </dt>
            <dd class="text-xl font-extrabold text-ink"><bdi>{{ item.value }}</bdi></dd>
            <dd v-if="'sub' in item" class="text-xs text-ink-soft">{{ item.sub }}</dd>
          </div>
        </div>
      </dl>

      <div class="mb-3 mt-8 flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-xl font-semibold">أحدث الطلبات</h2>
        <NuxtLink to="/admin/orders" class="min-h-11 py-2 text-brand underline">كل الطلبات</NuxtLink>
      </div>
      <AdminOrderList :orders="data.recentOrders" />
    </template>
  </section>
</template>

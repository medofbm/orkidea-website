<script setup lang="ts">
import type { AdminDashboard } from '~/types/admin'
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'لوحة إدارة أوركيديا' })
const shortcuts = [
  { to: '/admin/orders', label: 'مراجعة الطلبات' },
  { to: '/admin/orders/manual', label: 'تسجيل طلب من فيسبوك' },
  { to: '/admin/inventory', label: 'إدارة المخزون' },
  { to: '/admin/delivery', label: 'التوصيل والرواجع' },
  { to: '/admin/products', label: 'مراجعة المنتجات' }
]
const { data, error, status } = useFetch<AdminDashboard>('/api/admin/dashboard', { server: false, cache: 'no-store' })
const stats = computed(() => data.value ? [
  { label: 'قيمة المنتجات المطلوبة خلال 30 يومًا', value: `${data.value.productsValueLast30Days} د.ل`, icon: 'cash' as const, border: 'border-s-brand', text: 'text-brand' },
  { label: 'طلبات جديدة', value: data.value.pendingOrders, icon: 'cart' as const, border: 'border-s-warning', text: 'text-warning' },
  { label: 'منتجات بدون أحجام', value: data.value.productsWithoutVariants, icon: 'bag' as const, border: 'border-s-info', text: 'text-info' },
  { label: 'الحسابات', value: data.value.accounts, icon: 'user' as const, border: 'border-s-success', text: 'text-success' }
] : [])
</script>

<template>
  <section aria-labelledby="dashboard-title">
    <h1 id="dashboard-title" class="mb-6 text-3xl font-extrabold">لوحة الإدارة</h1>
    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الملخص…</p>
    <AdminReadError v-else-if="error" :status-code="error.statusCode" />
    <template v-else-if="data">
      <nav aria-labelledby="dashboard-shortcuts" class="mb-6">
        <h2 id="dashboard-shortcuts" class="mb-3 text-xl font-semibold">إجراءات الإدارة</h2>
        <ul class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          <li v-for="shortcut in shortcuts" :key="shortcut.to">
            <NuxtLink :to="shortcut.to"
              class="flex min-h-11 items-center rounded-lg border border-lavender-deep bg-white px-3 py-2 font-semibold text-brand-deep hover:bg-lavender focus-visible:bg-lavender">
              {{ shortcut.label }}
            </NuxtLink>
          </li>
        </ul>
      </nav>
      <dl class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <div v-for="stat in stats" :key="stat.label" class="card border-s-4 p-4" :class="stat.border">
          <div class="flex items-center gap-2" :class="stat.text">
            <AppIcon :name="stat.icon" :size="20" />
          </div>
          <dt class="mt-2 text-sm text-ink-soft">{{ stat.label }}</dt>
          <dd class="mt-1 text-2xl font-extrabold text-ink">{{ stat.value }}</dd>
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

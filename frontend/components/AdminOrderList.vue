<script setup lang="ts">
import type { AdminOrderSummary } from '~/types/admin'
defineProps<{ orders: AdminOrderSummary[], returnQuery?: { status?: string, source?: string, page?: string } }>()

</script>

<template>
  <p v-if="!orders.length" class="rounded-xl bg-white p-4">لا توجد طلبات للعرض.</p>
  <ul v-else class="grid gap-3">
    <li v-for="order in orders" :key="order.id" class="rounded-xl border border-lavender-deep bg-white">
      <NuxtLink :to="{ path: '/admin/orders/' + order.id, query: returnQuery }"
        class="grid gap-2 rounded-xl p-4 hover:bg-lavender sm:grid-cols-[1fr_auto]">
        <div>
          <p class="font-semibold">طلب #{{ order.id }} · {{ order.customerName }}</p>
          <p class="mt-1 text-sm text-brand">{{ order.city }} · {{ order.source }}</p>
          <p class="mt-0.5 text-xs text-ink-soft"><time :datetime="order.orderDate">{{ adminOrderDate(order.orderDate) }}</time></p>
        </div>
        <div class="flex flex-wrap items-center gap-3 sm:justify-end">
          <span class="badge" :class="adminOrderStatusClass(order.status)">{{ adminOrderStatus(order.status) }}</span>
          <span class="font-semibold">{{ order.total }} د.ل</span>
        </div>
      </NuxtLink>
    </li>
  </ul>
</template>

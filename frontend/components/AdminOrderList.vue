<script setup lang="ts">
import type { AdminOrderSummary } from '~/types/admin'
defineProps<{ orders: AdminOrderSummary[] }>()
</script>

<template>
  <p v-if="!orders.length" class="rounded-xl bg-white p-4">لا توجد طلبات للعرض.</p>
  <ul v-else class="grid gap-3">
    <li v-for="order in orders" :key="order.id" class="rounded-xl border border-purple-100 bg-white">
      <NuxtLink :to="'/admin/orders/' + order.id"
        class="grid gap-2 rounded-xl p-4 hover:bg-purple-50 sm:grid-cols-[1fr_auto]">
        <div>
          <p class="font-semibold">طلب #{{ order.id }} · {{ order.customerName }}</p>
          <p class="mt-1 text-sm text-purple-800">{{ order.city }} · {{ order.source }}</p>
        </div>
        <div class="flex flex-wrap items-center gap-3 sm:justify-end">
          <span class="rounded-full bg-purple-100 px-3 py-1 text-sm">{{ adminOrderStatus(order.status) }}</span>
          <span class="font-semibold">{{ order.total }} د.ل</span>
        </div>
      </NuxtLink>
    </li>
  </ul>
</template>

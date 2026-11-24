<script setup lang="ts">
import type { AdminStockAccount } from '~/types/admin'

const props = defineProps<{ account: AdminStockAccount }>()
// Available is what can still be sold; it turns red at zero or below.
const empty = computed(() => Number(props.account.available) <= 0)
</script>

<template>
  <dl class="grid grid-cols-2 gap-2 sm:grid-cols-3">
    <div class="col-span-2 flex items-center justify-between gap-2 rounded-xl p-3 sm:col-span-1 sm:block"
      :class="empty ? 'bg-status-cancelled text-danger' : 'bg-status-delivered text-success'">
      <dt class="text-sm font-semibold">المتاح للبيع</dt>
      <dd class="text-xl font-extrabold"><bdi>{{ account.available }}</bdi> <span class="text-sm">{{ adminStockUnit(account.unit) }}</span></dd>
    </div>
    <div class="rounded-xl bg-lavender p-3">
      <dt class="text-sm">الفعلي</dt><dd class="font-semibold"><bdi>{{ account.onHand }}</bdi> {{ adminStockUnit(account.unit) }}</dd>
    </div>
    <div class="rounded-xl bg-lavender p-3">
      <dt class="text-sm">المحجوز للطلبات</dt><dd class="font-semibold"><bdi>{{ account.reserved }}</bdi> {{ adminStockUnit(account.unit) }}</dd>
    </div>
  </dl>
</template>

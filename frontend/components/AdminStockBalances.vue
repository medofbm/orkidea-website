<script setup lang="ts">
import type { AdminStockAccount } from '~/types/admin'

defineProps<{ account: AdminStockAccount }>()
</script>

<template>
  <dl class="grid grid-cols-3 gap-2">
    <div class="rounded-lg bg-slate-50 p-2">
      <dt class="text-sm">الفعلي</dt><dd class="font-semibold"><bdi>{{ account.onHand }}</bdi> {{ adminStockUnit(account.unit) }}</dd>
    </div>
    <div class="rounded-lg bg-slate-50 p-2">
      <dt class="text-sm">المحجوز</dt><dd class="font-semibold"><bdi>{{ account.reserved }}</bdi> {{ adminStockUnit(account.unit) }}</dd>
    </div>
    <div class="rounded-lg bg-purple-50 p-2">
      <dt class="text-sm">المتاح</dt><dd class="font-semibold"><bdi>{{ account.available }}</bdi> {{ adminStockUnit(account.unit) }}</dd>
    </div>
  </dl>
</template>

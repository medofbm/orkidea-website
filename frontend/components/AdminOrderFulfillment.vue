<script setup lang="ts">
import type { AdminOrderDetails } from '~/types/admin'

const props = defineProps<{ order: AdminOrderDetails, disabled: boolean }>()
const emit = defineEmits<{
  stock: [operation: 'Reserve' | 'Consume']
  delivery: [body: Record<string, string | boolean | null>]
}>()

const stockLabels: Record<string, string> = {
  Untracked: 'طلب غير متتبع بالمخزون',
  AwaitingConfirmation: 'بانتظار التأكيد والحجز',
  Reserved: 'الكمية محجوزة',
  Filled: 'تمت التعبئة وصرف الكمية',
  Cancelled: 'ملغي بلا حجز'
}
const actions = {
  HandOver: 'تسجيل تسليم الطلب للتوصيل',
  Deliver: 'تأكيد استلام العميل',
  MarkReturning: 'تسجيل عدم استلام العميل',
  ReceiveReturn: 'تأكيد استلام الراجع',
  Resume: 'استئناف التوصيل',
  Redispatch: 'إعادة إرسال نفس الطلبية',
  CorrectDetails: 'تصحيح بيانات الشحنة',
  CorrectState: 'تصحيح تأكيد الاستلام'
}
type DeliveryAction = keyof typeof actions
const byState: Record<string, DeliveryAction[]> = {
  HandedOver: ['Deliver', 'MarkReturning', 'CorrectDetails'],
  Returning: ['ReceiveReturn', 'Resume', 'CorrectDetails'],
  Returned: ['Redispatch', 'CorrectDetails', 'CorrectState'],
  Delivered: ['CorrectDetails', 'CorrectState']
}

const stockAction = computed(() => {
  if (props.order.delivery || !['Pending', 'Processing'].includes(props.order.order.status)) return null
  if (props.order.stockState === 'AwaitingConfirmation') return 'Reserve'
  if (props.order.stockState === 'Reserved') return 'Consume'
  return null
})
const deliveryActions = computed<DeliveryAction[]>(() => {
  if (props.order.delivery) return byState[props.order.delivery.state] ?? []
  return props.order.order.status === 'Processing' && props.order.items.length &&
    ['Untracked', 'Filled'].includes(props.order.stockState) ? ['HandOver'] : []
})

const action = ref<DeliveryAction | ''>('')
const mode = ref<'Company' | 'Self'>('Company')
const carrierName = ref('')
const reference = ref('')
const reason = ref('')
const confirmed = ref(false)
const metadata = computed(() => ['HandOver', 'Redispatch', 'CorrectDetails'].includes(action.value))
const requiresReason = computed(() =>
  ['MarkReturning', 'Resume', 'Redispatch', 'CorrectDetails', 'CorrectState'].includes(action.value))
const confirmationLabel = computed(() => {
  if (action.value === 'HandOver' && props.order.stockState === 'Untracked')
    return 'راجعت البنود وتأكدت من تجهيز الطلب يدوياً.'
  if (action.value === 'ReceiveReturn') return 'استلمت الراجع فعلياً وراجعت محتوياته.'
  if (action.value === 'Redispatch') return 'راجعت سلامة نفس الطلبية وهي صالحة للإرسال لنفس العميل.'
  return ''
})
watch(action, value => {
  mode.value = props.order.delivery?.mode ?? 'Company'
  carrierName.value = props.order.delivery?.carrierName ?? (value === 'CorrectDetails' ? '' : 'درب السبيل')
  reference.value = value === 'CorrectDetails' ? props.order.delivery?.shipmentReference ?? '' : ''
  reason.value = ''
  confirmed.value = false
})

function submitDelivery() {
  const current = action.value
  if (!current || props.disabled || !deliveryActions.value.includes(current) ||
      (confirmationLabel.value && !confirmed.value) ||
      (requiresReason.value && !reason.value.trim()) ||
      (metadata.value && mode.value === 'Company' && !carrierName.value.trim())) return

  emit('delivery', {
    action: current,
    requestId: crypto.randomUUID(),
    mode: metadata.value ? mode.value : null,
    carrierName: metadata.value && mode.value === 'Company' ? carrierName.value.trim() : null,
    shipmentReference: metadata.value ? reference.value.trim() || null : null,
    reason: requiresReason.value ? reason.value.trim() : null,
    targetState: current === 'CorrectState'
      ? (props.order.delivery?.state === 'Delivered' ? 'HandedOver' : 'Returning') : null,
    confirmLegacyPrepared: current === 'HandOver' && props.order.stockState === 'Untracked' && confirmed.value,
    confirmReturnReceived: current === 'ReceiveReturn' && confirmed.value,
    confirmSamePackageReusable: current === 'Redispatch' && confirmed.value
  })
}
</script>

<template>
  <section aria-labelledby="order-stock-title" class="mt-6 rounded-xl bg-white p-4">
    <h2 id="order-stock-title" class="mb-3 text-xl font-bold">المخزون والتعبئة</h2>
    <p>{{ stockLabels[order.stockState] ?? 'حالة المخزون غير معروفة' }}</p>
    <button v-if="stockAction" type="button" :disabled="disabled" @click="emit('stock', stockAction)"
      class="mt-3 min-h-11 rounded-lg bg-purple-900 px-4 font-semibold text-white disabled:opacity-60">
      {{ stockAction === 'Reserve' ? 'تأكيد التوفر وحجز الكمية' : 'تمت التعبئة — صرف الكمية' }}
    </button>
  </section>

  <section aria-labelledby="order-delivery-title" class="mt-6 rounded-xl bg-white p-4">
    <h2 id="order-delivery-title" class="mb-3 text-xl font-bold">التوصيل والرواجع</h2>
    <p v-if="order.delivery">
      {{ adminDeliveryState(order.delivery.state) }}
      · {{ order.delivery.mode === 'Self' ? 'توصيل ذاتي' : order.delivery.carrierName }}
      <bdi v-if="order.delivery.shipmentReference"> · {{ order.delivery.shipmentReference }}</bdi>
    </p>
    <p v-else-if="deliveryActions.length">سجّل التسليم بعد ما تعطي الطلب لشركة التوصيل أو تبدأ توصله بنفسك.</p>
    <p v-else>تقدر تسجّل التوصيل بعد تجهيز الطلب.</p>
    <p v-if="order.delivery?.state === 'Returned'" class="mt-2 text-sm text-slate-700">
      استلام الراجع وإعادة إرساله ما يغيروش كمية الزيت المصروفة.
    </p>
    <form v-if="deliveryActions.length" class="mt-4" @submit.prevent="submitDelivery">
      <fieldset :disabled="disabled" class="space-y-3">
        <label class="block">
          <span class="block font-semibold">إجراء التوصيل</span>
          <select v-model="action" class="min-h-11 w-full rounded-lg border border-slate-400 p-2">
            <option value="" disabled>اختر الإجراء</option>
            <option v-for="value in deliveryActions" :key="value" :value="value">{{ actions[value] }}</option>
          </select>
        </label>
        <template v-if="metadata">
          <label class="block">
            <span class="block font-semibold">طريقة التوصيل</span>
            <select v-model="mode" class="min-h-11 w-full rounded-lg border border-slate-400 p-2">
              <option value="Company">شركة توصيل</option>
              <option value="Self">توصيل ذاتي</option>
            </select>
          </label>
          <label v-if="mode === 'Company'" class="block">
            <span class="block font-semibold">شركة التوصيل</span>
            <input v-model="carrierName" required maxlength="120" class="min-h-11 w-full rounded-lg border border-slate-400 p-2">
          </label>
          <label class="block">
            <span class="block font-semibold">مرجع الشحنة (اختياري)</span>
            <input v-model="reference" maxlength="100" class="min-h-11 w-full rounded-lg border border-slate-400 p-2">
          </label>
        </template>
        <label v-if="requiresReason" class="block">
          <span class="block font-semibold">سبب الإجراء</span>
          <textarea v-model="reason" required maxlength="500" rows="3" class="w-full rounded-lg border border-slate-400 p-2" />
        </label>
        <label v-if="confirmationLabel" class="flex min-h-11 items-center gap-2">
          <input v-model="confirmed" type="checkbox" required>
          {{ confirmationLabel }}
        </label>
        <button v-if="action" type="submit" class="min-h-11 rounded-lg bg-purple-900 px-4 font-semibold text-white disabled:opacity-60">
          {{ actions[action] }}
        </button>
      </fieldset>
    </form>

    <details v-if="order.deliveryHistory.length" class="mt-4">
      <summary class="min-h-11 cursor-pointer py-2 font-semibold">سجل التوصيل (آخر {{ order.deliveryHistory.length }} إجراء)</summary>
      <ol class="divide-y divide-slate-200">
        <li v-for="entry in order.deliveryHistory" :key="entry.revision" class="py-3">
          <p>{{ actions[entry.action as DeliveryAction] ?? entry.action }} · {{ adminDeliveryState(entry.newState) }}</p>
          <p>
            {{ entry.mode === 'Self' ? 'توصيل ذاتي' : entry.carrierName }}
            <bdi v-if="entry.shipmentReference"> · {{ entry.shipmentReference }}</bdi>
          </p>
          <p class="text-sm text-slate-700">
            <bdi>{{ entry.actorName ?? 'حساب إداري سابق' }}</bdi> ·
            <time :datetime="entry.changedAtUtc" dir="ltr">{{ entry.changedAtUtc.replace('T', ' ').slice(0, 16) }} UTC</time>
          </p>
          <p v-if="entry.reason" class="whitespace-pre-line break-words">{{ entry.reason }}</p>
        </li>
      </ol>
    </details>
  </section>
</template>

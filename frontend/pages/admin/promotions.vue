<script setup lang="ts">
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'العروض' })

// The owner turns the free-delivery promotion on and off here. Times are typed in Tripoli time;
// the server converts them to UTC and alone decides which orders get free delivery.
type Status = 'active' | 'scheduled' | 'ended' | 'stopped'
type Promotion = {
  isEnabled: boolean, title: string, scope: 'Tripoli' | 'AllLibya', startsAtLocal: string, endsAtLocal: string,
  minSubtotal: string | null, updatedAtUtc: string, status: Status
}
const statusLabels: Record<Status, string> = { active: 'شغال توا', scheduled: 'مجدول', ended: 'منتهي', stopped: 'موقف' }
const statusClass: Record<Status, string> = {
  active: 'bg-green-100 text-green-800', scheduled: 'bg-amber-100 text-amber-800',
  ended: 'bg-gray-100 text-gray-700', stopped: 'bg-gray-100 text-gray-700'
}

const toast = useToast()
const { data, status, error } = useFetch<{ promotion: Promotion | null }>('/api/admin/promotions', { server: false, cache: 'no-store' })
const form = reactive({ isEnabled: false, title: 'عرض الجمعة البيضاء', scope: 'Tripoli' as Promotion['scope'],
  startsAtLocal: '', endsAtLocal: '', minSubtotal: '' })
const busy = ref(false)
const current = computed(() => data.value?.promotion ?? null)

watch(current, promotion => {
  if (!promotion) return
  Object.assign(form, {
    isEnabled: promotion.isEnabled, title: promotion.title, scope: promotion.scope,
    startsAtLocal: promotion.startsAtLocal, endsAtLocal: promotion.endsAtLocal, minSubtotal: promotion.minSubtotal ?? ''
  })
}, { immediate: true })

async function save() {
  if (busy.value) return
  busy.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ promotion?: Promotion, error?: string }>('/api/admin/promotions', {
      method: 'POST', retry: 0, ignoreResponseError: true,
      headers: { RequestVerificationToken: session.requestToken },
      body: { ...form, minSubtotal: String(form.minSubtotal ?? '').trim() || null }
    })
    if (!response.ok || !response._data?.promotion) throw new Error(response._data?.error || 'تعذر حفظ العرض.')
    data.value = { promotion: response._data.promotion }
    toast.success('تم حفظ العرض.')
  } catch (e) {
    toast.error((e as Error).message)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <section class="card p-4 sm:p-5">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-lg font-bold">توصيل مجاني</h2>
        <span v-if="current" class="rounded-full px-3 py-1 text-sm font-semibold" :class="statusClass[current.status]">
          {{ statusLabels[current.status] }}
        </span>
      </div>
      <p class="mt-1 text-sm text-gray-600">
        لما يكون العرض شغال، طلبات الموقع في المكان المختار يكون توصيلها مجاني. طلبات فيسبوك اليدوية ما تتأثرش.
        الأوقات بتوقيت طرابلس.
      </p>
      <AdminReadError v-if="error" class="mt-3" :status-code="error.statusCode" />
      <p v-else-if="status === 'pending'" class="mt-3 text-sm text-gray-600">جاري التحميل…</p>
      <form v-else class="mt-4 grid max-w-xl gap-4" @submit.prevent="save">
        <label class="flex min-h-11 items-center gap-2 font-semibold">
          <input v-model="form.isEnabled" type="checkbox" class="size-5">
          العرض مفعّل
        </label>
        <label class="block text-sm font-semibold">اسم العرض
          <input v-model="form.title" required maxlength="80" class="field mt-1 block w-full">
        </label>
        <fieldset>
          <legend class="text-sm font-semibold">المكان</legend>
          <div class="mt-1 flex flex-wrap gap-4">
            <label class="flex min-h-11 items-center gap-2"><input v-model="form.scope" type="radio" value="Tripoli"> طرابلس فقط</label>
            <label class="flex min-h-11 items-center gap-2"><input v-model="form.scope" type="radio" value="AllLibya"> كل ليبيا</label>
          </div>
        </fieldset>
        <div class="grid gap-4 sm:grid-cols-2">
          <label class="block text-sm font-semibold">يبدا
            <input v-model="form.startsAtLocal" required type="datetime-local" class="field mt-1 block w-full">
          </label>
          <label class="block text-sm font-semibold">ينتهي
            <input v-model="form.endsAtLocal" required type="datetime-local" class="field mt-1 block w-full">
          </label>
        </div>
        <label class="block max-w-xs text-sm font-semibold">أقل قيمة للمنتجات (د.ل، اختياري)
          <input v-model="form.minSubtotal" type="number" inputmode="decimal" min="0" step="0.5" class="field mt-1 block w-full">
          <span class="mt-1 block text-xs font-normal text-gray-600">فاضي = بدون حد أدنى.</span>
        </label>
        <div>
          <button type="submit" class="btn-primary min-h-11 px-4" :disabled="busy">{{ busy ? 'جاري الحفظ…' : 'حفظ العرض' }}</button>
        </div>
      </form>
    </section>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'إدارة التقييمات' })

type Review = { id: number, customerName: string, message: string, rating: number, isVisible: boolean }
const toast = useToast()
const confirm = useConfirm()
const { data, status, error, refresh } = useFetch<{ items: Review[] }>('/api/admin/reviews', { server: false, cache: 'no-store' })
const items = ref<Review[]>([])
const filter = ref<'all' | 'pending' | 'visible'>('all')
const busy = ref<number[]>([])
const filters = [
  { key: 'all', label: 'الكل' },
  { key: 'pending', label: 'بانتظار المراجعة' },
  { key: 'visible', label: 'ظاهرة' }
] as const

watch(data, value => { if (value) items.value = value.items.map(item => ({ ...item })) }, { immediate: true })
const shown = computed(() => items.value.filter(item =>
  filter.value === 'all' || (filter.value === 'visible' ? item.isVisible : !item.isVisible)))

async function send(path: string, body?: Record<string, unknown>) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  return await $fetch.raw<{ message?: string }>(path, {
    method: 'POST', retry: 0, ignoreResponseError: true, body,
    headers: { RequestVerificationToken: session.requestToken }
  })
}

async function setVisible(review: Review, visible: boolean) {
  if (busy.value.includes(review.id)) return
  busy.value.push(review.id)
  const previous = review.isVisible
  review.isVisible = visible
  try {
    const response = await send('/api/admin/reviews/' + review.id + '/visibility', { visible })
    if (!response.ok) throw new Error(response._data?.message)
    toast.success(visible ? 'تم إظهار التقييم.' : 'تم إخفاء التقييم.')
  } catch (e) {
    review.isVisible = previous
    toast.error((e as Error).message || 'تعذر تحديث التقييم. حاول مرة أخرى.')
  } finally {
    busy.value = busy.value.filter(id => id !== review.id)
  }
}

async function remove(review: Review) {
  if (busy.value.includes(review.id)) return
  const ok = await confirm({ title: 'حذف التقييم؟', message: 'سيُحذف التقييم نهائيًا ولا يمكن التراجع.', confirmLabel: 'حذف', tone: 'danger' })
  if (!ok) return
  busy.value.push(review.id)
  const snapshot = items.value
  items.value = items.value.filter(item => item.id !== review.id)
  try {
    const response = await send('/api/admin/reviews/' + review.id + '/delete')
    if (!response.ok) throw new Error(response._data?.message)
    toast.success('تم حذف التقييم.')
  } catch (e) {
    items.value = snapshot
    toast.error((e as Error).message || 'تعذر حذف التقييم. حاول مرة أخرى.')
  } finally {
    busy.value = busy.value.filter(id => id !== review.id)
  }
}
</script>

<template>
  <section aria-labelledby="reviews-title" class="mx-auto max-w-4xl">
    <h1 id="reviews-title" class="mb-2 text-3xl font-extrabold">التقييمات</h1>
    <p class="mb-5 text-ink-soft">التقييمات الجديدة تظهر في المتجر بعد إظهارها من هنا. اسم العميلة يظهر لك فقط.</p>

    <div role="group" aria-label="تصفية التقييمات" class="mb-5 flex flex-wrap gap-2">
      <button v-for="option in filters" :key="option.key" type="button" :aria-pressed="filter === option.key"
        class="chip border border-transparent"
        :class="filter === option.key ? '!bg-brand !text-white' : 'hover:border-lavender-deep'"
        @click="filter = option.key">{{ option.label }}</button>
    </div>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل التقييمات…</p>
    <div v-else-if="error">
      <AdminReadError :status-code="error.statusCode" />
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <p v-else-if="!shown.length" role="status" class="card p-6 text-center text-ink-soft">ما فيش تقييمات هنا.</p>
    <ul v-else class="grid gap-3">
      <li v-for="review in shown" :key="review.id" class="card p-4">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div class="flex min-w-0 items-center gap-3">
            <p class="break-words font-bold">{{ review.customerName }}</p>
            <span class="flex items-center gap-0.5 text-gold" role="img" :aria-label="review.rating + ' من 5'">
              <AppIcon v-for="n in 5" :key="n" :name="n <= review.rating ? 'star-filled' : 'star'" :size="16"
                :class="n <= review.rating ? '' : 'text-lavender-deep'" />
            </span>
          </div>
          <span class="badge" :class="review.isVisible ? 'badge-status-delivered' : 'badge-status-pending'">
            {{ review.isVisible ? 'ظاهرة' : 'بانتظار المراجعة' }}
          </span>
        </div>
        <p class="mt-2 whitespace-pre-line break-words">{{ review.message }}</p>
        <div class="mt-3 flex flex-wrap gap-2">
          <button type="button" class="btn-secondary" :disabled="busy.includes(review.id)"
            @click="setVisible(review, !review.isVisible)">{{ review.isVisible ? 'إخفاء' : 'إظهار' }}</button>
          <button type="button" class="btn-ghost !text-danger" :disabled="busy.includes(review.id)"
            :aria-label="'حذف تقييم ' + review.customerName" @click="remove(review)">حذف</button>
        </div>
      </li>
    </ul>
  </section>
</template>

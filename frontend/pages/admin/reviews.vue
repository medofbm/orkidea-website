<script setup lang="ts">
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'إدارة التقييمات' })

type Review = { id: number, customerName: string, message: string, rating: number, isVisible: boolean, source?: 'facebook' | 'site', allowName?: boolean, createdAtUtc?: string, orderId?: number | null }
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

const dateFormatter = new Intl.DateTimeFormat('ar-LY', { dateStyle: 'medium', timeZone: 'Africa/Tripoli' })
function created(value?: string) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : dateFormatter.format(date)
}

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
  <section aria-labelledby="reviews-title" class="mx-auto max-w-6xl">
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
    <ul v-else class="grid gap-3 lg:grid-cols-2">
      <li v-for="review in shown" :key="review.id" class="card border-s-4 p-4 sm:p-6"
        :class="review.isVisible ? 'border-success' : 'border-warning bg-status-pending'">
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
        <p class="mt-2 flex flex-wrap items-center gap-2 text-sm text-ink-soft">
          <span class="chip">{{ review.source === 'facebook' ? 'فيسبوك' : 'الموقع' }}</span>
          <time v-if="created(review.createdAtUtc)" :datetime="review.createdAtUtc">{{ created(review.createdAtUtc) }}</time>
          <NuxtLink v-if="review.orderId" :to="'/admin/orders/' + review.orderId"
            class="inline-flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-white px-3 text-sm font-semibold text-brand hover:bg-lavender focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand motion-safe:transition-colors">طلب <bdi>#{{ review.orderId }}</bdi></NuxtLink>
        </p>
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

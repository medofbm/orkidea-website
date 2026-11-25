<script setup lang="ts">
definePageMeta({ layout: 'admin', middleware: 'admin' })
useSeoMeta({ title: 'رسائل التواصل' })

type Message = { id: number, name: string, email: string, phoneNumber: string, message: string, receivedAt: string, isRead?: boolean }
const toast = useToast()
const confirm = useConfirm()
const { data, status, error, refresh } = useFetch<{ items: Message[] }>('/api/admin/contact-messages', { server: false, cache: 'no-store' })
const items = ref<Message[]>([])
const busy = ref<number[]>([])
const unreadOnly = ref(false)
const shown = computed(() => unreadOnly.value ? items.value.filter(item => !item.isRead) : items.value)
const unreadCount = computed(() => items.value.filter(item => !item.isRead).length)
watch(data, value => { if (value) items.value = value.items.map(item => ({ ...item })) }, { immediate: true })

const formatter = new Intl.DateTimeFormat('ar-LY', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Tripoli' })
function when(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : formatter.format(date)
}

async function setRead(message: Message, read: boolean) {
  if (busy.value.includes(message.id)) return
  busy.value.push(message.id)
  const previous = !!message.isRead
  message.isRead = read
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ message?: string }>('/api/admin/contact-messages/' + message.id + '/read', {
      method: 'POST', retry: 0, ignoreResponseError: true, body: { read },
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!response.ok) throw new Error(response._data?.message)
  } catch (e) {
    message.isRead = previous
    toast.error((e as Error).message || 'تعذر تحديث حالة الرسالة. حاول مرة أخرى.')
  } finally {
    busy.value = busy.value.filter(id => id !== message.id)
  }
}

async function remove(message: Message) {
  if (busy.value.includes(message.id)) return
  const ok = await confirm({ title: 'حذف الرسالة؟', message: 'ستُحذف الرسالة نهائيًا ولا يمكن التراجع.', confirmLabel: 'حذف', tone: 'danger' })
  if (!ok) return
  busy.value.push(message.id)
  const snapshot = items.value
  items.value = items.value.filter(item => item.id !== message.id)
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch.raw<{ message?: string }>('/api/admin/contact-messages/' + message.id + '/delete', {
      method: 'POST', retry: 0, ignoreResponseError: true,
      headers: { RequestVerificationToken: session.requestToken }
    })
    if (!response.ok) throw new Error(response._data?.message)
    toast.success('تم حذف الرسالة.')
  } catch (e) {
    items.value = snapshot
    toast.error((e as Error).message || 'تعذر حذف الرسالة. حاول مرة أخرى.')
  } finally {
    busy.value = busy.value.filter(id => id !== message.id)
  }
}
</script>

<template>
  <section aria-labelledby="messages-title" class="mx-auto max-w-6xl">
    <h1 id="messages-title" class="mb-2 text-3xl font-extrabold">الرسائل</h1>
    <p class="mb-5 text-ink-soft">رسائل نموذج التواصل، الأحدث أولًا.</p>

    <div role="group" aria-label="تصفية الرسائل" class="mb-5 flex flex-wrap gap-2">
      <button type="button" :aria-pressed="unreadOnly" class="chip border border-transparent"
        :class="unreadOnly ? '!bg-brand !text-white' : 'hover:border-lavender-deep'"
        @click="unreadOnly = !unreadOnly">غير مقروءة<template v-if="unreadCount"> (<bdi>{{ unreadCount }}</bdi>)</template></button>
    </div>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الرسائل…</p>
    <div v-else-if="error">
      <AdminReadError :status-code="error.statusCode" />
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <p v-else-if="!shown.length" role="status" class="card p-6 text-center text-ink-soft">{{ unreadOnly ? 'ما فيش رسائل غير مقروءة.' : 'ما فيش رسائل جديدة.' }}</p>
    <ul v-else class="grid gap-3 lg:grid-cols-2">
      <li v-for="message in shown" :key="message.id" class="card border-s-4 p-4 sm:p-6"
        :class="message.isRead ? 'border-transparent opacity-90' : 'border-brand'">
        <div class="flex flex-wrap items-start justify-between gap-2">
          <p class="flex min-w-0 items-center gap-2 break-words" :class="message.isRead ? 'font-medium' : 'font-extrabold'">
            <span v-if="!message.isRead" aria-hidden="true" class="size-2.5 shrink-0 rounded-full bg-brand" />
            <span>{{ message.name }}</span>
            <span v-if="!message.isRead" class="sr-only">غير مقروءة</span>
          </p>
          <time class="text-sm text-ink-soft" :datetime="message.receivedAt">{{ when(message.receivedAt) }}</time>
        </div>
        <div class="mt-1 text-sm">
          <a v-if="message.phoneNumber" :href="'tel:' + message.phoneNumber" dir="ltr"
            class="inline-flex min-h-11 items-center text-brand underline">{{ message.phoneNumber }}</a>
          <div v-if="message.phoneNumber" class="mb-2 flex flex-wrap gap-2">
            <a :href="'tel:+' + adminPhoneDigits(message.phoneNumber)" class="btn-secondary inline-flex items-center gap-1 text-sm">
              <AppIcon name="phone" :size="16" />اتصال</a>
            <a :href="'https://wa.me/' + adminPhoneDigits(message.phoneNumber)" target="_blank" rel="noopener noreferrer"
              class="btn-secondary inline-flex items-center gap-1 text-sm"><AppIcon name="whatsapp" :size="16" />واتساب</a>
          </div>
          <a v-if="message.email" :href="'mailto:' + message.email" dir="ltr"
            class="inline-flex min-h-11 items-center break-all text-brand underline">{{ message.email }}</a>
        </div>
        <p class="whitespace-pre-line break-words" :class="message.isRead ? '' : 'font-bold'">{{ message.message }}</p>
        <div class="mt-3 flex flex-wrap gap-2">
          <button type="button" class="btn-secondary" :disabled="busy.includes(message.id)"
            @click="setRead(message, !message.isRead)">{{ message.isRead ? 'تعليم كغير مقروءة' : 'تعليم كمقروءة' }}</button>
          <button type="button" class="btn-ghost !text-danger" :disabled="busy.includes(message.id)"
            :aria-label="'حذف رسالة ' + message.name" @click="remove(message)">حذف</button>
        </div>
      </li>
    </ul>
  </section>
</template>

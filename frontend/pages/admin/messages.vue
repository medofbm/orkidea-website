<script setup lang="ts">
definePageMeta({ layout: 'admin' })
useSeoMeta({ title: 'رسائل التواصل' })

type Message = { id: number, name: string, email: string, phoneNumber: string, message: string, receivedAt: string }
const toast = useToast()
const confirm = useConfirm()
const { data, status, error, refresh } = useFetch<{ items: Message[] }>('/api/admin/contact-messages', { server: false, cache: 'no-store' })
const items = ref<Message[]>([])
const busy = ref<number[]>([])
watch(data, value => { if (value) items.value = value.items.map(item => ({ ...item })) }, { immediate: true })

const formatter = new Intl.DateTimeFormat('ar-LY', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Tripoli' })
function when(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : formatter.format(date)
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
  <section aria-labelledby="messages-title" class="mx-auto max-w-4xl">
    <h1 id="messages-title" class="mb-2 text-3xl font-extrabold">الرسائل</h1>
    <p class="mb-5 text-ink-soft">رسائل نموذج التواصل، الأحدث أولًا.</p>

    <p v-if="status === 'idle' || status === 'pending'" role="status">جاري تحميل الرسائل…</p>
    <div v-else-if="error">
      <AdminReadError :status-code="error.statusCode" />
      <button type="button" class="btn-secondary mt-3" @click="refresh()">إعادة المحاولة</button>
    </div>
    <p v-else-if="!items.length" role="status" class="card p-6 text-center text-ink-soft">ما فيش رسائل جديدة.</p>
    <ul v-else class="grid gap-3">
      <li v-for="message in items" :key="message.id" class="card p-4">
        <div class="flex flex-wrap items-start justify-between gap-2">
          <p class="min-w-0 break-words font-bold">{{ message.name }}</p>
          <time class="text-sm text-ink-soft" :datetime="message.receivedAt">{{ when(message.receivedAt) }}</time>
        </div>
        <p class="mt-1 flex flex-wrap gap-x-4 text-sm">
          <a v-if="message.phoneNumber" :href="'tel:' + message.phoneNumber" dir="ltr"
            class="inline-flex min-h-11 items-center text-brand underline">{{ message.phoneNumber }}</a>
          <a v-if="message.email" :href="'mailto:' + message.email" dir="ltr"
            class="inline-flex min-h-11 items-center break-all text-brand underline">{{ message.email }}</a>
        </p>
        <p class="whitespace-pre-line break-words">{{ message.message }}</p>
        <div class="mt-3">
          <button type="button" class="btn-ghost !text-danger" :disabled="busy.includes(message.id)"
            :aria-label="'حذف رسالة ' + message.name" @click="remove(message)">حذف</button>
        </div>
      </li>
    </ul>
  </section>
</template>

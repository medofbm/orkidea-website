<script setup lang="ts">
// Review form (rendered inside a native <dialog> by the home page). No names are ever shown publicly.
type Status = 'idle' | 'sending' | 'success' | 'error'
const emit = defineEmits<{ done: [], cancel: [] }>()
const toast = useToast()
const form = reactive({ name: '', rating: 0, message: '' })
const errors = reactive<Record<'name' | 'rating' | 'message', string>>({ name: '', rating: '', message: '' })
const status = ref<Status>('idle')
const serverMessage = ref('')
const ratingLabels = ['ضعيف', 'مقبول', 'جيد', 'جيد جدًا', 'ممتاز']

function validate() {
  const name = form.name.trim()
  const message = form.message.trim()
  errors.name = name.length < 2 || name.length > 80 ? 'اكتبي اسمك (من حرفين إلى 80 حرفًا).' : ''
  errors.rating = form.rating < 1 ? 'اختاري تقييمك من 1 إلى 5 نجوم.' : ''
  errors.message = message.length < 10 || message.length > 500 ? 'اكتبي رأيك (من 10 إلى 500 حرف).' : ''
  return !Object.values(errors).some(Boolean)
}

async function submit() {
  if (status.value === 'sending') return
  serverMessage.value = ''
  if (!validate()) {
    await nextTick()
    document.querySelector<HTMLElement>('#review-form [aria-invalid="true"]')?.focus()
    return
  }
  status.value = 'sending'
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const result = await $fetch<{ message?: string }>('/api/reviews', {
      method: 'POST',
      headers: { RequestVerificationToken: session.requestToken },
      body: { name: form.name.trim(), rating: form.rating, message: form.message.trim() }
    })
    serverMessage.value = result.message ?? 'شكرًا! رأيك وصلنا وبيظهر بعد المراجعة.'
    status.value = 'success'
    toast.success(serverMessage.value)
  } catch (e: unknown) {
    const err = e as { statusCode?: number, status?: number, data?: { message?: string } }
    const code = err.statusCode ?? err.status
    serverMessage.value = code === 429
      ? 'جربتي أكثر من مرة، جربي بعد شوية.'
      : err.data?.message ?? 'تعذر إرسال رأيك. راجعي اتصال الإنترنت وحاولي مجددًا.'
    status.value = 'error'
  }
}
</script>

<template>
  <div class="text-start">
    <div v-if="status === 'success'" role="status" class="flex flex-col items-center gap-4 py-4 text-center">
      <span class="grid size-12 place-items-center rounded-full bg-success text-white"><AppIcon name="check" :size="24" /></span>
      <p class="font-bold text-ink">{{ serverMessage }}</p>
      <button type="button" class="btn-primary" autofocus @click="emit('done')">إغلاق</button>
    </div>

    <form v-else id="review-form" class="flex flex-col gap-4" novalidate @submit.prevent="submit">
      <fieldset class="m-0 border-0 p-0" :aria-describedby="errors.rating ? 'rf-rating-err' : undefined">
        <legend class="mb-1 text-sm font-bold text-ink">تقييمك</legend>
        <div class="flex gap-1" role="radiogroup" aria-label="التقييم من 5 نجوم">
          <label v-for="n in 5" :key="n" class="relative cursor-pointer rounded-full p-1 text-gold has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-brand">
            <input v-model.number="form.rating" type="radio" name="rating" :value="n" class="sr-only" :aria-invalid="!!errors.rating">
            <span class="sr-only">{{ n }} من 5 - {{ ratingLabels[n - 1] }}</span>
            <svg width="36" height="36" viewBox="0 0 24 24" :fill="n <= form.rating ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="1.5" aria-hidden="true">
              <path d="M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" stroke-linejoin="round" />
            </svg>
          </label>
        </div>
        <p v-if="errors.rating" id="rf-rating-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.rating }}</p>
      </fieldset>

      <div>
        <label for="rf-name" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="user" :size="18" />اسمك (لن يظهر للزوار)</label>
        <input id="rf-name" v-model="form.name" class="field" type="text" autocomplete="name" maxlength="80" required
          :aria-invalid="!!errors.name" :aria-describedby="errors.name ? 'rf-name-err' : undefined">
        <p v-if="errors.name" id="rf-name-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.name }}</p>
      </div>

      <div>
        <label for="rf-message" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="heart" :size="18" />رأيك</label>
        <textarea id="rf-message" v-model="form.message" class="field min-h-28 resize-y" rows="4" maxlength="500" required
          :aria-invalid="!!errors.message" :aria-describedby="errors.message ? 'rf-message-err' : undefined"></textarea>
        <p v-if="errors.message" id="rf-message-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.message }}</p>
      </div>

      <p v-if="status === 'error'" role="alert" class="rounded-[var(--radius-field)] bg-status-cancelled px-4 py-3 text-sm font-semibold text-danger">{{ serverMessage }}</p>

      <div class="flex flex-col gap-3 sm:flex-row-reverse">
        <button type="submit" class="btn-primary sm:flex-1" :disabled="status === 'sending'" :aria-busy="status === 'sending'">
          {{ status === 'sending' ? 'جاري الإرسال...' : 'أرسلي رأيك' }}
        </button>
        <button type="button" class="btn-secondary" @click="emit('cancel')">إلغاء</button>
      </div>
    </form>
  </div>
</template>

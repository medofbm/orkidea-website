<script setup lang="ts">
// Optional rating card shown under the receipt summary. Never blocks the receipt.
type Status = 'idle' | 'sending' | 'success' | 'closed'
const toast = useToast()
const status = ref<Status>('idle')
const rating = ref(0)
const message = ref('')
const allowName = ref(false)
const error = ref('')
const closedMessage = ref('')
const thanks = ref('شكرًا على تقييمك! 💜')
const ratingLabels = ['ضعيف', 'مقبول', 'جيد', 'جيد جدًا', 'ممتاز']

async function submit() {
  if (status.value === 'sending') return
  error.value = ''
  if (rating.value < 1) {
    error.value = 'اختاري تقييمك من 1 إلى 5 نجوم.'
    return
  }
  status.value = 'sending'
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const result = await $fetch<{ message?: string }>('/api/checkout/receipt/review', {
      method: 'POST',
      headers: { RequestVerificationToken: session.requestToken },
      body: { rating: rating.value, message: message.value.trim(), allowName: allowName.value }
    })
    thanks.value = result?.message ?? thanks.value
    status.value = 'success'
    toast.success(thanks.value)
  } catch (e: unknown) {
    const err = e as { statusCode?: number, status?: number, data?: { message?: string } }
    const code = err.statusCode ?? err.status
    if (code === 409) {
      closedMessage.value = err.data?.message ?? 'قيّمتي هذا الطلب قبل.'
      status.value = 'closed'
      return
    }
    error.value = code === 429
      ? 'جربتي أكثر من مرة، جربي بعد شوية.'
      : err.data?.message ?? 'تعذر إرسال تقييمك. حاولي مرة أخرى.'
    status.value = 'idle'
  }
}
</script>

<template>
  <section aria-labelledby="order-rating-title" class="card-glass mt-4 p-4 sm:p-6">
    <p v-if="status === 'success'" role="status" class="flex items-center justify-center gap-2 text-center font-bold text-brand-deep">
      <AppIcon name="check" :size="20" />{{ thanks }}
    </p>
    <p v-else-if="status === 'closed'" role="status" class="text-center font-semibold text-ink">{{ closedMessage }}</p>
    <form v-else class="flex flex-col gap-4" novalidate @submit.prevent="submit">
      <h2 id="order-rating-title" class="text-lg font-bold text-ink">شن رأيك في تجربتك؟</h2>
      <fieldset class="m-0 border-0 p-0" :aria-describedby="error && rating < 1 ? 'orc-error' : undefined">
        <legend class="sr-only">تقييمك</legend>
        <div class="flex gap-1" role="radiogroup" aria-label="التقييم من 5 نجوم">
          <label v-for="n in 5" :key="n" class="relative cursor-pointer rounded-full p-1 text-gold has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-brand">
            <input v-model.number="rating" type="radio" name="order-rating" :value="n" class="sr-only">
            <span class="sr-only">{{ n }} من 5 - {{ ratingLabels[n - 1] }}</span>
            <svg width="36" height="36" viewBox="0 0 24 24" :fill="n <= rating ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="1.5" aria-hidden="true">
              <path d="M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" stroke-linejoin="round" />
            </svg>
          </label>
        </div>
      </fieldset>
      <div>
        <label for="orc-message" class="mb-1 block text-sm font-bold text-ink">تعليقك (اختياري)</label>
        <textarea id="orc-message" v-model="message" class="field min-h-24 resize-y" rows="3" maxlength="500"></textarea>
      </div>
      <label class="flex min-h-11 items-center gap-2 text-sm font-semibold text-ink">
        <input v-model="allowName" type="checkbox" class="size-5 accent-brand">
        اسمحي بعرض اسمي الأول مع رأيي
      </label>
      <p v-if="error" id="orc-error" role="alert" class="rounded-[var(--radius-field)] bg-status-cancelled px-4 py-3 text-sm font-semibold text-danger">{{ error }}</p>
      <button type="submit" class="btn-primary sm:w-fit" :disabled="status === 'sending'" :aria-busy="status === 'sending'">
        {{ status === 'sending' ? 'جاري الإرسال...' : 'أرسلي تقييمك' }}
      </button>
    </form>
  </section>
</template>

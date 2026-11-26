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
const ratingEmoji = ['😕', '😐', '🙂', '😊', '🤩']
// Hovering star n previews 1..n; otherwise the chosen rating shows.
const hover = ref(0)
const shown = computed(() => hover.value || rating.value)

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
  <section aria-labelledby="order-rating-title" class="card-glass mt-4 overflow-hidden p-4 sm:p-6">
    <div v-if="status === 'success'" role="status" class="flex flex-col items-center gap-3 py-2 text-center">
      <div class="relative grid size-16 place-items-center">
        <span class="star-burst-ring absolute inset-0 rounded-full bg-gold/30" aria-hidden="true"></span>
        <span class="star-burst relative grid size-16 place-items-center rounded-full bg-gradient-brand text-white shadow-glow"><AppIcon name="check" :size="30" /></span>
      </div>
      <div class="flex gap-1" aria-hidden="true">
        <svg width="0" height="0" class="absolute" aria-hidden="true" focusable="false">
          <defs><linearGradient id="orc-star-done" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFE08A" /><stop offset="0.55" stop-color="#F2B93B" /><stop offset="1" stop-color="#C9871F" /></linearGradient></defs>
        </svg>
        <svg v-for="n in rating" :key="n" class="star-svg star-pop-in is-on !size-7" viewBox="0 0 24 24" :style="{ animationDelay: `${n * 90}ms` }">
          <path d="M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" stroke-linejoin="round" stroke-width="1.2" fill="url(#orc-star-done)" stroke="#B8872B" />
        </svg>
      </div>
      <p class="flex items-center justify-center gap-2 font-bold text-brand-deep">{{ thanks }}</p>
    </div>
    <p v-else-if="status === 'closed'" role="status" class="text-center font-semibold text-ink">{{ closedMessage }}</p>
    <form v-else class="flex flex-col gap-4" novalidate @submit.prevent="submit">
      <div class="flex items-center gap-3">
        <span class="grid size-11 shrink-0 place-items-center rounded-full bg-gradient-brand text-white shadow-soft" aria-hidden="true"><AppIcon name="heart" :size="22" /></span>
        <div>
          <h2 id="order-rating-title" class="text-lg font-bold leading-tight text-ink">شن رأيك في تجربتك؟</h2>
          <p class="text-sm text-ink-soft">رأيك يساعدنا نصير أحسن</p>
        </div>
      </div>
      <fieldset class="m-0 border-0 p-0" :aria-describedby="error && rating < 1 ? 'orc-error' : undefined">
        <legend class="sr-only">تقييمك</legend>
        <svg width="0" height="0" class="absolute" aria-hidden="true" focusable="false">
          <defs><linearGradient id="orc-star" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFE08A" /><stop offset="0.55" stop-color="#F2B93B" /><stop offset="1" stop-color="#C9871F" /></linearGradient></defs>
        </svg>
        <div class="flex gap-0.5" role="radiogroup" aria-label="التقييم من 5 نجوم" @mouseleave="hover = 0">
          <label v-for="n in 5" :key="n" class="star-label" @mouseenter="hover = n">
            <input v-model.number="rating" type="radio" name="order-rating" :value="n" class="sr-only">
            <span class="sr-only">{{ n }} من 5 - {{ ratingLabels[n - 1] }}</span>
            <svg class="star-svg" :class="{ 'is-on': n <= shown }" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" stroke-linejoin="round" stroke-width="1.2"
                :fill="n <= shown ? 'url(#orc-star)' : '#fff'" :stroke="n <= shown ? '#B8872B' : '#D9C7E0'" />
            </svg>
          </label>
        </div>
        <p class="star-word mt-1 flex items-center gap-2" aria-hidden="true">
          <template v-if="shown"><span class="text-xl">{{ ratingEmoji[shown - 1] }}</span>{{ ratingLabels[shown - 1] }}</template>
          <span v-else class="text-sm font-normal text-ink-soft">اضغطي على النجوم</span>
        </p>
        <p class="sr-only" aria-live="polite">{{ rating ? ratingLabels[rating - 1] : '' }}</p>
      </fieldset>
      <div>
        <label for="orc-message" class="mb-1 block text-sm font-bold text-ink">تعليقك (اختياري)</label>
        <textarea id="orc-message" v-model="message" class="field min-h-24 resize-y" rows="3" maxlength="500"></textarea>
      </div>
      <label class="check-row text-sm font-semibold text-ink">
        <input v-model="allowName" type="checkbox">
        اسمحي بعرض اسمي الأول مع رأيي
      </label>
      <p v-if="error" id="orc-error" role="alert" class="rounded-[var(--radius-field)] bg-status-cancelled px-4 py-3 text-sm font-semibold text-danger">{{ error }}</p>
      <button type="submit" class="btn-primary sm:w-fit" :disabled="status === 'sending'" :aria-busy="status === 'sending'">
        {{ status === 'sending' ? 'جاري الإرسال...' : 'أرسلي تقييمك' }}
      </button>
    </form>
  </section>
</template>

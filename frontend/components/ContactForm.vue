<script setup lang="ts">
// Contact form. Server owns validation; this mirrors the contract for fast feedback.
type Status = 'idle' | 'sending' | 'success' | 'error'
const toast = useToast()
const form = reactive({ name: '', phone: '', email: '', message: '' })
const errors = reactive<Record<'name' | 'contact' | 'email' | 'message', string>>({ name: '', contact: '', email: '', message: '' })
const status = ref<Status>('idle')
const serverMessage = ref('')
const nameRef = ref<HTMLInputElement | null>(null)

function validate() {
  const name = form.name.trim()
  const phone = form.phone.trim().replace(/[\s-]/g, '')
  const email = form.email.trim()
  const message = form.message.trim()
  errors.name = name.length < 2 || name.length > 80 ? 'اكتبي اسمك (من حرفين إلى 80 حرفًا).' : ''
  errors.contact = !phone && !email ? 'اكتبي رقم هاتفك أو بريدك الإلكتروني.' : ''
  errors.email = email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) ? 'البريد الإلكتروني غير صحيح.' : ''
  if (phone && !/^\+?[0-9]+$/.test(phone)) errors.contact = 'رقم الهاتف يحتوي على أرقام فقط (ويمكن أن يبدأ بـ +).'
  errors.message = message.length < 10 || message.length > 1000 ? 'اكتبي رسالتك (من 10 إلى 1000 حرف).' : ''
  return !Object.values(errors).some(Boolean)
}

const submission = useIdempotencyKey()
async function submit() {
  if (status.value === 'sending') return
  serverMessage.value = ''
  if (!validate()) {
    status.value = 'idle'
    await nextTick()
    document.querySelector<HTMLElement>('#contact-form [aria-invalid="true"]')?.focus()
    return
  }
  status.value = 'sending'
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const body = {
      name: form.name.trim(),
      phone: form.phone.trim().replace(/[\s-]/g, '') || undefined,
      email: form.email.trim() || undefined,
      message: form.message.trim()
    }
    const result = await $fetch<{ message?: string }>('/api/contact', {
      method: 'POST',
      headers: { RequestVerificationToken: session.requestToken, 'Idempotency-Key': submission.get(body) },
      body
    })
    submission.reset()
    serverMessage.value = result.message ?? 'وصلتنا رسالتك، بنرد عليك في أقرب وقت.'
    status.value = 'success'
    toast.success(serverMessage.value)
    Object.assign(form, { name: '', phone: '', email: '', message: '' })
  } catch (e: unknown) {
    const err = e as { statusCode?: number, status?: number, data?: { message?: string } }
    const code = err.statusCode ?? err.status
    serverMessage.value = code === 429
      ? 'جربتي أكثر من مرة، جربي بعد شوية.'
      : err.data?.message ?? 'تعذر إرسال الرسالة. راجعي اتصال الإنترنت وحاولي مجددًا.'
    status.value = 'error'
  }
}
function reset() {
  status.value = 'idle'
  serverMessage.value = ''
  nextTick(() => nameRef.value?.focus())
}
</script>

<template>
  <div class="text-start">
    <div v-if="status === 'success'" role="status" class="card flex flex-col items-center gap-3 p-6 text-center">
      <span class="grid size-12 place-items-center rounded-full bg-success text-white"><AppIcon name="check" :size="24" /></span>
      <p class="font-bold text-ink">{{ serverMessage }}</p>
      <button type="button" class="btn-secondary" @click="reset">إرسال رسالة أخرى</button>
    </div>

    <form v-else id="contact-form" class="card flex flex-col gap-4 p-5 sm:p-6" novalidate @submit.prevent="submit">
      <div>
        <label for="cf-name" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="user" :size="18" />اسمك</label>
        <input id="cf-name" ref="nameRef" v-model="form.name" class="field" type="text" autocomplete="name" maxlength="80" required
          :aria-invalid="!!errors.name" :aria-describedby="errors.name ? 'cf-name-err' : undefined">
        <p v-if="errors.name" id="cf-name-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.name }}</p>
      </div>

      <div>
        <label for="cf-phone" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="phone" :size="18" />رقم الهاتف</label>
        <input id="cf-phone" v-model="form.phone" class="field" type="tel" inputmode="tel" dir="ltr" autocomplete="tel" maxlength="20"
          :aria-invalid="!!errors.contact" :aria-describedby="errors.contact ? 'cf-contact-err' : 'cf-contact-hint'">
        <p v-if="errors.contact" id="cf-contact-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.contact }}</p>
        <p v-else id="cf-contact-hint" class="mt-1 text-xs text-ink-soft">اكتبي رقم الهاتف أو البريد الإلكتروني على الأقل.</p>
      </div>

      <div>
        <label for="cf-email" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="mail" :size="18" />البريد الإلكتروني (اختياري)</label>
        <input id="cf-email" v-model="form.email" class="field" type="email" dir="ltr" autocomplete="email" maxlength="120"
          :aria-invalid="!!errors.email" :aria-describedby="errors.email ? 'cf-email-err' : undefined">
        <p v-if="errors.email" id="cf-email-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.email }}</p>
      </div>

      <div>
        <label for="cf-message" class="mb-1 flex items-center gap-2 text-sm font-bold text-ink"><AppIcon name="sparkle" :size="18" />رسالتك</label>
        <textarea id="cf-message" v-model="form.message" class="field min-h-32 resize-y" rows="4" maxlength="1000" required
          :aria-invalid="!!errors.message" :aria-describedby="errors.message ? 'cf-message-err' : undefined"></textarea>
        <p v-if="errors.message" id="cf-message-err" role="alert" class="mt-1 text-sm font-semibold text-danger">{{ errors.message }}</p>
      </div>

      <p v-if="status === 'error'" role="alert" class="rounded-[var(--radius-field)] bg-status-cancelled px-4 py-3 text-sm font-semibold text-danger">{{ serverMessage }}</p>

      <button type="submit" class="btn-primary" :disabled="status === 'sending'" :aria-busy="status === 'sending'">
        {{ status === 'sending' ? 'جاري الإرسال...' : 'أرسلي رسالتك' }}
      </button>
    </form>
  </div>
</template>

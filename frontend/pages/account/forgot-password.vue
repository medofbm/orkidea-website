<script setup lang="ts">
const email = ref('')
const pending = ref(false)
const error = ref('')
const done = ref('')
const touched = ref(false)

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim()))
const showInvalid = computed(() => touched.value && !emailValid.value)

async function send() {
  if (pending.value) return
  touched.value = true
  error.value = ''
  if (!emailValid.value) return
  pending.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch<{ message?: string }>('/api/account/forgot-password', {
      method: 'POST',
      body: { email: email.value.trim() },
      retry: 0,
      headers: { RequestVerificationToken: session.requestToken }
    })
    done.value = response?.message || 'إذا كان البريد مسجّل عندنا، بيوصلك رابط لتغيير كلمة المرور خلال دقائق.'
  } catch (e) {
    const status = (e as { statusCode?: number, status?: number })?.statusCode ?? (e as { status?: number })?.status
    error.value = status === 429
      ? 'جربتي أكثر من مرة، جربي بعد شوية.'
      : status === 400
        ? 'اكتبي بريد إلكتروني صحيح.'
        : 'تعذر إرسال الطلب. حاولي مرة أخرى.'
  } finally {
    pending.value = false
  }
}

useSeoMeta({ title: 'نسيتي كلمة المرور؟ - أوركيديا', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section aria-labelledby="forgot-title">
  <div class="relative isolate overflow-hidden rounded-3xl bg-gradient-hero px-4 py-10 md:px-8 md:py-16">
    <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <span class="pointer-events-none absolute -bottom-20 -end-12 size-64 rounded-full bg-blush blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <div class="card-glass relative mx-auto max-w-md p-6 md:p-8 motion-safe:animate-fade-in-up">
      <div class="mb-6 text-center">
        <span class="mx-auto mb-3 inline-flex size-14 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="flower" :size="28" /></span>
        <h1 id="forgot-title" class="section-title">نسيتي كلمة المرور؟</h1>
        <p class="mt-1 text-ink-soft">اكتبي بريدك الإلكتروني وبنرسل لك رابط لتغيير كلمة المرور.</p>
      </div>
      <p v-if="error" role="alert" class="mb-4 rounded-xl bg-danger/10 p-3 text-danger">{{ error }}</p>
      <div v-if="done" role="status">
        <p class="flex items-start gap-2 rounded-xl bg-success/10 p-3 font-semibold text-success"><AppIcon name="check-circle" :size="20" class="mt-0.5 shrink-0" />{{ done }}</p>
      </div>
      <form v-else novalidate @submit.prevent="send">
        <fieldset :disabled="pending" class="space-y-4">
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="mail" :size="16" class="text-brand" />البريد الإلكتروني</span>
            <input v-model="email" required type="email" maxlength="254" dir="ltr" autocomplete="username" class="field"
              :aria-invalid="showInvalid" :aria-describedby="showInvalid ? 'forgot-email-error' : undefined" @blur="touched = true">
          </label>
          <p v-if="showInvalid" id="forgot-email-error" role="alert" class="text-sm text-danger">اكتبي بريد إلكتروني صحيح.</p>
          <button type="submit" class="btn-primary w-full">{{ pending ? 'جاري الإرسال…' : 'إرسال رابط التغيير' }}</button>
        </fieldset>
      </form>
      <NuxtLink to="/account" class="btn-secondary mt-5 w-full">العودة لتسجيل الدخول</NuxtLink>
    </div>
  </div>
  </section>
</template>

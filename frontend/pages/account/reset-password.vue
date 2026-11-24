<script setup lang="ts">
const route = useRoute()
const first = (v: unknown) => (Array.isArray(v) ? v[0] : v)
const email = String(first(route.query.email) ?? '')
const token = String(first(route.query.token) ?? '')
const linkOk = Boolean(email && token)

const fields = reactive({ password: '', confirmation: '' })
const show = ref(false)
const pending = ref(false)
const done = ref('')
const message = ref('')
const errors = ref<string[]>([])

const rules = usePasswordRules(() => fields.password)
const mismatch = computed(() => fields.confirmation.length > 0 && fields.password !== fields.confirmation)
const hasError = computed(() => Boolean(message.value) || errors.value.length > 0)

async function submit() {
  if (pending.value) return
  message.value = ''
  errors.value = []
  if (fields.password !== fields.confirmation) {
    message.value = 'كلمتا المرور غير متطابقتين.'
    return
  }
  pending.value = true
  try {
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch<{ message?: string }>('/api/account/reset-password', {
      method: 'POST',
      body: { email, token, password: fields.password, confirmPassword: fields.confirmation },
      retry: 0,
      headers: { RequestVerificationToken: session.requestToken }
    })
    done.value = response?.message || 'تم تغيير كلمة المرور. تقدري تسجلي دخولك الحين.'
  } catch (e) {
    const err = e as { statusCode?: number, status?: number, data?: { message?: string, errors?: string[] } }
    const status = err.statusCode ?? err.status
    if (status === 429) message.value = 'جربتي أكثر من مرة، جربي بعد شوية.'
    else if (status === 400) {
      message.value = err.data?.message || 'تعذر تغيير كلمة المرور. اطلبي رابط جديد وحاولي مرة أخرى.'
      errors.value = Array.isArray(err.data?.errors) ? err.data!.errors! : []
    } else message.value = 'تعذر تغيير كلمة المرور. حاولي مرة أخرى.'
  } finally {
    fields.password = ''
    fields.confirmation = ''
    pending.value = false
  }
}

useSeoMeta({ title: 'تغيير كلمة المرور - أوركيديا', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section aria-labelledby="reset-title">
  <div class="relative isolate overflow-hidden rounded-3xl bg-gradient-hero px-4 py-10 md:px-8 md:py-16">
    <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <span class="pointer-events-none absolute -bottom-20 -end-12 size-64 rounded-full bg-blush blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <div class="card-glass relative mx-auto max-w-md p-6 md:p-8 motion-safe:animate-fade-in-up">
      <div class="mb-6 text-center">
        <BrandMark class="mb-3" />
        <h1 id="reset-title" class="section-title">تغيير كلمة المرور</h1>
        <p v-if="linkOk && !done" class="mt-1 text-ink-soft">اختاري كلمة مرور جديدة لحسابك.</p>
      </div>

      <template v-if="!linkOk">
        <p role="alert" class="mb-4 rounded-xl bg-danger/10 p-3 text-danger">الرابط ناقص. اطلبي رابط جديد.</p>
        <NuxtLink to="/account/forgot-password" class="btn-primary w-full">طلب رابط جديد</NuxtLink>
      </template>

      <template v-else-if="done">
        <p role="status" class="flex items-start gap-2 rounded-xl bg-success/10 p-3 font-semibold text-success"><AppIcon name="check-circle" :size="20" class="mt-0.5 shrink-0" />{{ done }}</p>
        <NuxtLink to="/account" class="btn-primary mt-5 w-full">تسجيل الدخول</NuxtLink>
      </template>

      <template v-else>
        <div v-if="hasError" id="reset-error" role="alert" class="mb-4 rounded-xl bg-danger/10 p-3 text-danger">
          <p v-if="message">{{ message }}</p>
          <ul v-if="errors.length" class="mt-1 list-disc ps-5 text-sm">
            <li v-for="item in errors" :key="item">{{ item }}</li>
          </ul>
          <NuxtLink to="/account/forgot-password" class="mt-2 inline-block text-sm underline">طلب رابط جديد</NuxtLink>
        </div>
        <form @submit.prevent="submit">
          <fieldset :disabled="pending" class="space-y-4">
            <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور الجديدة</span>
              <input v-model="fields.password" required :type="show ? 'text' : 'password'" minlength="8" maxlength="100" dir="ltr"
                autocomplete="new-password" class="field" aria-describedby="reset-rules" :aria-invalid="hasError">
            </label>
            <ul id="reset-rules" class="space-y-1 text-sm">
              <li v-for="rule in rules" :key="rule.text" class="flex items-center gap-2" :class="rule.ok ? 'text-success' : 'text-ink-soft'">
                <AppIcon :name="rule.ok ? 'check-circle' : 'shield'" :size="14" />{{ rule.text }}
              </li>
            </ul>
            <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />تأكيد كلمة المرور</span>
              <input v-model="fields.confirmation" required :type="show ? 'text' : 'password'" maxlength="100" dir="ltr"
                autocomplete="new-password" class="field" :aria-invalid="mismatch" :aria-describedby="mismatch ? 'reset-mismatch' : undefined">
            </label>
            <p v-if="mismatch" id="reset-mismatch" role="alert" class="text-sm text-danger">كلمتا المرور غير متطابقتين.</p>
            <label class="flex min-h-11 items-center gap-2">
              <input v-model="show" type="checkbox"> إظهار كلمة المرور
            </label>
            <button type="submit" class="btn-primary w-full">{{ pending ? 'جاري الحفظ…' : 'حفظ كلمة المرور' }}</button>
          </fieldset>
        </form>
        <NuxtLink to="/account" class="btn-ghost mt-3 w-full">العودة لتسجيل الدخول</NuxtLink>
      </template>
    </div>
  </div>
  </section>
</template>

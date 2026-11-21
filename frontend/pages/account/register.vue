<script setup lang="ts">
const router = useRouter()
const fields = reactive({ email: '', password: '', confirmation: '' })
const pending = ref(false)
const error = ref('')

async function register() {
  if (pending.value) return
  if (fields.password !== fields.confirmation) {
    error.value = 'كلمتا المرور غير متطابقتين.'
    return
  }
  pending.value = true
  error.value = ''
  try {
    await postAccount('register', { ...fields })
    await router.push('/account')
  } catch {
    error.value = 'تعذر إنشاء الحساب. راجعي البريد ومتطلبات كلمة المرور أو جرّبي تسجيل الدخول.'
  } finally {
    fields.password = ''
    fields.confirmation = ''
    pending.value = false
  }
}
useSeoMeta({ title: 'إنشاء حساب أوركيديا', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section aria-labelledby="register-title">
  <div class="relative isolate overflow-hidden rounded-3xl bg-gradient-hero px-4 py-10 md:px-8 md:py-16">
    <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <span class="pointer-events-none absolute -bottom-20 -end-12 size-64 rounded-full bg-blush blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <div class="card-glass relative mx-auto max-w-md p-6 md:p-8 motion-safe:animate-fade-in-up">
      <div class="mb-6 text-center">
        <span class="mx-auto mb-3 inline-flex size-14 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="flower" :size="28" /></span>
        <h1 id="register-title" class="section-title">إنشاء حساب أوركيديا</h1>
        <p class="mt-1 text-ink-soft">انضمي لعائلة أوركيديا وتابعي طلباتك بخطوة واحدة.</p>
      </div>
    <p v-if="error" role="alert" class="mb-4 rounded-xl bg-danger/10 p-3 text-danger">{{ error }}</p>
    <form @submit.prevent="register">
      <fieldset :disabled="pending" class="space-y-4">
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="mail" :size="16" class="text-brand" />البريد الإلكتروني</span>
          <input v-model="fields.email" required type="email" maxlength="254" dir="ltr"
            autocomplete="username" class="field">
        </label>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور</span>
          <input v-model="fields.password" required type="password" minlength="8" maxlength="100" dir="ltr"
            aria-describedby="password-rules" autocomplete="new-password" class="field">
        </label>
        <p id="password-rules" class="text-sm text-ink-soft">ثمانية أحرف على الأقل، مع حرف إنجليزي كبير وصغير ورقم ورمز.</p>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />تأكيد كلمة المرور</span>
          <input v-model="fields.confirmation" required type="password" maxlength="100" dir="ltr"
            autocomplete="new-password" class="field">
        </label>
        <button type="submit" class="btn-primary w-full">إنشاء الحساب</button>
      </fieldset>
    </form>
    <NuxtLink to="/account" class="btn-secondary mt-5 w-full">عندي حساب</NuxtLink>
    </div>
  </div>
  </section>
</template>

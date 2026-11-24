<script setup lang="ts">
const router = useRouter()
const fields = reactive({ email: '', password: '', confirmation: '' })
const pending = ref(false)
const error = ref('')
const show = ref(false)
const rules = usePasswordRules(() => fields.password)
const mismatch = computed(() => fields.confirmation.length > 0 && fields.password !== fields.confirmation)

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
        <BrandMark class="mb-3" />
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
          <input v-model="fields.password" required :type="show ? 'text' : 'password'" minlength="8" maxlength="100" dir="ltr"
            aria-describedby="password-rules" autocomplete="new-password" class="field">
        </label>
        <ul id="password-rules" class="space-y-1 text-sm">
          <li v-for="rule in rules" :key="rule.text" class="flex items-center gap-2" :class="rule.ok ? 'text-success' : 'text-ink-soft'">
            <AppIcon :name="rule.ok ? 'check-circle' : 'shield'" :size="14" />{{ rule.text }}
          </li>
        </ul>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />تأكيد كلمة المرور</span>
          <input v-model="fields.confirmation" required :type="show ? 'text' : 'password'" maxlength="100" dir="ltr"
            autocomplete="new-password" class="field" :aria-invalid="mismatch" :aria-describedby="mismatch ? 'register-mismatch' : undefined">
        </label>
        <p v-if="mismatch" id="register-mismatch" role="alert" class="text-sm text-danger">كلمتا المرور غير متطابقتين.</p>
        <label class="flex min-h-11 items-center gap-2">
          <input v-model="show" type="checkbox"> إظهار كلمة المرور
        </label>
        <button type="submit" class="btn-primary w-full">{{ pending ? 'جاري إنشاء الحساب…' : 'إنشاء الحساب' }}</button>
      </fieldset>
    </form>
    <NuxtLink to="/account" class="btn-secondary mt-5 w-full">عندي حساب</NuxtLink>
    </div>
  </div>
  </section>
</template>

<script setup lang="ts">
interface AccountSession { signedIn: boolean; name: string | null; phoneNumber: string | null; isAdmin: boolean }
const account = ref<AccountSession | null>(null)
const loading = ref(true)
const pending = ref(false)
const verification = ref(false)
const error = ref('')
const fields = reactive({ email: '', password: '', rememberMe: false, code: '', recovery: false })

async function load() {
  loading.value = true
  error.value = ''
  try {
    account.value = await $fetch<AccountSession>('/api/account')
  } catch {
    account.value = null
    error.value = 'تعذر قراءة حالة الحساب.'
  } finally {
    loading.value = false
  }
}

async function submit(action: 'login' | 'verify' | 'logout') {
  if (pending.value) return
  pending.value = true
  error.value = ''
  const body = action === 'login'
    ? { email: fields.email, password: fields.password, rememberMe: fields.rememberMe }
    : action === 'verify'
      ? { code: fields.code, recovery: fields.recovery, rememberMe: fields.rememberMe }
      : undefined
  try {
    const response = await postAccount(action, body)
    verification.value = response?.requiresVerification === true
    if (!verification.value) await load()
  } catch {
    error.value = action === 'logout'
      ? 'تعذر تسجيل الخروج. حاولي مجددًا.'
      : 'تعذر تسجيل الدخول. راجعي البيانات وحاولي مرة أخرى.'
  } finally {
    fields.password = ''
    fields.code = ''
    pending.value = false
  }
}

onMounted(load)
useSeoMeta({ title: 'حساب أوركيديا', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section aria-labelledby="account-title">
  <div class="relative isolate overflow-hidden rounded-3xl bg-gradient-hero px-4 py-10 md:px-8 md:py-16">
    <span class="pointer-events-none absolute -top-16 -start-16 size-56 rounded-full bg-lilac/50 blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <span class="pointer-events-none absolute -bottom-20 -end-12 size-64 rounded-full bg-blush blur-2xl motion-safe:animate-float" aria-hidden="true" />
    <div class="card-glass relative mx-auto max-w-md p-6 md:p-8 motion-safe:animate-fade-in-up">
      <div class="mb-6 text-center">
        <span class="mx-auto mb-3 inline-flex size-14 items-center justify-center rounded-full bg-gradient-brand text-white shadow-(--shadow-glow)"><AppIcon name="flower" :size="28" /></span>
        <h1 id="account-title" class="section-title">حساب أوركيديا</h1>
        <p class="mt-1 text-ink-soft">أهلًا بكِ، سجّلي دخولك وتابعي طلباتك بسهولة.</p>
      </div>
    <p v-if="loading" role="status">جاري التحقق من الجلسة…</p>
    <p v-if="error" role="alert" class="mb-4 rounded-xl bg-danger/10 p-3 text-danger">{{ error }}</p>
    <template v-if="account?.signedIn">
      <p class="flex items-center gap-2 font-semibold text-success"><AppIcon name="check-circle" :size="20" />تم تسجيل الدخول</p>
      <p v-if="account.name" class="my-2 break-all" dir="ltr">{{ account.name }}</p>
      <button type="button" :disabled="pending" class="btn-secondary mt-4" @click="submit('logout')">تسجيل الخروج</button>
      <NuxtLink v-if="account.isAdmin" to="/admin" class="btn-ghost ms-2 mt-4 underline">لوحة الإدارة</NuxtLink>
      <AccountSettings :phone-number="account.phoneNumber" />
    </template>
    <form v-else-if="account && !loading" @submit.prevent="submit(verification ? 'verify' : 'login')">
      <fieldset :disabled="pending" class="space-y-4">
        <template v-if="verification">
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />{{ fields.recovery ? 'رمز الاسترداد' : 'رمز تطبيق المصادقة' }}</span>
            <input v-model="fields.code" required maxlength="64" dir="ltr" autocomplete="one-time-code"
              :inputmode="fields.recovery ? 'text' : 'numeric'" class="field">
          </label>
          <label class="flex min-h-11 items-center gap-2">
            <input v-model="fields.recovery" type="checkbox"> استخدام رمز الاسترداد
          </label>
        </template>
        <template v-else>
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="mail" :size="16" class="text-brand" />البريد الإلكتروني</span>
            <input v-model="fields.email" required type="email" maxlength="254" dir="ltr" autocomplete="username" class="field">
          </label>
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور</span>
            <input v-model="fields.password" required type="password" maxlength="1024" dir="ltr" autocomplete="current-password" class="field">
          </label>
          <label class="flex min-h-11 items-center gap-2">
            <input v-model="fields.rememberMe" type="checkbox"> تذكّريني على هذا الجهاز
          </label>
        </template>
        <button type="submit" class="btn-primary w-full">
          {{ verification ? 'تأكيد الرمز' : 'تسجيل الدخول' }}
        </button>
        <NuxtLink v-if="!verification" to="/account/forgot-password" class="btn-ghost w-full text-sm underline">نسيتي كلمة المرور؟</NuxtLink>
      </fieldset>
    </form>
    <button v-else-if="!loading" class="btn-secondary w-full" @click="load()">إعادة المحاولة</button>
    <NuxtLink v-if="account && !account.signedIn && !verification" to="/account/register"
      class="btn-secondary mt-5 w-full">ما عندك حساب؟ إنشاء حساب</NuxtLink>
    <NuxtLink to="/" class="btn-ghost mt-3 w-full">العودة للمتجر</NuxtLink>
    </div>
  </div>
  </section>
</template>

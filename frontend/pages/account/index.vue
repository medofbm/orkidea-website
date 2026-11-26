<script setup lang="ts">
const { account, refresh } = useAccount()
const loading = ref(true)
const pending = ref(false)
const verification = ref(false)
const showPassword = ref(false)
const error = ref('')
const fields = reactive({ email: '', password: '', rememberMe: false, code: '', recovery: false })

async function load() {
  loading.value = true
  error.value = ''
  try {
    await refresh()
  } catch {
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
        <BrandMark class="mb-3" />
        <h1 id="account-title" class="section-title">حساب أوركيديا</h1>
        <p class="mt-1 text-ink-soft">أهلًا بكِ، سجّلي دخولك وتابعي طلباتك بسهولة.</p>
      </div>
    <p v-if="loading" role="status">جاري التحقق من الجلسة…</p>
    <p v-if="error" role="alert" class="mb-4 flex items-start gap-2 rounded-xl border border-danger/25 bg-danger/10 p-3 text-sm font-semibold text-danger"><AppIcon name="x" :size="16" class="mt-1" />{{ error }}</p>
    <template v-if="account?.signedIn">
      <p class="flex items-center gap-2 font-semibold text-success"><AppIcon name="check-circle" :size="20" />تم تسجيل الدخول</p>
      <p v-if="account.name" class="my-2 break-all" dir="ltr">{{ account.name }}</p>
      <button type="button" :disabled="pending" class="btn-secondary mt-4" @click="submit('logout')">تسجيل الخروج</button>
      <NuxtLink v-if="account.isAdmin" to="/admin" class="btn-ghost ms-2 mt-4">لوحة الإدارة</NuxtLink>
      <AccountOrders />
      <AccountSettings :phone-number="account.phoneNumber" />
    </template>
    <form v-else-if="account && !loading" @submit.prevent="submit(verification ? 'verify' : 'login')">
      <fieldset :disabled="pending" class="space-y-4">
        <template v-if="verification">
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />{{ fields.recovery ? 'رمز الاسترداد' : 'رمز تطبيق المصادقة' }}</span>
            <input v-model="fields.code" required maxlength="64" dir="ltr" autocomplete="one-time-code"
              :inputmode="fields.recovery ? 'text' : 'numeric'" class="field">
          </label>
          <label class="check-row">
            <input v-model="fields.recovery" type="checkbox"> استخدام رمز الاسترداد
          </label>
        </template>
        <template v-else>
          <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="mail" :size="16" class="text-brand" />البريد الإلكتروني</span>
            <input v-model="fields.email" required type="email" maxlength="254" dir="ltr" autocomplete="username" class="field">
          </label>
          <div class="relative">
            <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور</span>
              <input v-model="fields.password" required :type="showPassword ? 'text' : 'password'" maxlength="1024" dir="ltr" autocomplete="current-password" class="field pl-12">
            </label>
            <!-- Outside the label so the field's accessible name stays «كلمة المرور». The input is LTR, so the toggle sits on its left. -->
            <button type="button" class="absolute bottom-[3px] left-0.5 grid h-11 w-11 place-items-center rounded-xl text-ink-soft hover:text-brand focus-visible:outline-2 focus-visible:outline-offset-[-4px] focus-visible:outline-brand"
              :aria-label="showPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'" :aria-pressed="showPassword" @click="showPassword = !showPassword">
              <AppIcon :name="showPassword ? 'eye-off' : 'eye'" :size="20" />
            </button>
          </div>
          <div class="flex flex-wrap items-center gap-x-6 text-sm text-ink">
            <label class="check-row">
              <input v-model="fields.rememberMe" type="checkbox"> تذكّريني على هذا الجهاز
            </label>
          </div>
        </template>
        <button type="submit" class="btn-primary w-full">
          {{ pending ? 'جاري التحقق…' : verification ? 'تأكيد الرمز' : 'تسجيل الدخول' }}
        </button>
        <NuxtLink v-if="!verification" to="/account/forgot-password" class="btn-ghost w-full text-sm">نسيتي كلمة المرور؟</NuxtLink>
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

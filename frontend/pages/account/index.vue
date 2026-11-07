<script setup lang="ts">
interface AccountSession { signedIn: boolean; name: string | null; isAdmin: boolean }
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
    const session = await $fetch<{ requestToken: string }>('/api/session')
    const response = await $fetch<{ requiresVerification: boolean } | undefined>('/api/account/' + action, {
      method: 'POST', body, retry: 0, headers: { RequestVerificationToken: session.requestToken }
    })
    fields.password = ''
    fields.code = ''
    verification.value = response?.requiresVerification === true
    if (!verification.value) await load()
  } catch {
    error.value = action === 'logout'
      ? 'تعذر تسجيل الخروج. حاولي مجددًا.'
      : 'تعذر تسجيل الدخول. راجعي البيانات وحاولي مرة أخرى.'
  } finally {
    pending.value = false
  }
}

onMounted(load)
useSeoMeta({ title: 'حساب أوركيديا', robots: 'noindex, nofollow, noarchive' })
</script>

<template>
  <section class="mx-auto max-w-md rounded-xl bg-white p-5 md:p-8" aria-labelledby="account-title">
    <h1 id="account-title" class="mb-5 text-2xl font-bold">حساب أوركيديا</h1>
    <p v-if="loading" role="status">جاري التحقق من الجلسة…</p>
    <p v-if="error" role="alert" class="mb-4 rounded-lg bg-red-50 p-3 text-red-800">{{ error }}</p>
    <template v-if="account?.signedIn">
      <p>تم تسجيل الدخول</p>
      <p v-if="account.name" class="my-2 break-all" dir="ltr">{{ account.name }}</p>
      <button type="button" :disabled="pending" class="mt-4 min-h-11 rounded-lg border px-4" @click="submit('logout')">تسجيل الخروج</button>
    </template>
    <form v-else-if="account && !loading" @submit.prevent="submit(verification ? 'verify' : 'login')">
      <fieldset :disabled="pending" class="space-y-4">
        <template v-if="verification">
          <label class="block">{{ fields.recovery ? 'رمز الاسترداد' : 'رمز تطبيق المصادقة' }}
            <input v-model="fields.code" required maxlength="64" dir="ltr" autocomplete="one-time-code"
              :inputmode="fields.recovery ? 'text' : 'numeric'" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
          </label>
          <label class="flex min-h-11 items-center gap-2">
            <input v-model="fields.recovery" type="checkbox"> استخدام رمز الاسترداد
          </label>
        </template>
        <template v-else>
          <label class="block">البريد الإلكتروني
            <input v-model="fields.email" required type="email" maxlength="254" dir="ltr" autocomplete="username" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
          </label>
          <label class="block">كلمة المرور
            <input v-model="fields.password" required type="password" maxlength="1024" dir="ltr" autocomplete="current-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
          </label>
          <label class="flex min-h-11 items-center gap-2">
            <input v-model="fields.rememberMe" type="checkbox"> تذكّر تسجيل الدخول
          </label>
        </template>
        <button type="submit" class="min-h-12 w-full rounded-xl bg-purple-800 px-4 font-semibold text-white disabled:opacity-50">
          {{ verification ? 'تأكيد الرمز' : 'تسجيل الدخول' }}
        </button>
      </fieldset>
    </form>
    <button v-else-if="!loading" class="min-h-11 underline" @click="load()">إعادة المحاولة</button>
    <NuxtLink to="/" class="mt-5 inline-block min-h-11 text-purple-900 underline">العودة للمتجر</NuxtLink>
  </section>
</template>

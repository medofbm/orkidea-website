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
  <section class="mx-auto max-w-md rounded-xl bg-white p-5 md:p-8" aria-labelledby="register-title">
    <h1 id="register-title" class="mb-5 text-2xl font-bold">إنشاء حساب أوركيديا</h1>
    <p v-if="error" role="alert" class="mb-4 rounded-lg bg-red-50 p-3 text-red-800">{{ error }}</p>
    <form @submit.prevent="register">
      <fieldset :disabled="pending" class="space-y-4">
        <label class="block">البريد الإلكتروني
          <input v-model="fields.email" required type="email" maxlength="254" dir="ltr"
            autocomplete="username" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <label class="block">كلمة المرور
          <input v-model="fields.password" required type="password" minlength="8" maxlength="100" dir="ltr"
            aria-describedby="password-rules" autocomplete="new-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <p id="password-rules" class="text-sm">ثمانية أحرف على الأقل، مع حرف إنجليزي كبير وصغير ورقم ورمز.</p>
        <label class="block">تأكيد كلمة المرور
          <input v-model="fields.confirmation" required type="password" maxlength="100" dir="ltr"
            autocomplete="new-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <button type="submit" class="min-h-12 w-full rounded-xl bg-purple-800 px-4 font-semibold text-white">إنشاء الحساب</button>
      </fieldset>
    </form>
    <NuxtLink to="/account" class="mt-5 inline-block min-h-11 text-purple-900 underline">عندي حساب</NuxtLink>
  </section>
</template>

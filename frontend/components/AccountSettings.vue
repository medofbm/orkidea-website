<script setup lang="ts">
const props = defineProps<{ phoneNumber: string | null }>()
const phone = ref(props.phoneNumber ?? '')
const passwords = reactive({ currentPassword: '', newPassword: '', confirmation: '' })
const pending = ref(false)
const error = ref('')
const message = ref('')

async function save(action: 'phone' | 'password') {
  if (pending.value) return
  pending.value = true
  error.value = ''
  message.value = ''
  try {
    await postAccount(action, action === 'phone' ? { phoneNumber: phone.value.trim() || null } : { ...passwords })
    message.value = action === 'phone' ? 'تم تحديث رقم الهاتف.' : 'تم تغيير كلمة المرور.'
  } catch {
    error.value = action === 'phone'
      ? 'تعذر حفظ رقم الهاتف. راجعي الرقم أو أعيدي تحميل الصفحة.'
      : 'تعذر تغيير كلمة المرور. راجعي كلمة المرور الحالية ومتطلبات الجديدة.'
  } finally {
    passwords.currentPassword = ''
    passwords.newPassword = ''
    passwords.confirmation = ''
    pending.value = false
  }
}
</script>

<template>
  <section class="mt-6 space-y-5" aria-label="إعدادات الحساب">
    <p v-if="error" role="alert" class="rounded-lg bg-red-50 p-3 text-red-800">{{ error }}</p>
    <p v-if="message" role="status" class="rounded-lg bg-green-50 p-3 text-green-800">{{ message }}</p>
    <form class="space-y-3 border-t pt-4" @submit.prevent="save('phone')">
      <label class="block">رقم الهاتف
        <input v-model="phone" :disabled="pending" type="tel" maxlength="32" dir="ltr" autocomplete="tel"
          class="mt-1 block min-h-11 w-full rounded-lg border px-3">
      </label>
      <button type="submit" :disabled="pending" class="min-h-11 rounded-lg border px-4">حفظ رقم الهاتف</button>
    </form>
    <form class="border-t pt-4" @submit.prevent="save('password')">
      <fieldset :disabled="pending" class="space-y-3">
        <legend class="font-semibold">تغيير كلمة المرور</legend>
        <label class="block">كلمة المرور الحالية
          <input v-model="passwords.currentPassword" required type="password" maxlength="1024" dir="ltr"
            autocomplete="current-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <label class="block">كلمة المرور الجديدة
          <input v-model="passwords.newPassword" required type="password" minlength="8" maxlength="100" dir="ltr"
            autocomplete="new-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <label class="block">تأكيد كلمة المرور الجديدة
          <input v-model="passwords.confirmation" required type="password" maxlength="100" dir="ltr"
            autocomplete="new-password" class="mt-1 block min-h-11 w-full rounded-lg border px-3">
        </label>
        <button type="submit" class="min-h-11 rounded-lg border px-4">تغيير كلمة المرور</button>
      </fieldset>
    </form>
  </section>
</template>

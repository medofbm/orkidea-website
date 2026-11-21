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
  <section class="mt-6 space-y-4" aria-label="إعدادات الحساب">
    <p v-if="error" role="alert" class="rounded-xl bg-danger/10 p-3 text-danger">{{ error }}</p>
    <p v-if="message" role="status" class="rounded-xl bg-success/10 p-3 text-success">{{ message }}</p>
    <form class="card space-y-3 p-4" @submit.prevent="save('phone')">
      <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="phone" :size="16" class="text-brand" />رقم الهاتف</span>
        <input v-model="phone" :disabled="pending" type="tel" maxlength="32" dir="ltr" autocomplete="tel"
          class="field">
      </label>
      <button type="submit" :disabled="pending" class="btn-secondary">حفظ رقم الهاتف</button>
    </form>
    <form class="card p-4" @submit.prevent="save('password')">
      <fieldset :disabled="pending" class="space-y-3">
        <legend class="mb-2 font-bold text-brand">تغيير كلمة المرور</legend>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور الحالية</span>
          <input v-model="passwords.currentPassword" required type="password" maxlength="1024" dir="ltr"
            autocomplete="current-password" class="field">
        </label>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />كلمة المرور الجديدة</span>
          <input v-model="passwords.newPassword" required type="password" minlength="8" maxlength="100" dir="ltr"
            autocomplete="new-password" class="field">
        </label>
        <label class="block"><span class="mb-1.5 flex items-center gap-2 text-sm font-semibold text-ink"><AppIcon name="shield" :size="16" class="text-brand" />تأكيد كلمة المرور الجديدة</span>
          <input v-model="passwords.confirmation" required type="password" maxlength="100" dir="ltr"
            autocomplete="new-password" class="field">
        </label>
        <button type="submit" class="btn-secondary">تغيير كلمة المرور</button>
      </fieldset>
    </form>
  </section>
</template>

<script setup lang="ts">
// Optional card under the receipt: a guest leaves an email and gets a set-password link. The server finds the order
// from the session receipt and always answers the same way, so this card never learns whether the email was new.
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
    const response = await $fetch<{ message?: string }>('/api/checkout/receipt/account', {
      method: 'POST',
      body: { email: email.value.trim() },
      retry: 0,
      headers: { RequestVerificationToken: session.requestToken }
    })
    done.value = response?.message || 'لو الإيميل صحيح، بيوصلك رابط خلال دقايق.'
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
</script>

<template>
  <section aria-labelledby="post-order-account-title" class="card-glass mt-4 p-4 sm:p-6">
    <p v-if="done" role="status" class="flex items-start gap-2 rounded-xl bg-success/10 p-3 font-semibold text-success">
      <AppIcon name="check-circle" :size="20" class="mt-0.5 shrink-0" />{{ done }}
    </p>
    <form v-else novalidate @submit.prevent="send">
      <h2 id="post-order-account-title" class="text-lg font-bold text-ink">💜 تبي تتابعي طلبك وتلقي طلباتك الجاية في مكان واحد؟</h2>
      <p class="mt-1 text-sm text-ink-soft">اكتبي إيميلك ونبعتولك رابط تحطي بيه كلمة السر.</p>
      <p v-if="error" role="alert" class="mt-3 rounded-xl bg-danger/10 p-3 text-danger">{{ error }}</p>
      <fieldset :disabled="pending" class="mt-3 flex flex-col gap-3 sm:flex-row sm:items-start">
        <div class="min-w-0 flex-1">
          <label class="sr-only" for="post-order-account-email">البريد الإلكتروني</label>
          <input id="post-order-account-email" v-model="email" required type="email" maxlength="254" dir="ltr" autocomplete="email"
            class="field w-full" :aria-invalid="showInvalid" :aria-describedby="showInvalid ? 'post-order-account-error' : undefined" @blur="touched = true">
          <p v-if="showInvalid" id="post-order-account-error" role="alert" class="mt-1 text-sm text-danger">اكتبي بريد إلكتروني صحيح.</p>
        </div>
        <button type="submit" class="btn-primary">{{ pending ? 'جاري الإرسال…' : 'ابعتيلي الرابط' }}</button>
      </fieldset>
    </form>
  </section>
</template>

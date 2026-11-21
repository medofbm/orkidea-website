<script setup lang="ts">
import { resolveConfirm } from '~/composables/useConfirm'
import type { ConfirmOptions } from '~/composables/useConfirm'

const state = useState<ConfirmOptions | null>('app-confirm', () => null)
const dialog = ref<HTMLDialogElement | null>(null)
const cancelBtn = ref<HTMLButtonElement | null>(null)
const confirmBtn = ref<HTMLButtonElement | null>(null)
let previouslyFocused: HTMLElement | null = null

const danger = computed(() => state.value?.tone === 'danger')

watch(state, async (opts) => {
  const el = dialog.value
  if (!el) return
  if (opts) {
    previouslyFocused = document.activeElement as HTMLElement | null
    await nextTick()
    if (!el.open) el.showModal()
    ;(danger.value ? cancelBtn.value : confirmBtn.value)?.focus()
  } else if (el.open) {
    el.close()
  }
})

function finish(ok: boolean) {
  state.value = null
  resolveConfirm(ok)
  const target = previouslyFocused
  previouslyFocused = null
  nextTick(() => target?.focus?.())
}

function onCancel(e: Event) {
  e.preventDefault() // Escape: we close it ourselves
  finish(false)
}
function onDialogClick(e: MouseEvent) {
  if (e.target === dialog.value) finish(false) // backdrop click
}
</script>

<template>
  <dialog
    ref="dialog"
    class="confirm-dialog"
    aria-labelledby="app-confirm-title"
    :aria-describedby="state?.message ? 'app-confirm-message' : undefined"
    @cancel="onCancel"
    @click="onDialogClick"
  >
    <div v-if="state" class="card-glass confirm-card p-6">
      <h2 id="app-confirm-title" class="text-lg font-extrabold text-ink">{{ state.title }}</h2>
      <p v-if="state.message" id="app-confirm-message" class="mt-2 text-sm leading-7 text-ink-soft">{{ state.message }}</p>
      <div class="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
        <button ref="cancelBtn" type="button" class="btn-secondary" @click="finish(false)">
          {{ state.cancelLabel ?? 'إلغاء' }}
        </button>
        <button
          v-if="danger"
          ref="confirmBtn"
          type="button"
          class="inline-flex min-h-12 min-w-11 cursor-pointer items-center justify-center rounded-full bg-danger px-6 font-bold text-white hover:brightness-110 active:scale-[0.98]"
          @click="finish(true)"
        >{{ state.confirmLabel ?? 'تأكيد' }}</button>
        <button v-else ref="confirmBtn" type="button" class="btn-primary" @click="finish(true)">
          {{ state.confirmLabel ?? 'تأكيد' }}
        </button>
      </div>
    </div>
  </dialog>
</template>

<style scoped>
.confirm-dialog {
  margin: auto; padding: 0; border: 0; background: transparent; color: inherit;
  width: min(28rem, calc(100vw - 2rem)); max-height: calc(100dvh - 2rem); overflow: visible;
}
.confirm-dialog::backdrop { background: rgb(42 21 48 / 0.5); -webkit-backdrop-filter: blur(4px); backdrop-filter: blur(4px); }
.confirm-card { background: rgb(255 255 255 / 0.94); }

@media (prefers-reduced-motion: no-preference) {
  .confirm-dialog[open] .confirm-card { animation: confirm-pop 200ms ease-out both; }
  .confirm-dialog[open]::backdrop { animation: confirm-fade 200ms ease-out both; }
}
@keyframes confirm-pop { from { opacity: 0; transform: scale(0.94); } to { opacity: 1; transform: scale(1); } }
@keyframes confirm-fade { from { opacity: 0; } to { opacity: 1; } }
</style>

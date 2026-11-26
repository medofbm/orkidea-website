/**
 * Replaces the browser's native validation bubble («Please fill out this field.») with an inline Arabic message
 * under the field. Forms with `novalidate` and their own errors never fire `invalid`, so they are untouched.
 */
type Field = HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement

const owners = new WeakMap<HTMLElement, Field>()
const messages = new WeakMap<Field, HTMLElement>()
let counter = 0
let focusedThisSubmit = false

function text(el: Field): string {
  const v = el.validity
  if (v.valueMissing) return 'هذا الحقل مطلوب'
  if (v.typeMismatch) {
    if (el.getAttribute('type') === 'email') return 'اكتبي بريد إلكتروني صحيح'
    if (el.getAttribute('type') === 'url') return 'اكتبي رابطًا صحيحًا'
    return 'القيمة غير صحيحة'
  }
  if (v.patternMismatch) return el.getAttribute('title') || 'الصيغة غير صحيحة'
  if (v.tooShort) return `اكتبي ${(el as HTMLInputElement).minLength} أحرف على الأقل`
  if (v.tooLong) return `الحد الأقصى ${(el as HTMLInputElement).maxLength} حرفًا`
  if (v.rangeUnderflow) return `القيمة يجب ألا تقل عن ${(el as HTMLInputElement).min}`
  if (v.rangeOverflow) return `القيمة يجب ألا تزيد عن ${(el as HTMLInputElement).max}`
  if (v.stepMismatch || v.badInput) return 'القيمة غير صحيحة'
  return el.validationMessage
}

// The message goes right after the field, or after the wrapper when the field is absolutely positioned,
// or after the label when the field is a checkbox/radio wrapped by it.
function anchorOf(el: Field): HTMLElement {
  const type = el.getAttribute('type')
  if ((type === 'checkbox' || type === 'radio') && el.closest('label')) return el.closest('label') as HTMLElement
  if (getComputedStyle(el).position === 'absolute' && el.parentElement) return el.parentElement
  return el
}

function sweep() {
  document.querySelectorAll<HTMLElement>('.field-error').forEach((node) => {
    const owner = owners.get(node)
    if (!owner || !owner.isConnected) node.remove()
  })
}

function clear(el: Field) {
  const node = messages.get(el)
  if (!node) return
  const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(id => id && id !== node.id)
  if (ids.length) el.setAttribute('aria-describedby', ids.join(' '))
  else el.removeAttribute('aria-describedby')
  el.removeAttribute('aria-invalid')
  node.remove()
  messages.delete(el)
}

function show(el: Field) {
  let node = messages.get(el)
  if (!node || !node.isConnected) {
    node = document.createElement('span')
    node.id = `field-error-${++counter}`
    node.className = 'field-error'
    node.setAttribute('role', 'alert')
    node.setAttribute('aria-live', 'assertive')
    owners.set(node, el)
    messages.set(el, node)
    anchorOf(el).insertAdjacentElement('afterend', node)
    const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean)
    el.setAttribute('aria-describedby', [...ids, node.id].join(' '))
  }
  node.textContent = text(el)
  el.setAttribute('aria-invalid', 'true')
}

function onInvalid(event: Event) {
  const el = event.target as Field
  if (!el || !('validity' in el)) return
  // A component that handles its own invalid state (hidden proxy input) keeps focus control.
  if (el.getAttribute('aria-hidden') === 'true') {
    focusedThisSubmit = true
    setTimeout(() => { focusedThisSubmit = false }, 0)
    return
  }
  event.preventDefault()
  sweep()
  show(el)
  if (!focusedThisSubmit) {
    focusedThisSubmit = true
    el.focus()
    setTimeout(() => { focusedThisSubmit = false }, 0)
  }
}

function onEdit(event: Event) {
  const el = event.target as Field
  if (!el || !messages.has(el)) return
  if (el.validity.valid) clear(el)
  else show(el)
}

export default defineNuxtPlugin(() => {
  const w = window as Window & { __formValidation?: boolean }
  if (w.__formValidation) return
  w.__formValidation = true
  document.addEventListener('invalid', onInvalid, true)
  document.addEventListener('input', onEdit, true)
  document.addEventListener('change', onEdit, true)
  document.addEventListener('reset', () => setTimeout(sweep, 0), true)
})

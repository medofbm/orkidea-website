import type { Directive } from 'vue'

type RevealEl = HTMLElement & { __revealObserver?: IntersectionObserver }

/**
 * v-reveal: fades/slides an element in when it scrolls into view.
 * v-reveal="120" adds a 120 ms delay (for staggering lists); v-reveal.zoom scales in instead of sliding.
 * Server HTML stays visible. Elements already on screen at mount, or with reduced motion, are left as they are.
 */
const reveal: Directive<RevealEl, number | undefined> = {
  getSSRProps: () => ({}),
  mounted(el, binding) {
    if (typeof IntersectionObserver === 'undefined') return
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const rect = el.getBoundingClientRect()
    if (rect.top < window.innerHeight && rect.bottom > 0) return
    const delay = Number(binding.value) || 0
    if (delay > 0) el.style.transitionDelay = `${delay}ms`
    el.classList.add('reveal')
    if (binding.modifiers.zoom) el.classList.add('reveal-zoom')
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some(entry => entry.isIntersecting)) return
      el.classList.add('is-visible')
      observer.disconnect()
      // Drop the stagger delay once shown, so later hover transitions are not delayed.
      el.addEventListener('transitionend', () => { el.style.transitionDelay = '' }, { once: true })
    }, { threshold: 0.12 })
    observer.observe(el)
    el.__revealObserver = observer
  },
  unmounted(el) {
    el.__revealObserver?.disconnect()
  }
}

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('reveal', reveal)
})

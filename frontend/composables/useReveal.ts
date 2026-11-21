import { onBeforeUnmount, onMounted, type Ref } from 'vue'

interface RevealOptions {
  /** Selector for children inside the root. If omitted, the root element itself is revealed. */
  selector?: string
  /** Delay in ms added per child (stagger). Default 0. */
  stagger?: number
  /** Visible fraction that triggers the reveal. Default 0.15. */
  threshold?: number
}

/**
 * Adds the `is-visible` class (see .reveal in main.css) once an element enters the viewport.
 * Elements need the `reveal` class. SSR-safe: runs only on the client after mount.
 */
export function useReveal(target: Ref<HTMLElement | null | undefined>, options: RevealOptions = {}) {
  let observer: IntersectionObserver | null = null

  onMounted(() => {
    const root = target.value
    if (!root) return
    const items: HTMLElement[] = options.selector
      ? Array.from(root.querySelectorAll<HTMLElement>(options.selector))
      : [root]
    if (!items.length) return

    if (typeof IntersectionObserver === 'undefined') {
      items.forEach((el) => el.classList.add('is-visible'))
      return
    }

    const stagger = options.stagger ?? 0
    items.forEach((el, i) => {
      if (stagger > 0) el.style.transitionDelay = `${i * stagger}ms`
    })

    const io = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue
        entry.target.classList.add('is-visible')
        io.unobserve(entry.target)
      }
    }, { threshold: options.threshold ?? 0.15 })
    observer = io
    items.forEach((el) => io.observe(el))
  })

  onBeforeUnmount(() => {
    observer?.disconnect()
    observer = null
  })
}

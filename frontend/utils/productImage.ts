const LOCAL_PRODUCT_IMAGE = /^(\/images\/products\/[A-Za-z0-9_-]+)\.(png|jpe?g)$/

/**
 * Responsive srcset for locally uploaded product images (WebP variants made by the backend).
 * Returns undefined for brand images and external URLs, so they keep their own src.
 *
 * Usage:
 *   <img :src="url" :srcset="productImageSrcset(url)" sizes="(min-width: 1024px) 25vw, 50vw">
 *
 * The original `src` stays as the fallback if a variant is missing.
 */
export function productImageSrcset(url: string | null | undefined): string | undefined {
  const match = url ? LOCAL_PRODUCT_IMAGE.exec(url) : null
  if (!match) return undefined
  const base = match[1]
  return `${base}-400.webp 400w, ${base}-800.webp 800w, ${base}-1200.webp 1200w`
}

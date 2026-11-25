// Security headers for everything the Nuxt server sends (pages, API proxy, images).
// Set here so they deploy with the app; nginx repeats the static ones for non-Nuxt responses.
// The app loads only same-origin scripts, styles, fonts (self-hosted @fontsource) and images,
// so the policy needs no external hosts. Nuxt SSR injects inline scripts (payload), hence 'unsafe-inline'.
const csp = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  "connect-src 'self'",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'"
].join('; ')

export default defineEventHandler(event => {
  setResponseHeaders(event, {
    'X-Content-Type-Options': 'nosniff',
    'X-Frame-Options': 'DENY',
    'Referrer-Policy': 'strict-origin-when-cross-origin',
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=()'
  })
  // The dev server needs inline eval and websockets for HMR, so the CSP is enforced in production builds only.
  if (!import.meta.dev) setResponseHeader(event, 'Content-Security-Policy', csp)
  if (getRequestProtocol(event, { xForwardedProto: true }) === 'https') {
    setResponseHeader(event, 'Strict-Transport-Security', 'max-age=15552000; includeSubDomains')
  }
})

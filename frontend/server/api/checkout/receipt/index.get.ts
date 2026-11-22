// GET /api/checkout/receipt. It must live here because the receipt/ folder (review.post.ts)
// takes precedence over the [path].ts catch-all for this segment.
export default defineEventHandler(event => {
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/checkout/receipt')
})

// Receipt page review. The backend finds the order from the session receipt; no order id is accepted here.
export default defineEventHandler(event => {
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/checkout/receipt/review')
})

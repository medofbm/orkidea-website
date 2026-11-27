// "Create an account after ordering". The backend takes the order from the session receipt; only an email is sent.
export default defineEventHandler(event => {
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/checkout/receipt/account')
})

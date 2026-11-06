export default defineEventHandler(event => {
  setResponseHeader(event, 'Cache-Control', 'no-store')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/cart')
})

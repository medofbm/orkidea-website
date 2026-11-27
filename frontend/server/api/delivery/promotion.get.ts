export default defineEventHandler(event => {
  setResponseHeader(event, 'Cache-Control', 'public, max-age=60')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/delivery/promotion')
})

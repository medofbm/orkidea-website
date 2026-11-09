export default defineEventHandler(event => {
  if (event.method !== 'GET') throw createError({ statusCode: 405 })
  setResponseHeader(event, 'Cache-Control', 'no-store')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/session')
})

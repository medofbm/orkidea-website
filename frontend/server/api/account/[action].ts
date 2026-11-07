export default defineEventHandler(event => {
  const action = getRouterParam(event, 'action')
  if (event.method !== 'POST' || !action || !['login', 'verify', 'logout'].includes(action)) {
    throw createError({ statusCode: 404 })
  }
  setResponseHeader(event, 'Cache-Control', 'no-store')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/account/' + action)
})

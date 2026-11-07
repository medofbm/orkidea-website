export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  if (event.method !== 'GET' || !new RegExp('^(dashboard|orders(?:/[1-9][0-9]*)?)$').test(path)) {
    throw createError({ statusCode: 404 })
  }
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/admin/' + path + getRequestURL(event).search)
})

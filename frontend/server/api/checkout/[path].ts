export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path')
  const allowed = (event.method === 'GET' && path === 'receipt') ||
    (event.method === 'POST' && (path === 'shipping' || path === 'orders'))
  if (!allowed) throw createError({ statusCode: 404 })
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/checkout/' + path)
})

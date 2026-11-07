export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  const method = event.method
  const allowed = (path === '' && method === 'GET') ||
    (path === 'items' && method === 'POST') ||
    (new RegExp('^items/[1-9][0-9]*$').test(path) && ['PATCH', 'DELETE'].includes(method))
  if (!allowed) throw createError({ statusCode: 404 })
  setResponseHeader(event, 'Cache-Control', 'no-store')
  const suffix = path ? '/' + path : ''
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/cart' + suffix)
})

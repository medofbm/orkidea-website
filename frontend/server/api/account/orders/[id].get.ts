export default defineEventHandler(event => {
  const id = getRouterParam(event, 'id') ?? ''
  if (!/^[1-9]\d{0,9}$/.test(id)) throw createError({ statusCode: 404 })
  setResponseHeader(event, 'Cache-Control', 'no-store')
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/account/orders/' + id)
})

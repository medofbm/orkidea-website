export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  if (!new RegExp('^[a-zA-Z0-9/_-]+[.](?:png|jpe?g|webp)$', 'i').test(path)) {
    throw createError({ statusCode: 404, statusMessage: 'Not found' })
  }
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/images/' + path)
})

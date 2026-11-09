export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  if (event.method !== 'GET' || !new RegExp('^products(?:/[1-9][0-9]*)?$').test(path)) {
    throw createError({ statusCode: 404, statusMessage: 'Not found' })
  }
  const origin = useRuntimeConfig(event).backendOrigin
  const query = getRequestURL(event).search
  return proxyRequest(event, origin + '/api/v1/catalogue/' + path + query)
})

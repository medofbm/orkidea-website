export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  const read = event.method === 'GET' && new RegExp('^(dashboard|delivery|categories|reviews|contact-messages|sales-chart|orders/(?:export|manual)|products(?:/(?:categories|[1-9][0-9]*))?|(?:orders|inventory)(?:/[1-9][0-9]*)?)$').test(path)
  const write = event.method === 'POST' && new RegExp('^(categories(?:/[1-9][0-9]*(?:/delete)?)?|reviews/[1-9][0-9]*/(?:visibility|delete)|contact-messages/[1-9][0-9]*/delete|orders/(?:[1-9][0-9]*/(?:status|stock|delivery)|manual/(?:review|confirm))|inventory/[1-9][0-9]*/(open|configure|adjust)|products(?:/[1-9][0-9]*(?:/delete|/variants(?:/[1-9][0-9]*(?:/delete)?)?)?)?)$').test(path)
  if (!read && !write) {
    throw createError({ statusCode: 404 })
  }
  setResponseHeader(event, 'Cache-Control', 'no-store')
  setResponseHeader(event, 'X-Robots-Tag', 'noindex, nofollow, noarchive')
  const productUpload = write && new RegExp('^products(?:/[1-9][0-9]*)?$').test(path)
  return proxyRequest(event, useRuntimeConfig(event).backendOrigin + '/api/v1/admin/' + path + getRequestURL(event).search, {
    streamRequest: productUpload
  })
})

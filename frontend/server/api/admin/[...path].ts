export default defineEventHandler(event => {
  const path = getRouterParam(event, 'path') ?? ''
  const read = event.method === 'GET' && new RegExp('^(dashboard|delivery|shipping-rates|promotions|categories|reviews|contact-messages|sales-chart|admins|users(?:/[A-Za-z0-9-]{1,64})?|orders/(?:export|manual)|products(?:/(?:categories|[1-9][0-9]*))?|(?:orders|inventory)(?:/[1-9][0-9]*)?)$').test(path)
  const write = event.method === 'POST' && new RegExp('^(shipping-rates/import-sabil|promotions|admins(?:/[A-Za-z0-9-]{1,64}/remove)?|categories(?:/[1-9][0-9]*(?:/(?:delete|archive))?)?|reviews/[1-9][0-9]*/(?:visibility|delete)|contact-messages/[1-9][0-9]*/(?:delete|read)|orders/(?:[1-9][0-9]*/(?:status|stock|delivery)|manual/(?:review|confirm))|inventory/[1-9][0-9]*/(open|configure|adjust|unit)|products(?:/[1-9][0-9]*(?:/(?:delete|archive)|/variants(?:/[1-9][0-9]*(?:/(?:delete|archive))?)?)?)?)$').test(path)
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

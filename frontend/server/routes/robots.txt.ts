export default defineEventHandler(event => {
  const config = useRuntimeConfig(event).public
  // Never derive absolute URLs from request headers (Host / X-Forwarded-Host can be spoofed).
  const origin = String(config.siteUrl).replace(/\/+$/, '')
  const preview = config.preview
  setResponseHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  setResponseHeader(event, 'Cache-Control', 'public, max-age=3600')
  // Preview builds stay out of search engines (the pages also send noindex).
  if (preview) return 'User-agent: *\nDisallow: /\n'
  return [
    'User-agent: *',
    'Allow: /',
    'Disallow: /admin',
    'Disallow: /account',
    'Disallow: /cart',
    'Disallow: /checkout',
    'Disallow: /api',
    '',
    'Sitemap: ' + origin + '/sitemap.xml',
    ''
  ].join('\n')
})

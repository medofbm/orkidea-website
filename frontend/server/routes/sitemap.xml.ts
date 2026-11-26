interface SitemapPage { items?: { id: number }[]; total?: number }

const escapeXml = (value: string) =>
  value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;')

export default defineEventHandler(async event => {
  const config = useRuntimeConfig(event).public
  // Never derive absolute URLs from request headers (Host / X-Forwarded-Host can be spoofed).
  const origin = String(config.siteUrl).replace(/\/+$/, '')
  const paths = ['/', '/about', '/order-and-delivery', '/privacy']
  let seen = 0
  try {
    for (let page = 1; page <= 50; page++) {
      // Through Nuxt's own catalogue proxy, which forwards X-Forwarded-Proto; a direct backend call is redirected to HTTPS.
      const data = await event.$fetch<SitemapPage>('/api/v1/catalogue/products', { query: { page } })
      const items = data.items ?? []
      for (const item of items) paths.push('/products/' + item.id)
      seen += items.length
      if (!items.length || seen >= (data.total ?? 0)) break
    }
  } catch {
    // Backend unavailable: still serve the static pages rather than failing the sitemap.
  }
  const body = '<?xml version="1.0" encoding="UTF-8"?>\n' +
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
    paths.map(path => '  <url><loc>' + escapeXml(origin + path) + '</loc></url>').join('\n') +
    '\n</urlset>\n'
  setResponseHeader(event, 'Content-Type', 'application/xml; charset=utf-8')
  setResponseHeader(event, 'Cache-Control', 'public, max-age=3600')
  return body
})

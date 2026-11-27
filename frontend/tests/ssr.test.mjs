import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'

test('built storefront renders catalogue data and the product gallery on the server', { timeout: 30000 }, async () => {
  const product = {
    id: 1, name: 'زيت اختبار الواجهة', description: 'وصف اصطناعي للاختبار',
    category: 'زيوت', images: ['/images/front.png', '/images/back.png'],
    variants: [{ id: 1, size: '100 مل', price: '25.00', currency: 'LYD' }]
  }
  const backend = createServer((req, res) => {
    res.setHeader('Content-Type', 'application/json')
    if (req.url === '/api/v1/session') {
      res.setHeader('Set-Cookie', 'qa-session=synthetic; HttpOnly; Path=/; SameSite=Strict')
      return res.end(JSON.stringify({ requestToken: 'synthetic-token' }))
    }
    if (req.url === '/api/v1/cart/items' && req.method === 'POST') {
      const valid = req.headers.requestverificationtoken === 'synthetic-token' &&
        req.headers.cookie === 'qa-session=synthetic'
      res.statusCode = valid ? 200 : 400
      return res.end(JSON.stringify({ success: valid }))
    }
    if (req.url === '/api/v1/admin/promotions') {
      res.statusCode = req.headers.cookie === 'qa-session=synthetic' &&
        (req.method === 'GET' || req.headers.requestverificationtoken === 'synthetic-token') ? 200 : 401
      return res.end(JSON.stringify({ method: req.method, promotion: null }))
    }
    if (req.url === '/api/v1/delivery/promotion' && req.method === 'GET') {
      return res.end(JSON.stringify({ promotion: { title: 'عرض الجمعة البيضاء', scope: 'Tripoli',
        endsAtUtc: '2026-11-30T22:00:00Z', minSubtotal: null } }))
    }
    const accountsPath = req.url?.match(/^\/api\/v1\/admin\/(users|admins)(\/[^?]*)?(\?.*)?$/)
    if (accountsPath) {
      res.statusCode = req.headers.cookie === 'qa-session=synthetic' ? 200 : 401
      return res.end(JSON.stringify({ method: req.method, path: accountsPath[1] + (accountsPath[2] ?? '') }))
    }
    const orderWrite = req.url?.match(/^\/api\/v1\/admin\/orders\/1\/(status|stock|delivery)$/)?.[1]
    if (orderWrite && req.method === 'POST') {
      const chunks = []
      req.on('data', chunk => chunks.push(chunk))
      req.on('end', () => {
        const input = JSON.parse(Buffer.concat(chunks).toString())
        const expected = { status: ['status', 'Processing'], stock: ['operation', 'Reserve'],
          delivery: ['action', 'HandOver'] }[orderWrite]
        const valid = input[expected[0]] === expected[1] && input.expectedRevision === 0 &&
          req.headers.requestverificationtoken === 'synthetic-token' &&
          req.headers.cookie === 'qa-session=synthetic'
        res.statusCode = valid ? 409 : 400
        res.end(JSON.stringify({ success: false }))
      })
      return
    }
    if (req.url?.startsWith('/api/v1/admin/delivery?') && req.method === 'GET') {
      const query = new URL(req.url, 'http://127.0.0.1').searchParams
      const authorized = req.headers.cookie === 'qa-session=synthetic'
      res.statusCode = authorized ? 200 : 401
      return res.end(JSON.stringify({ state: query.get('state'), search: query.get('search') }))
    }
    if (req.url === '/api/v1/admin/orders/export?status=pending&search=%231&source=FacebookManual' && req.method === 'GET') {
      if (req.headers.cookie !== 'qa-session=synthetic') {
        res.statusCode = 401
        return res.end('{}')
      }
      res.setHeader('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
      res.setHeader('Content-Disposition', 'attachment; filename="qa-orders.xlsx"')
      return res.end(Buffer.from([0x50, 0x4b, 0x03, 0x04]))
    }
    const manualPath = req.url?.match(/^\/api\/v1\/admin\/orders\/manual(?:\/(review|confirm))?$/)
    if (manualPath) {
      if (req.headers.cookie !== 'qa-session=synthetic') {
        res.statusCode = 401
        return res.end('{}')
      }
      if (!manualPath[1] && req.method === 'GET') return res.end(JSON.stringify({ variants: [], rates: [] }))
      const chunks = []
      req.on('data', chunk => chunks.push(chunk))
      req.on('end', () => {
        const payload = JSON.parse(Buffer.concat(chunks).toString())
        const input = manualPath[1] === 'review' ? payload : payload.input
        const valid = req.method === 'POST' && req.headers.requestverificationtoken === 'synthetic-token' &&
          input?.lines?.[0]?.quantity === 2 && (manualPath[1] === 'review' || payload.reviewToken === 'qa-review-token')
        res.statusCode = valid ? 200 : 400
        res.end(JSON.stringify(manualPath[1] === 'review' ? { reviewToken: 'qa-review-token' } : { orderId: 1 }))
      })
      return
    }
    if (req.url === '/api/v1/admin/inventory/1/adjust' && req.method === 'POST') {
      const authorized = req.headers.cookie === 'qa-session=synthetic' &&
        req.headers.requestverificationtoken === 'synthetic-token'
      res.statusCode = authorized ? 409 : 401
      return res.end('{}')
    }
    if (req.url === '/api/v1/admin/products' && req.method === 'POST') {
      const chunks = []
      req.on('data', chunk => chunks.push(chunk))
      req.on('end', () => {
        const payload = Buffer.concat(chunks).toString()
        const valid = req.headers.cookie === 'qa-session=synthetic' &&
          req.headers.requestverificationtoken === 'synthetic-token' &&
          req.headers['content-type']?.startsWith('multipart/form-data; boundary=') &&
          payload.includes('name="galleryFiles"; filename="synthetic.png"') && payload.includes('synthetic image bytes')
        res.statusCode = valid ? 200 : 400
        res.end(JSON.stringify({ success: valid }))
      })
      return
    }
    if (req.url === '/api/v1/admin/categories/1' && req.method === 'POST') {
      const authorized = req.headers.cookie === 'qa-session=synthetic' &&
        req.headers.requestverificationtoken === 'synthetic-token'
      res.statusCode = authorized ? 409 : 401
      return res.end('{}')
    }
    if (req.url === '/api/v1/catalogue/products/1') return res.end(JSON.stringify(product))
    if (req.url === '/api/v1/catalogue/products/2') {
      return res.end(JSON.stringify({ ...product, id: 2, images: ['/images/logo.jpg'], variants: [] }))
    }
    if (req.url?.startsWith('/api/v1/catalogue/products?')) {
      const query = new URL(req.url, 'http://localhost').searchParams
      const items = query.has('categoryId') && query.get('categoryId') !== '1' ? [] : [product]
      const page = Number(query.get('page') ?? '1')
      const total = query.get('categoryId') === '1' && page === 2 ? 49 : items.length
      return res.end(JSON.stringify({ items, page, pageSize: 24, total,
        categories: [{ id: 1, name: product.category }] }))
    }
    res.statusCode = 404
    res.end('{}')
  })
  await new Promise(resolve => backend.listen(0, '127.0.0.1', resolve))
  const probe = createServer()
  await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve))
  const port = probe.address().port
  await new Promise(resolve => probe.close(resolve))
  const origin = 'http://127.0.0.1:' + port
  const child = spawn(process.execPath, ['.output/server/index.mjs'], {
    env: { ...process.env, HOST: '127.0.0.1', PORT: String(port),
      NUXT_BACKEND_ORIGIN: 'http://127.0.0.1:' + backend.address().port },
    stdio: 'ignore'
  })
  try {
    let ready = false
    for (let attempt = 0; attempt < 100; attempt++) {
      try { await fetch(origin); ready = true; break } catch { await delay(100) }
    }
    assert.ok(ready, 'built frontend starts')
    const home = await fetch(origin)
    const csp = home.headers.get('content-security-policy') ?? ''
    assert.ok(csp.includes("default-src 'self'") && csp.includes("frame-ancestors 'none'") && csp.includes("object-src 'none'"),
      'home page enforces a Content-Security-Policy')
    assert.ok(!csp.includes('unsafe-eval'), 'CSP does not allow eval')
    assert.equal(home.headers.get('x-content-type-options'), 'nosniff')
    assert.equal(home.headers.get('x-frame-options'), 'DENY')
    assert.equal(home.headers.get('referrer-policy'), 'strict-origin-when-cross-origin')
    assert.equal(home.headers.get('strict-transport-security'), null, 'no HSTS over plain http')
    const httpsHome = await fetch(origin, { headers: { 'X-Forwarded-Proto': 'https' } })
    assert.match(httpsHome.headers.get('strict-transport-security') ?? '', /max-age=15552000/, 'HSTS when forwarded as https')
    const robots = await (await fetch(origin + '/robots.txt', { headers: { 'X-Forwarded-Host': 'evil.example' } })).text()
    assert.ok(!robots.includes('evil.example'), 'robots.txt ignores X-Forwarded-Host')
    const listing = await home.text()
    assert.ok(listing.includes(product.name), 'catalogue is present in initial HTML')
    const filtered = await (await fetch(origin + '/?categoryId=1')).text()
    assert.ok(filtered.includes(product.name), 'matching category retains its product')
    assert.match(filtered, /<a(?=[^>]*aria-current="page")[^>]*href="\/#catalogue-heading"/, 'active category card is announced and clears the filter when clicked')
    const paginated = await (await fetch(origin + '/?page=2&categoryId=1')).text()
    assert.match(paginated, /صفحة <bdi[^>]*>2<\/bdi> من <bdi[^>]*>3<\/bdi>/, 'current page and page count are announced')
    assert.match(paginated, /href="\/?\?page=1&(?:amp;)?categoryId=1#catalogue-heading"[^>]*>السابق/, 'previous page keeps the selected category')
    assert.match(paginated, /href="\/?\?page=3&(?:amp;)?categoryId=1#catalogue-heading"[^>]*>التالي/, 'next page keeps the selected category')
    const emptyCategory = await (await fetch(origin + '/?categoryId=2')).text()
    assert.ok(!emptyCategory.includes(product.name), 'category filter is forwarded to the backend')
    assert.ok(emptyCategory.includes('لا توجد منتجات ضمن الفئة المختارة حاليًا.'), 'empty category explains the active filter')
    assert.match(emptyCategory, /href="\/#catalogue-heading"[^>]*>\s*عرض كل المنتجات/, 'empty category offers a link that clears the filter')
    const about = await (await fetch(origin + '/about')).text()
    assert.ok(about.includes('عن أوركيديا') && about.includes('تسوقي الآن'), 'about page renders story and call to action')
    assert.ok(!listing.includes('id="footer-about"'), 'footer no longer carries the about block')
    const detail = await (await fetch(origin + '/products/1')).text()
    assert.ok(detail.includes(product.description), 'detail renders backend data')
    assert.ok(detail.includes('25.00'), 'decimal price remains exact')
    assert.ok(detail.includes('/images/back.png'), 'gallery includes additional photo')
    assert.ok(detail.includes('100 مل'), 'variant is rendered')
    assert.ok(!detail.includes('عرض السلة'), 'product page has no extra view-cart link')
    const unavailable = await (await fetch(origin + '/products/2')).text()
    assert.ok(unavailable.includes('صورة غير متاحة'), 'brand-only cover uses the gallery placeholder')
    assert.ok(unavailable.includes('المنتج غير متاح للطلب حاليًا.'), 'missing variants explain why ordering is unavailable')
    assert.ok(!unavailable.includes('أضيفي للسلة'), 'product without variants cannot be added to cart')
    const missing = await fetch(origin + '/api/v1/catalogue/products/999')
    assert.equal(missing.status, 404)
    const session = await fetch(origin + '/api/session')
    assert.ok(session.headers.get('set-cookie')?.includes('qa-session=synthetic'),
      'session proxy forwards the HttpOnly cookie')
    assert.ok(session.headers.get('cache-control')?.includes('no-store'))
    const cartWrite = await fetch(origin + '/api/cart/items', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: 'synthetic-token',
        Cookie: 'qa-session=synthetic' },
      body: JSON.stringify({ variantId: 1, quantity: 1 })
    })
    assert.equal(cartWrite.status, 200, 'cart proxy forwards the session and antiforgery header')
    assert.ok((await cartWrite.json()).success)
    const adminWrites = { status: { status: 'Processing' }, stock: { operation: 'Reserve' },
      delivery: { action: 'HandOver' } }
    for (const [path, payload] of Object.entries(adminWrites)) {
      const adminWrite = await fetch(origin + '/api/admin/orders/1/' + path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: 'synthetic-token',
          Cookie: 'qa-session=synthetic' },
        body: JSON.stringify({ ...payload, expectedRevision: 0 })
      })
      assert.equal(adminWrite.status, 409, path + ' forwards authenticated payload and preserves conflicts')
      assert.ok(adminWrite.headers.get('cache-control')?.includes('no-store'))
    }
    const accountCalls = [['GET', '/api/admin/users?search=qa&page=2&role=admins', 'users'],
      ['GET', '/api/admin/users/abc-123', 'users/abc-123'], ['GET', '/api/admin/admins', 'admins'],
      ['POST', '/api/admin/admins', 'admins'], ['POST', '/api/admin/admins/abc-123/remove', 'admins/abc-123/remove']]
    for (const [method, path, forwarded] of accountCalls) {
      const response = await fetch(origin + path, { method, headers: { Cookie: 'qa-session=synthetic' } })
      assert.equal(response.status, 200, method + ' ' + path + ' is forwarded')
      assert.deepEqual(await response.json(), { method, path: forwarded })
    }
    for (const [method, path] of [['POST', '/api/admin/users'], ['POST', '/api/admin/users/abc-123'],
      ['POST', '/api/admin/admins/abc-123/delete'], ['GET', '/api/admin/admins/abc-123'], ['GET', '/api/admin/users/a.b']]) {
      assert.equal((await fetch(origin + path, { method })).status, 404, method + ' ' + path + ' is not proxied')
    }
    for (const method of ['GET', 'POST']) {
      const promotions = await fetch(origin + '/api/admin/promotions', { method,
        headers: { Cookie: 'qa-session=synthetic', RequestVerificationToken: 'synthetic-token' } })
      assert.equal(promotions.status, 200, method + ' promotions is forwarded with session and antiforgery')
      assert.ok(promotions.headers.get('cache-control')?.includes('no-store'))
    }
    for (const path of ['/api/admin/promotions/1', '/api/admin/promotions/delete']) {
      assert.equal((await fetch(origin + path, { method: 'POST' })).status, 404, 'POST ' + path + ' is not proxied')
    }
    const promotion = await fetch(origin + '/api/delivery/promotion')
    assert.equal(promotion.status, 200)
    assert.equal((await promotion.json()).promotion.scope, 'Tripoli', 'public promotion proxy returns the server promotion')
    const promotionsPage = await (await fetch(origin + '/admin/promotions')).text()
    assert.ok(promotionsPage.includes('العروض') && promotionsPage.includes('/admin/promotions'), 'admin sidebar lists the promotions page under settings')
    const adminsPage = await (await fetch(origin + '/admin/admins')).text()
    assert.ok(adminsPage.includes('الحسابات') && adminsPage.includes('/admin/users'), 'admin sidebar lists the accounts pages')
    const queuePath = '/api/admin/delivery?state=Returned&search=%231'
    const queuePage = await (await fetch(origin + '/admin/delivery')).text()
    assert.ok(queuePage.includes('التوصيل والرواجع'))
    const deniedQueue = await fetch(origin + queuePath)
    assert.equal(deniedQueue.status, 401)
    const queue = await fetch(origin + queuePath, { headers: { Cookie: 'qa-session=synthetic' } })
    assert.equal(queue.status, 200)
    assert.ok(queue.headers.get('cache-control')?.includes('no-store'))
    assert.deepEqual(await queue.json(), { state: 'Returned', search: '#1' })
    assert.equal((await fetch(origin + '/api/admin/delivery', { method: 'POST' })).status, 404)
    const exportUrl = origin + '/api/admin/orders/export?status=pending&search=%231&source=FacebookManual'
    assert.equal((await fetch(exportUrl)).status, 401)
    assert.equal((await fetch(exportUrl, { method: 'POST' })).status, 404)
    const exported = await fetch(exportUrl, { headers: { Cookie: 'qa-session=synthetic' } })
    assert.equal(exported.status, 200)
    assert.equal(exported.headers.get('content-type'), 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
    assert.match(exported.headers.get('content-disposition'), /attachment; filename="qa-orders.xlsx"/)
    assert.match(exported.headers.get('cache-control'), /no-store/)
    assert.deepEqual(new Uint8Array(await exported.arrayBuffer()), new Uint8Array([0x50, 0x4b, 0x03, 0x04]))
    const manualUrl = origin + '/api/admin/orders/manual'
    assert.equal((await fetch(manualUrl)).status, 401)
    assert.equal((await fetch(manualUrl + '/confirm')).status, 404)
    const manualPage = await (await fetch(origin + '/admin/orders/manual')).text()
    assert.ok(manualPage.includes('تسجيل طلب من فيسبوك'))
    const options = await fetch(manualUrl, { headers: { Cookie: 'qa-session=synthetic' } })
    assert.deepEqual(await options.json(), { variants: [], rates: [] })
    assert.match(options.headers.get('cache-control'), /no-store/)
    const manualHeaders = { Cookie: 'qa-session=synthetic', RequestVerificationToken: 'synthetic-token', 'Content-Type': 'application/json' }
    const manualInput = { lines: [{ variantId: 1, quantity: 2 }] }
    const manualReview = await fetch(manualUrl + '/review', {
      method: 'POST', headers: manualHeaders, body: JSON.stringify(manualInput)
    })
    assert.equal(manualReview.status, 200)
    const manualConfirm = await fetch(manualUrl + '/confirm', {
      method: 'POST', headers: manualHeaders, body: JSON.stringify({ input: manualInput, ...await manualReview.json() })
    })
    assert.equal(manualConfirm.status, 200)
    assert.deepEqual(await manualConfirm.json(), { orderId: 1 })
    const stockPage = await (await fetch(origin + '/admin/inventory')).text()
    assert.ok(stockPage.includes('المخزون والتعبئة'))
    const adjust = await fetch(origin + '/api/admin/inventory/1/adjust', {
      method: 'POST', headers: { Cookie: 'qa-session=synthetic', RequestVerificationToken: 'synthetic-token' }
    })
    assert.equal(adjust.status, 409)
    assert.ok(adjust.headers.get('cache-control')?.includes('no-store'))
    assert.equal((await fetch(origin + '/api/admin/inventory/1/delete', { method: 'POST' })).status, 404)
    const adminProductsPage = await (await fetch(origin + '/admin/products')).text()
    assert.ok(adminProductsPage.includes('إدارة المنتجات'))
    const multipart = new FormData()
    multipart.append('galleryFiles', new Blob(['synthetic image bytes'], { type: 'image/png' }), 'synthetic.png')
    const productUpload = await fetch(origin + '/api/admin/products', {
      method: 'POST', body: multipart,
      headers: { Cookie: 'qa-session=synthetic', RequestVerificationToken: 'synthetic-token' }
    })
    assert.equal(productUpload.status, 200, 'product proxy preserves multipart boundary, file and credentials')
    assert.ok(productUpload.headers.get('cache-control')?.includes('no-store'))
    assert.equal((await fetch(origin + '/api/admin/products/1/delete', { method: 'POST' })).status, 404)
    const adminCategoriesPage = await (await fetch(origin + '/admin/categories')).text()
    assert.ok(adminCategoriesPage.includes('إدارة الفئات'))
    const rename = await fetch(origin + '/api/admin/categories/1', {
      method: 'POST',
      headers: { Cookie: 'qa-session=synthetic', RequestVerificationToken: 'synthetic-token', 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'New', expectedName: 'Old' })
    })
    assert.equal(rename.status, 409)
    assert.ok(rename.headers.get('cache-control')?.includes('no-store'))
    assert.equal((await fetch(origin + '/api/admin/categories/1/delete', { method: 'POST' })).status, 404)
    const unsupportedAdminWrite = await fetch(origin + '/api/admin/orders/1', { method: 'POST' })
    assert.equal(unsupportedAdminWrite.status, 404)
    const write = await fetch(origin + '/api/v1/catalogue/products', { method: 'POST' })
    assert.equal(write.status, 404, 'catalogue proxy accepts reads only')
  } finally {
    child.kill()
    await new Promise(resolve => backend.close(resolve))
  }
})

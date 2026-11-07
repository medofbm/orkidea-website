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
    if (req.url === '/api/v1/catalogue/products/1') return res.end(JSON.stringify(product))
    if (req.url?.startsWith('/api/v1/catalogue/products?')) {
      return res.end(JSON.stringify({ items: [product], page: 1, pageSize: 24, total: 1 }))
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
    const listing = await (await fetch(origin)).text()
    assert.ok(listing.includes(product.name), 'catalogue is present in initial HTML')
    const detail = await (await fetch(origin + '/products/1')).text()
    assert.ok(detail.includes(product.description), 'detail renders backend data')
    assert.ok(detail.includes('25.00'), 'decimal price remains exact')
    assert.ok(detail.includes('/images/back.png'), 'gallery includes additional photo')
    assert.ok(detail.includes('100 مل'), 'variant is rendered')
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
    const unsupportedAdminWrite = await fetch(origin + '/api/admin/orders/1', { method: 'POST' })
    assert.equal(unsupportedAdminWrite.status, 404)
    const write = await fetch(origin + '/api/v1/catalogue/products', { method: 'POST' })
    assert.equal(write.status, 404, 'catalogue proxy accepts reads only')
  } finally {
    child.kill()
    await new Promise(resolve => backend.close(resolve))
  }
})

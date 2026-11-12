import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { request } from 'node:http'
import { createServer } from 'node:https'
import { test } from 'node:test'
import { chromium } from 'playwright'

test('mobile browser uses Vue and ASP.NET through one HTTPS origin', { timeout: 120000 }, async () => {
  const email = process.env.ORKIDEA_BROWSER_ADMIN_EMAIL
  const password = process.env.ORKIDEA_BROWSER_ADMIN_PASSWORD
  const certificate = process.env.ORKIDEA_BROWSER_CERT
  const key = process.env.ORKIDEA_BROWSER_KEY
  assert.ok(email && password && certificate && key, 'isolated browser fixtures are required')

  // Exercise the forwarded HTTPS request and cookie path on loopback.
  const gateway = createServer({ cert: readFileSync(certificate), key: readFileSync(key) }, (incoming, outgoing) => {
    const upstream = request({
      hostname: '127.0.0.1',
      port: 3081,
      method: incoming.method,
      path: incoming.url,
      headers: { ...incoming.headers, 'x-forwarded-proto': 'https', 'x-forwarded-for': '127.0.0.1' }
    }, response => {
      outgoing.writeHead(response.statusCode, response.headers)
      response.pipe(outgoing)
    })
    upstream.on('error', () => { outgoing.writeHead(502).end() })
    incoming.pipe(upstream)
  })
  await new Promise((resolve, reject) => {
    gateway.once('error', reject)
    gateway.listen(0, '127.0.0.1', resolve)
  })
  const origin = 'https://127.0.0.1:' + gateway.address().port
  let browser
  try {
    browser = await chromium.launch()
    const mobile = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, ignoreHTTPSErrors: true }
    const admin = await browser.newContext(mobile)
    const page = await admin.newPage()
    page.setDefaultTimeout(15000)
    const anonymous = await admin.request.get(origin + '/api/admin/categories')
    assert.equal(anonymous.status(), 401)
    await page.goto(origin + '/account')
    await page.getByLabel('البريد الإلكتروني').fill(email)
    await page.getByLabel('كلمة المرور').fill(password)
    await page.getByRole('button', { name: 'تسجيل الدخول', exact: true }).click()
    await page.getByRole('link', { name: 'لوحة الإدارة' }).waitFor()
    assert.ok((await admin.cookies()).some(cookie => cookie.name.startsWith('.AspNetCore.Identity.Application') &&
      cookie.secure && cookie.httpOnly), 'Identity cookie is Secure and HttpOnly')
    console.log('PASS browser: anonymous denial and real admin login over HTTPS')

    await page.goto(origin + '/admin')
    const dashboardShortcuts = page.getByRole('navigation', { name: 'إجراءات الإدارة' })
    await dashboardShortcuts.waitFor()
    const expectedShortcuts = [
      ['مراجعة الطلبات', '/admin/orders'],
      ['تسجيل طلب من فيسبوك', '/admin/orders/manual'],
      ['إدارة المخزون', '/admin/inventory'],
      ['التوصيل والرواجع', '/admin/delivery'],
      ['مراجعة المنتجات', '/admin/products']
    ]
    assert.equal(await dashboardShortcuts.getByRole('link').count(), expectedShortcuts.length,
      'dashboard exposes exactly the five expected shortcuts')
    for (const [label, href] of expectedShortcuts) {
      assert.equal(await dashboardShortcuts.getByRole('link', { name: label, exact: true }).getAttribute('href'), href,
        `dashboard shortcut ${label} targets ${href}`)
    }
    console.log('PASS browser: authenticated dashboard shortcuts have accessible names and correct routes')

    await page.route('**/api/admin/orders?*', route => route.fulfill({ json: {
      items: [{ id: 99, customerName: 'Test', city: 'Test', orderDate: '2026-01-01',
        status: 'Pending', source: 'Website', total: '25.00', shippingFee: '5.00' }],
      page: 2, pages: 3, pageSize: 25, total: 51
    } }))
    await page.route('**/api/admin/orders/99', route => route.fulfill({ status: 404, json: {} }))
    await page.goto(origin + '/admin/orders?status=pending&source=Website&page=2')
    await page.getByRole('link', { name: /طلب #99/ }).click()
    await page.waitForURL(/\/admin\/orders\/99\?/)
    const detailUrl = new URL(page.url())
    assert.equal(detailUrl.searchParams.get('status'), 'pending')
    assert.equal(detailUrl.searchParams.get('source'), 'Website')
    assert.equal(detailUrl.searchParams.get('page'), '2')
    await page.getByRole('navigation', { name: 'تنقل الطلب' }).getByRole('link', { name: 'الطلبات', exact: true }).click()
    await page.waitForURL(/\/admin\/orders\?/)
    assert.equal(new URL(page.url()).searchParams.get('page'), '2')
    await page.goto(origin + '/admin/orders/99?status=pending&search=private&source=Website&page=2')
    const returnHref = await page.getByRole('navigation', { name: 'تنقل الطلب' }).getByRole('link', { name: 'الطلبات', exact: true }).getAttribute('href')
    assert.ok(!returnHref.includes('search'), 'details return excludes personal search input')
    await page.unroute('**/api/admin/orders?*')
    await page.unroute('**/api/admin/orders/99')

    await page.goto(origin + '/admin/categories')
    await page.getByLabel('اسم الفئة').fill('فئة اختبار المتصفح')
    await page.getByRole('button', { name: 'حفظ الفئة' }).click()
    await page.getByText('تمت إضافة الفئة.', { exact: true }).waitFor()
    await page.goto(origin + '/admin/products/new')
    await page.getByLabel('اسم المنتج').fill('زيت اختبار المتصفح')
    await page.getByLabel('الفئة', { exact: true }).selectOption({ label: 'فئة اختبار المتصفح' })

    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN0sAAAAASUVORK5CYII=', 'base64')
    const image = name => ({ name, mimeType: 'image/png', buffer: png })
    await page.getByLabel('أو اختر ملف غلاف').setInputFiles(image('cover.png'))
    await page.getByLabel('اختيار الصور').setInputFiles([image('retained.png'), image('removed.png')])
    await page.getByRole('button', { name: 'إزالة removed.png' }).click()
    await page.getByRole('button', { name: 'حفظ المنتج' }).click()
    await page.waitForURL(/\/admin\/products\/[0-9]+$/)
    await page.getByLabel('اسم الحجم').fill('100 مل')
    await page.getByLabel('السعر بالدينار الليبي').fill('19.95')
    await page.getByRole('button', { name: 'حفظ الحجم' }).click()
    await page.getByText('تم حفظ الحجم والسعر.', { exact: true }).waitFor()
    const productId = new URL(page.url()).pathname.split('/').at(-1)
    console.log('PASS browser: category, multipart cover, gallery removal and exact variant price')

    const shop = await browser.newContext(mobile)
    const customer = await shop.newPage()
    customer.setDefaultTimeout(15000)
    await customer.goto(origin + '/products/' + productId)
    await customer.getByRole('heading', { name: 'زيت اختبار المتصفح' }).waitFor()
    await customer.getByText('نسخة تجريبية. استخدم بيانات اختبار فقط.', { exact: true }).waitFor()
    assert.match(await customer.locator('meta[name="robots"]').getAttribute('content'), /noindex/)
    assert.equal(await customer.getByRole('button', { name: /^عرض الصورة/ }).count(), 2)
    await customer.getByRole('button', { name: 'عرض الصورة 2' }).click()
    assert.equal(await customer.getByRole('button', { name: 'عرض الصورة 2' }).getAttribute('aria-pressed'), 'true')
    assert.ok(await customer.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'product fits the mobile viewport without horizontal scrolling')
    await customer.getByRole('button', { name: 'أضيفي للسلة' }).click()
    await customer.getByText('تمت الإضافة للسلة.', { exact: true }).waitFor()
    const cartLink = customer.getByRole('navigation', { name: 'القائمة الرئيسية' }).locator('a[href="/cart"]')
    const cartCount = cartLink.getByText('1', { exact: true })
    await cartCount.waitFor({ state: 'visible' })
    await customer.reload()
    await cartCount.waitFor({ state: 'visible' })
    await customer.getByRole('link', { name: 'عرض السلة' }).click()
    await customer.getByRole('link', { name: 'إتمام الطلب' }).click()
    await customer.getByLabel('الاسم الأول').fill('اختبار')
    await customer.getByLabel('اسم العائلة').fill('المتصفح')
    await customer.getByLabel('رقم الواتساب').fill('0920000000')
    await customer.getByLabel('العنوان بالتفصيل').fill('عنوان تجريبي داخل قاعدة بيانات الاختبار')
    await customer.getByLabel('مدينة التوصيل').selectOption({ index: 1 })
    await customer.getByRole('button', { name: 'تأكيد الطلب والدفع عند الاستلام' }).click()
    await customer.waitForURL(/\/checkout\/receipt$/)
    await customer.getByText(/^طلب رقم /).waitFor()
    const receiptResponse = await shop.request.get(origin + '/api/checkout/receipt')
    assert.equal(receiptResponse.status(), 200)
    const receipt = await receiptResponse.json()
    assert.equal(receipt.productsTotal, '19.95')
    assert.equal(receipt.shippingFee, '1.00')
    assert.equal(receipt.total, '20.95')
    assert.equal(await cartLink.locator('bdi').count(), 0, 'cart badge clears on the receipt')
    const [clearedCart] = await Promise.all([
      customer.waitForResponse(response => response.url() === origin + '/api/cart' && response.request().method() === 'GET'),
      customer.reload()
    ])
    assert.equal(clearedCart.status(), 200)
    assert.equal((await clearedCart.json()).count, 0, 'checkout clears the persisted cart')
    assert.equal(await cartLink.locator('bdi').count(), 0, 'cart badge stays clear after receipt reload')
    assert.equal((await admin.request.get(origin + '/api/checkout/receipt')).status(), 404)
    console.log('PASS browser: mobile gallery, cart, guest checkout, exact totals and private receipt')

    for (const destinations of [null, []]) {
      await customer.route('**/api/checkout', route => route.fulfill({
        json: { checkoutToken: 'fixture', quoteToken: null, productsTotal: '19.95', shippingFee: null,
          total: null, cartRevision: 'fixture', shippingRates: destinations ? [
            { id: 1, city: 'Synthetic fallback', price: '1.00', total: '20.95' }
          ] : [], destinations, errors: [] }
      }))
      await customer.goto(origin + '/checkout')
      await customer.getByText('التوصيل غير متاح حاليًا.', { exact: true }).waitFor()
      assert.equal(await customer.getByLabel('رقم الواتساب').count(), 0)
      assert.equal(await customer.getByRole('button', { name: 'تأكيد الطلب والدفع عند الاستلام' }).count(), 0)
      assert.equal(await customer.getByRole('button', { name: 'إعادة المحاولة' }).isEnabled(), true)
      await customer.unroute('**/api/checkout')
    }
    console.log('PASS browser: empty delivery coverage blocks checkout in both modes')

    for (const device of [
      { name: 'mobile', options: mobile },
      { name: 'desktop', options: { viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true } }
    ]) {
      const context = await browser.newContext(device.options)
      const customer = await context.newPage()
      customer.setDefaultTimeout(15000)
      const deliveryChoices = {
        checkoutToken: 'fixture', quoteToken: null, productsTotal: '25.00', shippingFee: null,
        total: null, cartRevision: 'fixture', shippingRates: [], errors: [],
        destinations: [
          { key: 'women-home', city: 'طرابلس', area: 'عين زارة', service: 'women', isPickup: false },
          { key: 'men-office', city: 'طرابلس', area: 'مكتب الاختبار', service: 'men', isPickup: true },
          { key: 'men-home', city: 'طرابلس', area: 'عين زارة', service: 'men', isPickup: false }
        ]
      }
      const quotedChoices = []
      await customer.route('**/api/checkout', route => route.fulfill({ json: deliveryChoices }))
      await customer.route('**/api/checkout/shipping', route => {
        const selection = route.request().postDataJSON()
        quotedChoices.push(selection)
        const office = selection.carrierDestination === 'men-office'
        return route.fulfill({ json: { ...deliveryChoices, quoteToken: 'fixture-quote',
          shippingFee: office ? '5.00' : '15.00', total: office ? '30.00' : '40.00' } })
      })
      await customer.goto(origin + '/checkout')
      await customer.getByRole('radio', { name: 'توصيل نسائي' }).check()
      assert.equal(await customer.getByLabel('رقم الواتساب').count(), 0)
      await customer.getByRole('radio', { name: 'استلام من المكتب' }).check()
      await customer.getByLabel('مدينة التوصيل').selectOption('طرابلس')
      await customer.getByText('30.00', { exact: true }).waitFor()
      assert.equal(await customer.getByLabel('العنوان بالتفصيل').count(), 0)
      await customer.getByLabel('الاسم الأول').waitFor()
      await customer.getByRole('radio', { name: 'توصيل رجالي' }).check()
      assert.equal(await customer.getByText('30.00', { exact: true }).count(), 0)
      await customer.getByLabel('مدينة التوصيل').selectOption('طرابلس')
      await customer.getByText('40.00', { exact: true }).waitFor()
      await customer.getByLabel('العنوان بالتفصيل').waitFor()
      assert.equal(await customer.getByRole('radio', { name: 'استلام من المكتب' }).count(), 1)
      assert.deepEqual(quotedChoices, [
        { checkoutToken: 'fixture', carrierDestination: 'men-office' },
        { checkoutToken: 'fixture', carrierDestination: 'men-home' }
      ])
      await context.close()
      console.log(`PASS browser: ${device.name} service choices, conditional office pickup and automatic private quoting`)
    }
  } finally {
    await browser?.close()
    gateway.closeAllConnections()
    await new Promise(resolve => gateway.close(resolve))
  }
})

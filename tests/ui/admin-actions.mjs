import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'http://127.0.0.1:15188';
assert.equal((await fetch(base + '/Login')).headers.get('x-motoshop-test-fixture'), 'true');
const output = path.join(root, 'docs/ui-review/admin-actions');
await mkdir(output, { recursive: true });
const png = await readFile(path.join(root, 'Infrastructure/wwwroot/images/1.png'));
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ locale: 'vi-VN' });
const page = await context.newPage();
page.setDefaultTimeout(15000);
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const results = [];
const state = () => page.request.get(base + '/_fixture/admin-state').then(response => response.json());
async function check(name, work) {
  try { await work(); results.push({ name, status: 'PASS' }); console.log('PASS ' + name); }
  catch (error) { results.push({ name, status: 'FAIL', message: error.message }); console.error('FAIL ' + name + ': ' + error.message); await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
}
const submit = async () => { await page.locator('button[type=submit]').click(); await page.waitForLoadState('networkidle'); };
let postId, productId;
try {
  await page.goto(base + '/_fixture/identity/admin');
  await check('article-create-posts-to-real-service-and-database', async () => {
    await page.goto(base + '/BlogList'); // Warm real production cache before mutation.
    await page.locator('a[href="/Article"]').click();
    await page.locator('[name=Title]').fill('Admin action create article');
    await page.locator('.note-editable').fill('Actual article content');
    await page.locator('[name=ImageUrl]').setInputFiles({ name: 'cover.png', mimeType: 'image/png', buffer: png });
    await submit();
    await page.waitForURL(base + '/BlogList');
    assert.match(await page.locator('.notice').innerText(), /thành công/);
    const row = (await state()).posts.find(post => post.title === 'Admin action create article');
    assert.ok(row); postId = row.id;
    assert.match(row.content, /Actual article content/);
    assert.match(row.imageUrl, /^[a-f0-9]{32}_\d{14}\.png$/);
    await page.reload();
    assert.equal((await state()).posts.filter(post => post.id === postId).length, 1);
  });
  await check('article-edit-refreshes-cache-and-keeps-cover', async () => {
    const before = (await state()).posts.find(post => post.id === postId);
    await page.goto(base + '/Article/' + postId);
    await page.locator('[name=Title]').fill('Admin action updated article');
    await page.locator('.note-editable').fill('Updated actual content');
    await submit();
    await page.waitForURL(base + '/BlogList');
    assert.equal(await page.locator(`a[href="/Post/${postId}"]`).innerText(), 'Admin action updated article');
    await page.goto(base + '/Post/' + postId);
    assert.match(await page.locator('h1').innerText(), /updated article/);
    assert.match(await page.locator('main').innerText(), /Updated actual content/);
    const after = (await state()).posts.find(post => post.id === postId);
    assert.equal(after.imageUrl, before.imageUrl);
    assert.equal(after.createdAt, before.createdAt);
  });
  await check('article-validation-shows-real-error-and-keeps-input', async () => {
    const before = (await state()).posts.length;
    await page.goto(base + '/Article');
    const response = await page.request.post(base + '/Article', { multipart: { Title: 'x'.repeat(101), Content: '<p>Keep this content</p>' } });
    assert.equal(response.status(), 200);
    await page.setContent(await response.text());
    assert.match(await page.locator('[data-valmsg-for=Title]').innerText(), /100/);
    assert.match(await page.locator('[data-valmsg-for=ImageUrl]').innerText(), /ảnh bìa/);
    assert.equal(await page.locator('[name=Title]').inputValue(), 'x'.repeat(101));
    assert.equal((await state()).posts.length, before);
  });
  await check('product-create-uses-generated-id-for-coupon', async () => {
    await page.goto(base + '/Manage');
    await page.locator('[name=Name]').fill('Admin action new product');
    await page.locator('[name=Brand]').fill('Real brand');
    await page.locator('[name=CategoryId]').selectOption('1');
    await page.locator('[name=Price]').fill('250000');
    await page.locator('[name=Quantity]').fill('5');
    await page.locator('[name=Coupon]').fill('ADMINTEST');
    await page.locator('[name=Discount]').fill('12');
    await page.locator('.note-editable').fill('Actual product description');
    await submit();
    assert.match(await page.locator('.notice').innerText(), /thành công/);
    const saved = await state();
    const product = saved.products.find(product => product.name === 'Admin action new product');
    assert.ok(product); productId = product.id;
    assert.equal(product.oldPrice, 0);
    assert.equal(saved.discounts.find(discount => discount.id === productId)?.coupon, 'ADMINTEST');
  });
  await check('product-edit-coupon-and-price-independent-of-other-tabs', async () => {
    await page.goto(base + '/Edit/' + productId);
    const other = await context.newPage();
    await other.goto(base + '/Edit/1'); // Previously overwrote the shared TempData price.
    await other.close();
    await page.locator('[name=Price]').fill('200000');
    await page.locator('[name=Quantity]').fill('9');
    await page.locator('[name=Discount]').fill('20');
    await page.locator('.note-editable').fill('Updated product description');
    await submit();
    assert.match(await page.locator('.notice').innerText(), /thành công/);
    const saved = await state();
    const product = saved.products.find(product => product.id === productId);
    assert.equal(product.price, 200000); assert.equal(product.oldPrice, 250000); assert.equal(product.quantity, 9);
    assert.match(product.description, /Updated product description/);
    assert.equal(saved.discounts.find(discount => discount.id === productId).discountPercent, 20);
  });
  await check('product-credential-management-removed', async () => {
    await page.goto(base + '/Edit/' + productId);
    assert.equal(await page.locator('a[href*="EditAccountInfor"],a[href^="/Infor/"]').count(), 0);
    for (const url of ['/EditAccountInfor/' + productId, '/Infor/0/' + productId])
      assert.equal((await page.request.get(base + url)).status(), 404);
    assert.equal((await page.request.post(base + '/Infor/0/' + productId, { form: { AccountName: 'must not save' } })).status(), 404);
    assert.equal((await state()).credentials.some(item => item.productId === productId), false);
  });
  await check('product-table-filter-sort-pagination-and-cancel-delete', async () => {
    await page.goto(base + '/Product-List');
    assert.equal(await page.locator('tr[data-row]:visible').count(), 20);
    await page.locator('[data-table-next]').click();
    assert.equal(await page.locator('tr[data-row]:visible').count(), 7);
    await page.locator('[data-table-previous]').click();
    await page.locator('[data-table-stock]').selectOption('out');
    assert.equal(await page.locator('tr[data-row]:visible').count(), 1);
    await page.locator('[data-table-stock]').selectOption('');
    await page.locator('[data-table-sort]').selectOption('price');
    const prices = await page.locator('tr[data-row]:visible').evaluateAll(rows => rows.map(row => Number(row.dataset.price)));
    assert.deepEqual(prices, [...prices].sort((a,b) => a-b));
    await page.locator('[data-table-search]').fill('Admin action new product');
    const row = page.locator('tr[data-row]:visible');
    assert.equal(await row.count(), 1);
    await row.locator('[data-confirm-delete]').click();
    await page.locator('#confirm-delete .btn-outline').click();
    assert.ok((await state()).products.find(product => product.id === productId));
  });
  await check('delete-product-without-image-actually-deletes-and-cascades', async () => {
    await page.locator('tr[data-row]:visible [data-confirm-delete]').click();
    await page.locator('#confirm-delete-link').click();
    await page.waitForLoadState('networkidle');
    assert.match(await page.locator('.notice').innerText(), /thành công/);
    const saved = await state();
    assert.equal(saved.products.some(product => product.id === productId), false);
    assert.equal(saved.discounts.some(item => item.id === productId), false);
    assert.equal(saved.credentials.some(item => item.productId === productId), false);
    await page.locator('.notice-dismiss').click();
    assert.equal(await page.locator('.notice').count(), 0);
  });
  await check('article-table-search-sort-pagination-and-delete', async () => {
    await page.goto(base + '/BlogList');
    assert.equal(await page.locator('tr[data-row]:visible').count(), 20);
    await page.locator('[data-table-next]').click();
    assert.equal(await page.locator('tr[data-row]:visible').count(), 7);
    await page.locator('[data-table-sort]').selectOption('name');
    await page.locator('[data-table-search]').fill('Admin action updated article');
    assert.equal(await page.locator('tr[data-row]:visible').count(), 1);
    await page.locator('tr[data-row]:visible [data-confirm-delete]').click();
    await page.locator('#confirm-delete-link').click();
    await page.waitForLoadState('networkidle');
    assert.equal((await state()).posts.some(post => post.id === postId), false);
    await page.reload();
    assert.equal(await page.locator(`a[href="/Post/${postId}"]`).count(), 0);
  });
  await check('navigation-cancel-sales-mobile-menu-and-logout', async () => {
    await page.goto(base + '/Manage');
    for (const target of ['/Product-List', '/BlogList', '/api/AHP', '/Manage']) {
      await page.locator(`.admin-nav a[href="${target}"]`).click();
      await page.waitForURL(base + target);
    }
    await page.goto(base + '/Edit/1');
    await page.locator('.form-actions a[href="/Product-List"]').click();
    await page.waitForURL(base + '/Product-List');
    await page.setViewportSize({ width: 390, height: 844 });
    await page.locator('.admin-menu-trigger').click();
    assert.equal(await page.locator('.admin-menu-trigger').getAttribute('aria-expanded'), 'true');
    await page.keyboard.press('Escape');
    assert.equal(await page.locator('.admin-menu-trigger').getAttribute('aria-expanded'), 'false');
    await page.locator('.admin-menu-trigger').click();
    await page.locator('.admin-nav a[href="/Logout"]').click();
    await page.waitForURL(base + '/');
    const protectedResponse = await page.request.get(base + '/_fixture/admin-state', { maxRedirects: 0 });
    assert.equal(protectedResponse.status(), 302);
    assert.match(protectedResponse.headers().location, /\/Login/);
  });
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
  const report = { passed: results.filter(result => result.status === 'PASS').length, failed: results.filter(result => result.status === 'FAIL').length, source: 'Real MVC controllers, CachablePostManage/ProductManage and EF repositories on isolated SQLite. Test identity and external providers only are fixtures; no business DB.', results, pageErrors: errors };
  await writeFile(path.join(output, 'browser-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
  if (report.failed || errors.length) process.exitCode = 1;
}

import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'http://127.0.0.1:15187';
assert.equal((await fetch(base + '/Login')).headers.get('x-motoshop-test-fixture'), 'true');
const output = path.join(root, 'docs/ui-review/cod');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [], errors = [];
async function check(name, work, role = 'guest', javaScriptEnabled = true) {
  const context = await browser.newContext({ locale: 'vi-VN', javaScriptEnabled });
  const page = await context.newPage();
  page.setDefaultTimeout(12000);
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(base + '/_fixture/identity/' + role);
    await page.request.get(base + '/_fixture/mail/reset');
    if (role === 'guest') await page.goto(base + '/Cart-Add/1/test?quantity=2');
    await work(page);
    results.push({ name, status: 'PASS' }); console.log('PASS ' + name);
  } catch (error) {
    results.push({ name, status: 'FAIL', message: error.message }); console.error('FAIL ' + name + ': ' + error.message);
    await page.screenshot({ path: path.join(output, name + '-failure.png'), fullPage: true });
  } finally { await context.close(); }
}
async function fill(page, role = 'guest', address = true) {
  await page.goto(base + (role === 'guest' ? '/Pay/0' : '/Pay/1'));
  await page.locator('[name=name]').fill('COD test buyer');
  await page.locator('[name=phone]').fill('0901234567');
  await page.locator('[name=email]').fill('cod-test@example.invalid');
  if (address) await page.locator('[name=address]').fill('123 Đường thử nghiệm, Phường thử, Thành phố thử');
  await page.locator('[name=note]').fill('Gọi trước khi giao');
}
const mails = page => page.request.get(base + '/_fixture/mail/stats').then(response => response.json());
try {
  await check('guest-cod-real-controller-sends-two-emails', async page => {
    await fill(page);
    await page.locator('[name=totalAmount]').evaluate(input => input.value = '1');
    await page.locator('#cod-confirm').click();
    await page.waitForURL(base + '/Payment/CodConfirmed');
    assert.match(await page.locator('main').innerText(), /chưa thanh toán/i);
    assert.match(await page.locator('.price').innerText(), /520\.000/);
    const sent = await mails(page);
    assert.equal(sent.length, 2);
    assert.equal(sent[0].to, 'cod-test@example.invalid');
    assert.equal(sent[1].to, 'luongnhattruong2004@gmail.com');
    for (const mail of sent) {
      assert.match(mail.subject, /COD/); assert.match(mail.body, /Chưa thanh toán/);
      assert.match(mail.body, /123 Đường thử nghiệm/); assert.match(mail.body, /520\.000/);
    }
    await page.reload(); assert.equal((await mails(page)).length, 2);
    await page.goto(base + '/Cart'); assert.match(await page.locator('main').innerText(), /Giỏ hàng đang trống/);
  });
  await check('member-cod-confirmation-and-email', async page => {
    await fill(page, 'member');
    await page.locator('#cod-confirm').click();
    await page.waitForURL(base + '/Payment/CodConfirmed');
    assert.equal((await mails(page)).length, 2);
    assert.match(await page.locator('.price').innerText(), /260\.000/);
  }, 'member');
  await check('no-javascript-cod-validates-address-and-preserves-contact', async page => {
    await fill(page, 'guest', false);
    await page.locator('#cod-confirm').press('Enter');
    await page.waitForLoadState('networkidle');
    assert.match(await page.locator('[data-valmsg-for=Address]').innerText(), /địa chỉ/);
    assert.equal(await page.locator('[name=name]').inputValue(), 'COD test buyer');
    assert.equal((await mails(page)).length, 0);
    await page.locator('[name=address]').fill('123 Địa chỉ nhận hàng');
    await page.locator('#cod-confirm').press('Enter');
    await page.waitForURL(base + '/Payment/CodConfirmed');
    assert.equal((await mails(page)).length, 2);
  }, 'guest', false);
  await check('smtp-failure-shows-error-keeps-cart-and-can-retry', async page => {
    await fill(page);
    await page.request.get(base + '/_fixture/mail/fail');
    await page.locator('#cod-confirm').click();
    await page.waitForLoadState('networkidle');
    assert.match(await page.locator('#checkout-form [role=alert]').innerText(), /Chưa gửi được đầy đủ email/);
    assert.equal(await page.locator('[name=address]').inputValue(), '123 Đường thử nghiệm, Phường thử, Thành phố thử');
    assert.equal((await page.request.get(base + '/Payment/CodConfirmed', { maxRedirects: 0 })).status(), 302);
    await page.request.get(base + '/_fixture/mail/reset');
    await page.locator('#cod-confirm').click();
    await page.waitForURL(base + '/Payment/CodConfirmed');
    assert.equal((await mails(page)).length, 2);
  });
  await check('cod-antiforgery-blocks-direct-forged-submit', async page => {
    await fill(page);
    const response = await page.request.post(base + '/Payment/PayByCod', { form: { name: 'Fake', phone: '0901234567', email: 'fake@example.invalid', address: 'Fake address' } });
    assert.equal(response.status(), 400);
    assert.equal((await mails(page)).length, 0);
  });
  await check('bank-confirmation-still-works-after-empty-cod-address', async page => {
    await fill(page, 'guest', false);
    await page.locator('#cod-confirm').click();
    assert.equal(await page.locator('[name=address]').evaluate(input => input.validity.customError), true);
    await page.locator('#bank-confirm').click();
    await page.waitForURL(base + '/CF-Order');
    assert.match(await page.locator('h1').innerText(), /Đã tiếp nhận thông tin/);
    assert.equal((await mails(page)).length, 2);
  });
  await check('checkout-cod-responsive-and-online-methods-preserved', async page => {
    for (const width of [390, 1440]) {
      await page.setViewportSize({ width, height: 1000 });
      await fill(page, 'member');
      assert.equal(await page.locator('form[action="/Payment/PayByMomo"]').count(), 1);
      assert.equal(await page.locator('form[action="/Payment/PayByVnpay"]').count(), 1);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await page.screenshot({ path: path.join(output, `checkout-${width}.png`), fullPage: true });
    }
  }, 'member');
  await check('product-key-section-and-old-routes-removed', async page => {
    await page.goto(base + '/Edit/1');
    assert.equal(await page.locator('a[href*="EditAccountInfor"],a[href^="/Infor/"]').count(), 0);
    for (const url of ['/EditAccountInfor/1', '/Infor/0/1', '/Product/AccountInfor/1']) assert.equal((await page.request.get(base + url)).status(), 404);
    assert.equal((await page.request.post(base + '/Infor/0/1', { form: { AccountName: 'must not save' } })).status(), 404);
  }, 'admin');
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
  const report = { passed: results.filter(item => item.status === 'PASS').length, failed: results.filter(item => item.status === 'FAIL').length, source: 'Real MVC payment/product controllers; isolated fixture catalog/cart and captured SMTP sender. No business DB, real emails or payment transactions.', results, pageErrors: errors, realSmtp: 'NOT_VERIFIED' };
  await writeFile(path.join(output, 'browser-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
  if (report.failed || errors.length) process.exitCode = 1;
}

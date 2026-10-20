import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const base = process.env.MOTOSHOP_UI_URL || 'http://127.0.0.1:15187';
const probe = await fetch(base + '/Login');
assert.equal(probe.headers.get('x-motoshop-test-fixture'), 'true', 'Chat checks require the isolated fixture host.');
const output = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/ui-review/chat/gemini');
const failureMessage = 'Xin lỗi, hiện tại trợ lý chưa thể trả lời. Vui lòng thử lại sau.';
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
const errors = [];
const context = await browser.newContext({ locale: 'vi-VN' });
const page = await context.newPage();
const warnings = [];
page.on('console', message => { if (message.type() === 'warning') warnings.push(message.text()); });
page.on('pageerror', error => errors.push(error.message));
const launcher = page.getByRole('button', { name: 'Mở chat tư vấn', exact: true });
const panel = page.getByRole('dialog', { name: 'Trợ lý MotoShop' });
const input = page.getByRole('textbox', { name: 'Tin nhắn tư vấn' });
const botMessages = page.locator('.shop-chat-message.bot:not(.shop-chat-greeting)');
const userMessages = page.locator('.shop-chat-message.user');
const errorBox = page.locator('#shop-chat-error');
const tick = name => results.push({ name, status: 'PASS' });
async function expectVisible(locator) { await locator.waitFor({ state: 'visible' }); }
async function expectSettled() { await page.waitForFunction(() => document.querySelector('#shop-chat-status').textContent === ''); }
async function reset() { await page.getByRole('button', { name: 'Trò chuyện mới', exact: true }).click(); }
async function ask(text) { await input.fill(text); await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).click(); }

try {
  // Exercise the actual MVC endpoint without invoking a real provider or database.
  for (const [prompt, status, code] of [['', 400, 'invalid_prompt'], ['x'.repeat(1001), 400, 'invalid_prompt'], ['[fixture:unconfigured]', 503, 'chat_unavailable'], ['[fixture:provider-error]', 502, 'chat_unavailable'], ['[fixture:provider-billing]', 503, 'chat_unavailable'], ['[fixture:provider-auth]', 503, 'chat_unavailable'], ['[fixture:provider-rate]', 429, 'chat_unavailable'], ['[fixture:provider-blocked]', 422, 'chat_unavailable'], ['[fixture:provider-empty]', 502, 'chat_unavailable']]) {
    const response = await context.request.post(base + '/api/chat/ask', { data: JSON.stringify(prompt), headers: { 'Content-Type': 'application/json' } });
    assert.equal(response.status(), status);
    const body = await response.json();
    assert.equal(body.code, code);
    if (code === 'chat_unavailable') assert.equal(body.message, failureMessage);
    assert.equal(JSON.stringify(body).includes('PRIVATE_PROVIDER_ERROR'), false);
    tick(`api-${code}-${prompt.length}`);
  }
  for (const width of [360, 390, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(base + '/');
    assert.equal(await panel.count(), 0, 'Closed chat must not be exposed as an open dialog.');
    await launcher.click();
    await expectVisible(panel);
    assert.equal(await input.evaluate(el => document.activeElement === el), true);
    assert.equal(await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).isDisabled(), true);
    const bounds = await panel.boundingBox();
    assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width && bounds.y >= 0 && bounds.y + bounds.height <= 901);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    assert.equal(await page.locator('.shop-chat-avatar').evaluate(image => image.complete && image.naturalWidth > 0), true);
    await page.screenshot({ path: path.join(output, `chat-${width}.png`) });
    await input.press('Escape');
    assert.equal(await launcher.evaluate(el => document.activeElement === el), true);
    tick(`responsive-keyboard-avatar-${width}`);
  }
  await launcher.click();
  await ask('Cửa hàng có sản phẩm nào?');
  await expectVisible(botMessages.first());
  assert.match(await botMessages.first().innerText(), /TEST FIXTURE ONLY/);
  assert.equal(await botMessages.first().locator('b').count(), 1);
  assert.equal(await userMessages.count(), 1);
  tick('original-http-json-string-contract-real-controller');

  await page.goto(base + '/Product/1');
  await launcher.click();
  assert.equal(await userMessages.count(), 1);
  assert.equal(await botMessages.count(), 1);
  await reset();
  assert.equal(await userMessages.count(), 0);
  assert.equal(await botMessages.count(), 0);
  tick('history-survives-navigation-and-reset');

  await page.getByRole('button', { name: 'Gợi ý sản phẩm' }).click();
  await expectVisible(botMessages.first());
  assert.match(await userMessages.first().innerText(), /Gợi ý sản phẩm trong cửa hàng/);
  tick('quick-question');
  await expectSettled();
  await reset();

  await input.fill('Dòng đầu');
  await input.press('Shift+Enter');
  await input.pressSequentially('Dòng tiếp theo');
  assert.equal(await input.inputValue(), 'Dòng đầu\nDòng tiếp theo');
  await input.evaluate(el => el.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true, bubbles: true })));
  assert.equal(await userMessages.count(), 0);
  await input.press('Enter');
  await expectVisible(botMessages.first());
  tick('enter-shift-enter-and-ime');
  await expectSettled();
  await reset();

  await ask('[fixture:provider-error]');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  assert.equal(await errorBox.getByRole('link', { name: 'Liên hệ Zalo' }).getAttribute('href'), 'https://zalo.me/0358223929');
  await expectSettled();
  await input.fill('Bản nháp đang nhập');
  await page.route('**/api/chat/ask', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ choices: [{ message: { content: 'Đã thử lại thành công.' } }] }) }));
  await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await expectVisible(botMessages.first());
  assert.equal(await userMessages.count(), 1);
  assert.equal(await input.inputValue(), 'Bản nháp đang nhập');
  await page.unroute('**/api/chat/ask');
  tick('generic-error-zalo-and-retry-without-duplicate');
  await expectSettled();
  await reset();

  await ask('[fixture:provider-billing]');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  assert.equal(await errorBox.getByRole('button', { name: 'Thử lại', exact: true }).count(), 0);
  assert.equal(await errorBox.getByRole('link', { name: 'Liên hệ Zalo' }).count(), 1);
  assert.equal((await errorBox.innerText()).includes('PRIVATE_PROVIDER_BILLING_DETAIL'), false);
  assert.equal(await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).isDisabled(), true);
  await input.fill('Có thể tiếp tục nhập câu hỏi khác');
  assert.equal(await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).isDisabled(), false);
  await page.screenshot({ path: path.join(output, 'generic-error.png') });
  tick('billing-failure-generic-message-without-useless-retry');
  await expectSettled();
  await reset();

  await ask('[fixture:provider-rate]');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  assert.equal(await errorBox.getByRole('button', { name: 'Thử lại', exact: true }).count(), 1);
  tick('rate-limit-error-can-be-retried-after-waiting');
  await expectSettled();
  await reset();

  let release;
  let calls = 0;
  const hold = new Promise(resolve => release = resolve);
  await page.route('**/api/chat/ask', async route => { calls++; await hold; await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ choices: [{ message: { content: 'Phản hồi sau khi chờ.' } }] }) }); });
  await ask('Câu hỏi đang chờ');
  await expectVisible(page.locator('#shop-chat-status'));
  assert.equal(await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).isDisabled(), true);
  assert.equal(await page.getByRole('button', { name: 'Trò chuyện mới', exact: true }).isDisabled(), true);
  await input.fill('Soạn tiếp khi chờ');
  await input.press('Enter');
  assert.equal(await userMessages.count(), 1);
  await page.getByRole('button', { name: 'Đóng chat', exact: true }).click();
  await launcher.click();
  release();
  await expectVisible(botMessages.first());
  await expectSettled();
  assert.equal(calls, 1);
  assert.equal(await input.inputValue(), 'Soạn tiếp khi chờ');
  await page.unroute('**/api/chat/ask');
  tick('pending-single-request-draft-and-close-reopen');
  await reset();

  await page.route('**/api/chat/ask', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ choices: [{ message: { content: '<script>window.CHAT_XSS=1</script><img src=x onerror="window.CHAT_XSS=2"><svg onload="window.CHAT_XSS=3"></svg><b onclick="window.CHAT_XSS=4">Định dạng an toàn</b><br><a href="javascript:alert(1)">Link nguy hiểm</a><a href="/Product/1" onclick="window.CHAT_XSS=5">Sản phẩm</a><a href="http://[invalid">Link hỏng</a>' } }] }) }));
  await ask('<img src=x onerror="window.CHAT_XSS=6">');
  await expectVisible(botMessages.first());
  assert.equal(await userMessages.locator('img').count(), 0);
  assert.equal(await botMessages.locator('script,img,svg,[onclick],[onerror]').count(), 0);
  assert.equal(await botMessages.getByRole('link').count(), 1);
  assert.equal(await botMessages.getByRole('link', { name: 'Sản phẩm' }).getAttribute('href'), '/Product/1');
  assert.equal(await page.evaluate(() => window.CHAT_XSS), undefined);
  await expectSettled();
  await page.unroute('**/api/chat/ask');
  tick('assistant-and-user-xss-protection-with-safe-formatting');
  await reset();

  await page.route('**/api/chat/ask', route => route.abort('failed'));
  await ask('Kiểm tra mất mạng');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  await expectSettled();
  await page.unroute('**/api/chat/ask');
  tick('network-error-recovery');
  await reset();

  await page.route('**/api/chat/ask', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
  await ask('Kiểm tra phản hồi trống');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  await expectSettled();
  await page.unroute('**/api/chat/ask');
  tick('malformed-response');
  await reset();

  await page.clock.install();
  await page.route('**/api/chat/ask', () => {});
  await ask('Kiểm tra hết thời gian chờ');
  await page.clock.fastForward(51000);
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  await expectSettled();
  await page.unrouteAll({ behavior: 'ignoreErrors' });
  tick('timeout-clears-busy-state');
  await reset();

  for (const prompt of ['[fixture:unconfigured]', '[fixture:provider-auth]', '[fixture:provider-blocked]', '[fixture:provider-empty]']) {
    await ask(prompt);
    await expectVisible(errorBox);
    assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
    await expectSettled();
    await reset();
  }
  tick('configuration-auth-safety-and-empty-use-identical-ui-text');

  await page.route('**/api/chat/ask', route => route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ message: 'PRIVATE_PROVIDER_ERROR_MUST_NOT_RENDER', code: 'PRIVATE_PROVIDER_CODE' }) }));
  await ask('PRIVATE_USER_PROMPT_MUST_NOT_LOG');
  await expectVisible(errorBox);
  assert.equal(await page.locator('#shop-chat-error-text').innerText(), failureMessage);
  assert.equal((await errorBox.innerText()).includes('PRIVATE_PROVIDER'), false);
  await expectSettled();
  await page.unroute('**/api/chat/ask');
  assert.ok(warnings.some(message => message.startsWith('Chat request failed.')));
  assert.equal(warnings.some(message => message.includes('PRIVATE_')), false);
  tick('raw-server-error-and-prompt-never-displayed-or-logged-in-console');
  await reset();

  assert.equal(await input.getAttribute('maxlength'), '1000');
  await page.setViewportSize({ width: 390, height: 480 });
  await page.screenshot({ path: path.join(output, 'chat-short-viewport.png') });
  const smallBounds = await panel.boundingBox();
  assert.ok(smallBounds.y >= 0 && smallBounds.y + smallBounds.height <= 481);
  assert.equal(await input.isVisible(), true);
  tick('small-height-viewport-and-input-limit');

  // Simulate visualViewport shrinking without a layout-viewport resize (mobile keyboard).
  await page.setViewportSize({ width: 390, height: 900 });
  await page.evaluate(() => {
    Object.defineProperty(window.visualViewport, 'height', { configurable: true, value: 480 });
    window.visualViewport.dispatchEvent(new Event('resize'));
  });
  const keyboardBounds = await panel.boundingBox();
  assert.ok(keyboardBounds.y >= 0 && keyboardBounds.y + keyboardBounds.height <= 481);
  const composerBounds = await input.boundingBox();
  assert.ok(composerBounds.y + composerBounds.height <= 480);
  tick('visual-viewport-keyboard-inset-simulation');
  await page.evaluate(() => { delete window.visualViewport.height; window.visualViewport.dispatchEvent(new Event('resize')); });

  for (const role of ['guest', 'member', 'admin']) {
    await page.goto(base + '/_fixture/identity/' + role);
    assert.equal(await launcher.count(), 1);
    tick(`storefront-available-${role}`);
  }
  await page.goto(base + '/Manage');
  assert.equal(await page.locator('[data-chat-widget]').count(), 0);
  await page.goto(base + '/Login');
  assert.equal(await page.locator('[data-chat-widget]').count(), 0);
  tick('admin-auth-layouts-preserved');
  assert.deepEqual(errors, []);
  const report = { passed: results.length, failed: 0, source: 'Isolated UI fixtures and intercepted HTTP; no real database or AI provider calls.', results };
  await writeFile(path.join(output, 'results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} finally { await context.close(); await browser.close(); }

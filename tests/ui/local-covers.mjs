import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'http://127.0.0.1:15187';
assert.equal((await fetch(base + '/Login')).headers.get('x-motoshop-test-fixture'), 'true', 'Requires isolated fixture host.');
const png = await readFile(path.join(root, 'Infrastructure/wwwroot/images/1.png'));
const output = path.join(root, 'docs/ui-review/cloudinary/config-fix');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
try {
  for (const kind of ['products', 'posts']) {
    const context = await browser.newContext({ locale: 'vi-VN' });
    const page = await context.newPage();
    const requests = [];
    page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/admin/editor-images')) requests.push(request.url()); });
    await page.goto(base + '/_fixture/identity/admin');
    const target = kind === 'products' ? '/Edit/1' : '/Article';
    await page.goto(base + target);
    await page.locator('.note-editable').waitFor();
    if (kind === 'posts') await page.locator('[name=Title]').fill('Kiểm tra lưu ảnh bìa cục bộ');
    await page.locator('.note-editable').fill('Nội dung chữ kiểm tra ảnh riêng, không cần gọi Cloudinary.');
    const directory = path.join(root, 'tests/MotoShop.UiHarness/.fixture-wwwroot/uploads', kind);
    const before = await readdir(directory).catch(error => error.code === 'ENOENT' ? [] : Promise.reject(error));
    await page.locator('[name=ImageUrl]').setInputFiles({ name: 'local-cover.png', mimeType: 'image/png', buffer: png });
    const preview = page.locator('img.image-preview');
    await page.waitForFunction(() => { const image = document.querySelector('img.image-preview'); return image?.src.startsWith('blob:') && image.complete && image.naturalWidth > 0; });
    await preview.screenshot({ path: path.join(output, `${kind}-local-preview.png`) });
    results.push({ name: `${kind}-local-image-preview`, status: 'PASS' });
    const sent = page.waitForResponse(response => new URL(response.url()).pathname === target && response.request().method() === 'POST');
    await page.locator('button[type=submit]').click();
    assert.equal((await sent).status(), kind === 'posts' ? 302 : 200);
    await page.locator('.notice').filter({ hasText: kind === 'products' ? 'Cập nhật sản phẩm thành công' : 'Đăng bài viết thành công' }).waitFor();
    const after = await readdir(directory);
    const created = after.filter(file => !before.includes(file));
    assert.equal(created.length, 1);
    assert.match(created[0], /^[a-f0-9]{32}_\d{14}\.png$/);
    assert.deepEqual(await readFile(path.join(directory, created[0])), png);
    assert.equal(requests.length, 0, 'Separate cover/product image inputs must continue saving locally.');
    results.push({ name: `${kind}-multipart-image-saved-locally-with-original-bytes`, status: 'PASS' });
    await context.close();
  }
  await writeFile(path.join(output, 'local-covers.json'), JSON.stringify({ passed: results.length, failed: 0, source: 'Fixture MVC controllers and local fixture webroot only. No business database or Cloudinary requests.', results }, null, 2));
  console.log(JSON.stringify({ passed: results.length, failed: 0, results }));
} finally { await browser.close(); }

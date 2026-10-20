import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'http://127.0.0.1:15187';
assert.equal((await fetch(base + '/Login')).headers.get('x-motoshop-test-fixture'), 'true', 'Refusing to run against the real application.');
const png = await readFile(path.join(root, 'Infrastructure/wwwroot/images/1.png'));
const dataUri = 'data:image/png;base64,' + png.toString('base64');
const ownedPrefix = 'https://res.cloudinary.com/motoshop-fixture/image/upload/';
const output = path.join(root, 'docs/ui-review/cloudinary');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
let identity = 100 + (Date.now() % 10000000) * 100;
const filter = process.env.MOTOSHOP_EDITOR_FILTER;
const errors = [];
async function check(name, work, role) {
  if (filter && !name.includes(filter)) return;
  console.log('RUN ' + name);
  const context = await browser.newContext({ locale: 'vi-VN' });
  await context.route('https://res.cloudinary.com/**', route => route.fulfill({ status: 200, contentType: 'image/png', body: png }));
  const page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(base + '/_fixture/identity/' + (role || 'admin-' + identity++));
    await work(page, context);
    results.push({ name, status: 'PASS' });
    console.log('PASS ' + name);
  } catch (failure) {
    results.push({ name, status: 'FAIL', message: failure.message });
    console.error(name + ': ' + failure.message);
    await page.screenshot({ path: path.join(output, name + '-failure.png'), fullPage: true }).catch(() => {});
  } finally { await context.close(); }
}
const edit = async (page, kind) => { await page.goto(base + (kind === 'article' ? '/Article' : '/Edit/1')); await page.locator('.note-editable').waitFor(); };
const stats = page => page.request.get(base + '/_fixture/cloudinary/stats').then(response => response.json());
const mode = (page, value) => page.request.get(base + '/_fixture/cloudinary/mode/' + value);
const token = page => page.locator('[name="__RequestVerificationToken"]').inputValue();
async function imagesReady(page, count) {
  await page.waitForFunction(({ count, prefix }) => {
    const images = [...document.querySelectorAll('.note-editable img')];
    return images.length === count && images.every(image => image.src.startsWith(prefix)) && !document.querySelector('button[type=submit]').disabled;
  }, { count, prefix: ownedPrefix });
}
async function paste(page, html, action = 'paste', files = false) {
  await page.locator('.note-editable').evaluate((target, { html, action, dataUri, files }) => {
    const data = new DataTransfer();
    if (html) data.setData('text/html', html);
    if (files) data.items.add(new File([Uint8Array.from(atob(dataUri.split(',')[1]), c => c.charCodeAt(0))], 'clipboard.png', { type: 'image/png' }));
    target.dispatchEvent(action === 'paste' ? new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }) : new DragEvent('drop', { dataTransfer: data, bubbles: true, cancelable: true }));
  }, { html, action, dataUri, files });
}
async function dropOnOverlay(page) {
  await page.locator('.note-editable').evaluate(target => {
    const data = new DataTransfer();
    data.items.add(new File(['drag marker'], 'marker.png', { type: 'image/png' }));
    target.dispatchEvent(new DragEvent('dragenter', { dataTransfer: data, bubbles: true, cancelable: true }));
  });
  await page.locator('.note-editor.dragover .note-dropzone').waitFor({ state: 'visible' });
  await page.locator('.note-dropzone-message').evaluate((target, dataUri) => {
    target.dispatchEvent(new DragEvent('dragenter', { bubbles: true, cancelable: true }));
    const data = new DataTransfer();
    data.items.add(new File([Uint8Array.from(atob(dataUri.split(',')[1]), c => c.charCodeAt(0))], 'dropped.png', { type: 'image/png' }));
    data.setData('text/html', '<img src="file:///C:/dropped.png">');
    target.dispatchEvent(new DragEvent('drop', { dataTransfer: data, bubbles: true, cancelable: true }));
  }, dataUri);
}
async function interceptSave(page, kind) {
  let submitted;
  const target = kind === 'article' ? '/Article' : '/Edit/1';
  if (kind === 'article') {
    await page.locator('[name=Title]').fill('Bài viết thử nghiệm Cloudinary');
    await page.locator('[name=ImageUrl]').setInputFiles({ name: 'cover.png', mimeType: 'image/png', buffer: png });
  }
  await page.route('**' + target, async route => {
    if (route.request().method() !== 'POST') return route.continue();
    submitted = route.request().postData();
    await route.fulfill({ status: 200, contentType: 'text/html', body: '<p>Form intercepted. No business data written.</p>' });
  });
  await Promise.all([
    page.waitForResponse(response => new URL(response.url()).pathname === target && response.request().method() === 'POST'),
    page.locator('button[type=submit]').click()
  ]);
  return submitted;
}
try {
  for (const kind of ['article', 'product']) {
    await check(kind + '-file-dialog', async page => {
      await edit(page, kind);
      assert.equal(await page.locator('.note-editor').count(), 1);
      await page.locator('.note-toolbar button').filter({ has: page.locator('.note-icon-picture') }).click();
      await page.locator('.note-image-input').setInputFiles({ name: 'picture.png', mimeType: 'image/png', buffer: png });
      await imagesReady(page, 1);
      assert.match(await interceptSave(page, kind), /res\.cloudinary\.com\/motoshop-fixture\/image\/upload/);
    });
    for (const action of ['paste', 'drop']) await check(kind + '-' + action + '-file', async page => {
      await edit(page, kind);
      await paste(page, `<img src="${dataUri}">`, action, true);
      await imagesReady(page, 1); // Clipboard carrying both HTML and a file must not insert twice.
      assert.equal(await page.locator('.note-editable img').count(), 1);
    });
    await check(kind + '-drop-overlay-upload-and-save', async page => {
      await edit(page, kind);
      const before = (await stats(page)).uploads.length;
      await dropOnOverlay(page);
      await imagesReady(page, 1);
      assert.equal(await page.locator('.note-editor.dragover,.note-dropzone.hover').count(), 0, 'Drop overlay must close after a captured drop.');
      assert.equal(await page.locator('.note-dropzone').isVisible(), false);
      assert.equal(await page.locator('.editor-image-error').count(), 0);
      assert.equal((await page.locator('#editor-error').innerText()).trim(), '');
      assert.equal((await stats(page)).uploads.length - before, 1);
      await page.waitForFunction(() => [...document.querySelectorAll('.note-editable img')].every(image => image.complete && image.naturalWidth > 0));
      await page.locator('.note-editor').screenshot({ path: path.join(output, `${kind}-drop-success.png`) });
      await page.locator('.note-editable').click();
      await page.keyboard.type('Continue after drop');
      await dropOnOverlay(page);
      await imagesReady(page, 2);
      assert.equal(await page.locator('.note-editor.dragover,.note-dropzone.hover').count(), 0);
      assert.equal((await stats(page)).uploads.length - before, 1, 'Repeated file uses the existing successful upload.');
      const body = await interceptSave(page, kind);
      assert.match(body, /res\.cloudinary\.com\/motoshop-fixture\/image\/upload/);
      assert.doesNotMatch(body, /data:image|blob:|file:\/\/\//);
    });
    await check(kind + '-drop-overlay-failure-retry', async page => {
      await edit(page, kind);
      await mode(page, 'fail');
      try {
        await dropOnOverlay(page);
        await page.locator('.editor-image-error').waitFor();
        assert.equal(await page.locator('.note-dropzone').isVisible(), false);
        assert.equal(await page.locator('button[type=submit]').isDisabled(), true);
        await mode(page, 'normal');
        await page.locator('.editor-image-messages button').first().click();
        await imagesReady(page, 1);
        assert.equal(await page.locator('.editor-image-error').count(), 0);
        assert.equal((await page.locator('#editor-error').innerText()).trim(), '');
      } finally { await mode(page, 'normal'); }
    });
    for (const source of ['base64', 'url']) await check(kind + '-paste-html-' + source, async page => {
      await edit(page, kind);
      await paste(page, `<p>Trước ảnh</p><img alt="Ảnh đã dán" width="120" src="${source === 'base64' ? dataUri : 'https://public.invalid/image.png'}"><p>Sau ảnh</p>`);
      await imagesReady(page, 1);
      assert.equal(await page.locator('.note-editable img').getAttribute('alt'), 'Ảnh đã dán');
      assert.equal(await page.locator('.note-editable img').getAttribute('width'), '120');
    });
    await check(kind + '-url-dialog', async page => {
      await edit(page, kind);
      await page.locator('.note-toolbar button').filter({ has: page.locator('.note-icon-picture') }).click();
      await page.locator('.note-image-url').fill('https://public.invalid/dialog.png');
      await page.locator('.note-image-btn').click();
      await imagesReady(page, 1);
    });
    await check(kind + '-codeview-base64-save', async page => {
      await edit(page, kind);
      await page.locator('.note-toolbar button').filter({ has: page.locator('.note-icon-code') }).click();
      await page.locator('.note-codable').fill(`<p>Nội dung codeview</p><img src="${dataUri}">`);
      const body = await interceptSave(page, kind);
      assert.match(body, /res\.cloudinary\.com/);
      assert.doesNotMatch(body, /data:image|blob:/);
    });
    await check(kind + '-legacy-converts-only-on-edit-save', async page => {
      await page.request.get(base + '/_fixture/editor/source/legacy');
      const before = (await stats(page)).uploads.length;
      await page.goto(base + (kind === 'article' ? '/Article/11111111-1111-1111-1111-111111111111' : '/Edit/1'));
      assert.match(await page.locator('.note-editable img').getAttribute('src'), /^data:image/);
      assert.equal((await stats(page)).uploads.length, before);
      // Start an actual edit; the migration queue should now process the old image.
      await page.locator('.note-editable').press('End');
      await page.locator('.note-editable').press('x');
      await imagesReady(page, 1);
      await page.request.get(base + '/_fixture/editor/source/plain');
    });
    await check(kind + '-deduplicates-and-limits-concurrency', async page => {
      await edit(page, kind);
      const before = (await stats(page)).uploads.length;
      await paste(page, `<img src="${dataUri}"><img src="${dataUri}"><img src="https://public.invalid/a.png"><img src="https://public.invalid/b.png">`);
      await imagesReady(page, 4);
      const after = await stats(page);
      assert.equal(after.uploads.length - before, 3);
      assert.ok(after.maximumConcurrent <= 2);
    });
    await check(kind + '-failure-retry-no-base64-fallback', async page => {
      await edit(page, kind);
      await mode(page, 'fail');
      try {
        await paste(page, `<p>Giữ nội dung đang viết</p><img src="${dataUri}">`);
        await page.locator('.editor-image-error').waitFor();
        assert.equal(await page.locator('button[type=submit]').isDisabled(), true);
        assert.match(await page.locator('.note-editable').innerText(), /Giữ nội dung/);
        await mode(page, 'normal');
        await page.getByRole('button', { name: 'Thử lại ảnh lỗi' }).click();
        await imagesReady(page, 1);
      } finally { await mode(page, 'normal'); }
    });
    await check(kind + '-out-of-order-and-deleted-placeholder', async page => {
      await edit(page, kind);
      await paste(page, '<p>Đầu</p><img alt="slow" src="https://public.invalid/slow.png"><img alt="fast" src="https://public.invalid/fast.png"><p>Cuối</p>', 'drop');
      await page.locator('.note-editable').press('End');
      await page.locator('.note-editable').press('x');
      await imagesReady(page, 2);
      assert.deepEqual(await page.locator('.note-editable img').evaluateAll(images => images.map(image => image.alt)), ['slow', 'fast']);
      const uploads = (await stats(page)).uploads;
      for (const name of ['slow', 'fast']) {
        const image = page.locator(`.note-editable img[alt=${name}]`);
        const id = uploads.findLast(upload => upload.source.endsWith('/' + name + '.png')).publicId;
        assert.match(await image.getAttribute('src'), new RegExp(id));
      }
      await paste(page, '<img alt="delete-me" src="https://public.invalid/slow-delete.png">');
      await page.locator('img[alt=delete-me]').evaluate(image => image.remove());
      await page.waitForFunction(() => !document.querySelector('button[type=submit]').disabled);
      assert.equal(await page.locator('img[alt=delete-me]').count(), 0);
    });
    await check(kind + '-undo-redo', async page => {
      await edit(page, kind);
      await paste(page, `<img src="${dataUri}">`);
      await imagesReady(page, 1);
      const url = await page.locator('.note-editable img').getAttribute('src');
      await page.evaluate(() => jQuery('[data-rich-editor]').summernote('undo'));
      await page.evaluate(() => jQuery('[data-rich-editor]').summernote('redo'));
      await imagesReady(page, 1);
      assert.equal(await page.locator('.note-editable img').getAttribute('src'), url);
    });
  }
  await check('legacy-plaintext-product-roundtrip', async page => {
    await page.request.get(base + '/_fixture/editor/source/plain');
    await edit(page, 'product');
    const text = await page.locator('.note-editable').innerText();
    assert.match(text, /<tag> & ký tự đặc biệt\./);
    assert.match(text, /\nDòng thứ hai\./);
    await page.goto(base + '/Product/1');
    assert.match(await page.locator('.plain-description').innerText(), /<tag> &/);
  });
  await check('same-origin-image-import', async page => {
    await edit(page, 'article');
    await paste(page, '<img src="/images/1.png">');
    await imagesReady(page, 1);
    assert.equal((await stats(page)).uploads.at(-1).isFile, true);
  });
  await check('api-admin-validation-and-failures', async page => {
    await edit(page, 'article');
    const headers = { RequestVerificationToken: await token(page) };
    const upload = file => page.request.post(base + '/admin/editor-images/upload', { headers, multipart: { file } });
    assert.equal((await page.request.post(base + '/admin/editor-images/upload', { multipart: { file: { name: 'x.png', mimeType: 'image/png', buffer: png } } })).status(), 400);
    assert.equal((await upload({ name: 'fake.png', mimeType: 'image/png', buffer: Buffer.from('<svg/>') })).status(), 400);
    assert.equal((await upload({ name: 'huge.png', mimeType: 'image/png', buffer: Buffer.alloc(5242881) })).status(), 413);
    for (const url of ['http://127.0.0.1/private.png', 'https://private.invalid/a.png', 'https://user:secret@public.invalid/a.png', 'file:///private/a.png'])
      assert.equal((await page.request.post(base + '/admin/editor-images/import', { headers, data: { url } })).status(), 400);
    const badJson = await page.request.post(base + '/admin/editor-images/import', { headers: { ...headers, 'Content-Type': 'application/json' }, data: '{' });
    assert.equal(badJson.status(), 400); assert.equal(typeof (await badJson.json()).code, 'string');
    await mode(page, 'timeout');
    assert.equal((await upload({ name: 'x.png', mimeType: 'image/png', buffer: png })).status(), 504);
    await mode(page, 'oversize');
    const before = (await stats(page)).deletes.length;
    assert.equal((await upload({ name: 'x.png', mimeType: 'image/png', buffer: png })).status(), 413);
    assert.equal((await stats(page)).deletes.length, before + 1);
    await mode(page, 'normal');
  });
  for (const role of ['guest', 'member']) await check('api-role-' + role, async page => {
    const response = await page.request.post(base + '/admin/editor-images/import', { data: { url: 'https://public.invalid/a.png' } });
    assert.equal(response.status(), role === 'guest' ? 401 : 403);
    assert.equal(typeof (await response.json()).code, 'string');
  }, role);
  await check('missing-config-text-still-works', async page => {
    await page.request.get(base + '/_fixture/cloudinary/config/false');
    try {
      await edit(page, 'article');
      const response = await page.request.post(base + '/admin/editor-images/import', { headers: { RequestVerificationToken: await token(page) }, data: { url: 'https://public.invalid/a.png' } });
      assert.equal(response.status(), 503);
      await page.locator('.note-editable').fill('Bài viết chỉ có chữ.');
      assert.match(await interceptSave(page, 'article'), /Bài viết chỉ có chữ/);
    } finally { await page.request.get(base + '/_fixture/cloudinary/config/true'); }
  });
  await check('direct-post-guards-before-cover-write', async page => {
    await edit(page, 'article');
    const folder = path.join(root, 'tests/MotoShop.UiHarness/.fixture-wwwroot/uploads');
    const files = async () => readdir(folder, { recursive: true }).catch(() => []);
    const before = await files();
    for (const [url, fields] of [['/Article', { Title: 'Giữ tiêu đề', Content: `<p>Giữ nội dung</p><img src="${dataUri}">` }], ['/Add', { Name: 'Giữ tên', Description: `<img src="${dataUri}">` }]]) {
      const response = await page.request.post(base + url, { multipart: { ...fields, ImageUrl: { name: 'never-written.png', mimeType: 'image/png', buffer: png } } });
      assert.equal(response.status(), 200);
      const message = await page.evaluate(html => new DOMParser().parseFromString(html, 'text/html').querySelector('.field-validation-error')?.textContent || '', await response.text());
      assert.match(message, /chưa được upload thành công/);
    }
    assert.deepEqual(await files(), before);
  });
  await check('rate-limit-per-admin', async page => {
    await edit(page, 'article');
    const headers = { RequestVerificationToken: await token(page) };
    for (let i = 0; i < 60; i++) assert.equal((await page.request.post(base + '/admin/editor-images/upload', { headers })).status(), 400);
    const response = await page.request.post(base + '/admin/editor-images/upload', { headers });
    assert.equal(response.status(), 429);
    assert.equal((await response.json()).code, 'rate_limited');
  }, 'admin-' + (identity + 10000));
  await check('responsive-editors', async page => {
    for (const width of [390, 1440]) for (const kind of ['article', 'product']) {
      await page.setViewportSize({ width, height: 1000 });
      await edit(page, kind);
      await paste(page, '<p>Nội dung sau upload</p><img alt="Ảnh nội dung" src="https://public.invalid/review.png">');
      await imagesReady(page, 1);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await page.locator('.note-editor').screenshot({ path: path.join(output, `${kind}-${width}.png`) });
    }
  });
  await check('saved-product-rich-html-rendering', async page => {
    await edit(page, 'product');
    await page.locator('.note-editable').fill('Mô tả đã lưu bằng editor.');
    await paste(page, '<p><strong>Thông tin phụ kiện</strong></p><img alt="Ảnh đã lưu" src="https://public.invalid/saved.png">');
    await imagesReady(page, 1);
    await page.locator('button[type=submit]').click();
    await page.locator('.notice').filter({ hasText: 'Cập nhật sản phẩm thành công' }).waitFor();
    await page.goto(base + '/Product/1');
    assert.match(await page.locator('.rich-description strong').innerText(), /Thông tin phụ kiện/);
    assert.match(await page.locator('.rich-description img').getAttribute('src'), /^https:\/\/res\.cloudinary\.com\/motoshop-fixture\/image\/upload\//);
    await page.request.get(base + '/_fixture/editor/source/plain');
  });
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
  const report = { passed: results.filter(result => result.status === 'PASS').length, failed: results.filter(result => result.status === 'FAIL').length, source: 'TEST ONLY: fake Cloudinary, isolated UI host; no production DB or real provider calls.', realCloudinaryUpload: 'NOT_TESTED_BY_THIS_SCRIPT; see config-fix/live-results.json for a separate live probe.', results, pageErrors: errors };
  await writeFile(path.join(output, filter ? 'ui-results-filtered.json' : 'ui-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
  if (report.failed || errors.length) process.exitCode = 1;
}

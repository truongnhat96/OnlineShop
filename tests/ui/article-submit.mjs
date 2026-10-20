import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'http://127.0.0.1:15188';
assert.equal((await fetch(base + '/Login')).headers.get('x-motoshop-test-fixture'), 'true');
const output = path.join(root, 'docs/ui-review/admin-actions/article-submit');
await mkdir(output, { recursive: true });
const png = await readFile(path.join(root, 'Infrastructure/wwwroot/images/1.png'));
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ locale: 'vi-VN', viewport: { width: 1280, height: 720 } });
await context.route('https://res.cloudinary.com/**', route => route.fulfill({ status: 200, contentType: 'image/png', body: png }));
const page = await context.newPage();
page.setDefaultTimeout(10000);
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const results = [];
let posts = 0;
page.on('request', request => { if (request.method() === 'POST' && new URL(request.url()).pathname.startsWith('/Article')) posts++; });
async function check(name, work) {
  try { await work(); results.push({ name, status: 'PASS' }); console.log('PASS ' + name); }
  catch (error) { results.push({ name, status: 'FAIL', message: error.message }); console.error('FAIL ' + name + ': ' + error.message); await page.screenshot({ path: path.join(output, name + '-failure.png'), fullPage: true }); }
}
const cover = () => page.locator('[name=ImageUrl]').setInputFiles({ name: 'cover.png', mimeType: 'image/png', buffer: png });
async function openNew() {
  await page.goto(base + '/Article');
  await page.locator('.note-editable').waitFor();
  await page.locator('[name=Title]').fill('Article submit regression');
}
async function feedback(pattern) {
  await page.waitForFunction(() => document.querySelector('[data-editor-submit-feedback]')?.textContent.trim());
  assert.match(await page.locator('[data-editor-submit-feedback]').innerText(), pattern);
  assert.equal(await page.locator('#post-save').isEnabled(), true);
}
try {
  await page.goto(base + '/_fixture/identity/admin');
  await check('missing-cover-visible-feedback-no-silent-click', async () => {
    await openNew();
    await page.locator('.note-editable').fill('Article text');
    const before = posts;
    await page.locator('#post-save').click();
    await feedback(/ảnh bìa/i);
    assert.equal(posts, before);
    await cover();
    await page.locator('#post-save').click();
    await page.waitForURL(base + '/BlogList');
    assert.equal(posts, before + 1);
  });
  await check('empty-content-visible-feedback-can-correct-and-save', async () => {
    await openNew(); await cover();
    const before = posts;
    await page.locator('#post-save').click();
    await feedback(/nội dung/i);
    assert.equal(posts, before);
    await page.locator('.note-editable').fill('Corrected article content');
    await page.locator('#post-save').click();
    await page.waitForURL(base + '/BlogList');
    assert.equal(posts, before + 1);
  });
  await check('long-title-jquery-validation-visible-and-recoverable', async () => {
    await openNew(); await cover();
    await page.locator('.note-editable').fill('Article content');
    await page.locator('[name=Title]').evaluate(input => { input.value = 'x'.repeat(101); input.dispatchEvent(new Event('input', { bubbles: true })); });
    const before = posts;
    await page.locator('#post-save').click();
    await feedback(/100/);
    assert.equal(posts, before);
    await page.locator('[name=Title]').fill('Corrected title');
    await page.locator('#post-save').click();
    await page.waitForURL(base + '/BlogList');
    assert.equal(posts, before + 1);
  });
  await check('keyboard-submit-edit-sends-once-and-persists', async () => {
    const response = await page.request.get(base + '/_fixture/admin-state');
    const post = (await response.json()).posts[0];
    await page.goto(base + '/Article/' + post.id);
    await page.locator('.note-editable').fill('Keyboard submit actual content');
    const before = posts;
    await page.locator('#post-save').focus();
    await page.keyboard.press('Enter');
    await page.waitForURL(base + '/BlogList');
    assert.equal(posts, before + 1);
    const saved = await page.request.get(base + '/_fixture/admin-state').then(response => response.json());
    assert.match(saved.posts.find(item => item.id === post.id).content, /Keyboard submit actual content/);
  });
  await check('codeview-unsupported-media-visible-feedback', async () => {
    await openNew(); await cover();
    await page.evaluate(() => jQuery('[data-rich-editor]').summernote('codeview.activate'));
    await page.locator('.note-codable').fill('<p>Keep content</p><iframe src="https://example.invalid"></iframe>');
    const before = posts;
    await page.locator('#post-save').click();
    await feedback(/media|hỗ trợ/i);
    assert.equal(posts, before);
    assert.equal(await page.locator('.note-codable').isEnabled(), true);
  });
  await check('copied-article-css-reset-and-legacy-images-save', async () => {
    const existing = await page.request.get(base + '/_fixture/admin-state').then(response => response.json());
    const post = existing.posts[0];
    const source = 'data:image/png;base64,' + png.toString('base64');
    const html = '<p style="border-image: initial; text-align: justify; background-image: none">Copied article formatting</p>'
      + [1, 2, 3].map(index => `<img alt="Copied image ${index}" src="${source}">`).join('');
    // Set legacy content before editor initialization, like an old article loaded from DB.
    // No editor input/change event: the first save must normalize CSS and all images itself.
    await page.addInitScript(html => document.addEventListener('DOMContentLoaded', () => {
      const textarea = document.querySelector('[data-rich-editor]');
      if (textarea) textarea.value = html;
    }, { once: true }), html);
    await page.goto(base + '/Article/' + post.id);
    assert.equal(await page.locator('.note-editable img[src^="data:"]').count(), 3);
    const before = posts;
    await page.locator('#post-save').click();
    await page.waitForURL(base + '/BlogList');
    assert.equal(posts, before + 1);
    const saved = await page.request.get(base + '/_fixture/admin-state').then(response => response.json());
    const content = saved.posts.find(item => item.id === post.id).content;
    assert.match(content, /Copied article formatting/);
    assert.equal((content.match(/<img/g) || []).length, 3);
    assert.doesNotMatch(content, /border-image|background-image|data:image|blob:/);
  });
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
  const report = { passed: results.filter(item => item.status === 'PASS').length, failed: results.filter(item => item.status === 'FAIL').length, source: 'Real article controllers/services/repositories, isolated SQLite, fixture identity only. No business DB.', results, pageErrors: errors };
  await writeFile(path.join(output, 'results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
  if (report.failed || errors.length) process.exitCode = 1;
}

import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const base = process.env.MOTOSHOP_UI_URL || 'http://127.0.0.1:15187';
const probe = await fetch(base + '/Login');
assert.equal(probe.headers.get('x-motoshop-test-fixture'), 'true', 'Review checks require the isolated UI fixture host.');
const output = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/ui-review/reviews');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = [];
try {
  for (const role of ['guest', 'member']) {
    const context = await browser.newContext({ locale: 'vi-VN' });
    const page = await context.newPage();
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));
    await page.goto(base + '/_fixture/identity/' + role);
    for (const width of [360, 390, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.goto(base + '/Product/1');
      const reviews = page.locator('.product-reviews');
      await reviews.scrollIntoViewIfNeeded();
      assert.equal(await reviews.locator('.review').count(), 2);
      for (const [index, score] of [3, 5].entries()) {
        const review = reviews.locator('.review').nth(index);
        assert.equal(await review.locator('.review-star').count(), 5);
        assert.equal(await review.locator('.review-star.is-filled').count(), score);
        assert.equal(await review.locator('.review-stars').getAttribute('aria-label'), `${score} trên 5 sao`);
        assert.equal(await review.locator('.review-avatar').evaluate(image => image.complete && image.naturalWidth > 0), true);
      }
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      if (role === 'member') {
        assert.equal(await page.getByRole('radio', { name: '5 sao', exact: true }).isChecked(), true);
        await page.locator('.star-choice[title="3 sao"]').click();
        const radio = page.getByRole('radio', { name: '3 sao', exact: true });
        assert.equal(await radio.isChecked(), true);
        await radio.focus();
        await radio.press('ArrowRight');
        assert.equal(await page.getByRole('radio', { name: '4 sao', exact: true }).isChecked(), true);
        await page.mouse.move(0, 0);
        assert.equal(await page.locator('.star-choice .picker-star').evaluateAll(stars => stars.filter(star => getComputedStyle(star).fill !== 'none').length), 4);
        await page.locator('.star-choice[title="1 sao"]').hover();
        assert.equal(await page.locator('.star-choice .picker-star').evaluateAll(stars => stars.filter(star => getComputedStyle(star).fill !== 'none').length), 1);
        assert.equal(await page.getByRole('radio', { name: '4 sao', exact: true }).isChecked(), true);
        await page.mouse.move(0, 0);
      } else {
        assert.equal(await reviews.locator('.review-form').count(), 0);
        assert.equal(await reviews.getByRole('link', { name: 'Đăng nhập để đánh giá' }).count(), 1);
      }
      if (width === 390 || width === 1440) await reviews.screenshot({ path: path.join(output, `${role}-${width}.png`) });
      results.push({ name: `${role}-${width}`, status: 'PASS' });
    }
    if (role === 'member') {
      let submitted;
      await page.route('**/User/Comment', route => {
        submitted = { method: route.request().method(), data: new URLSearchParams(route.request().postData()) };
        return route.fulfill({ status: 200, contentType: 'text/plain', body: 'Intercepted UI form check; no review saved.' });
      });
      await page.locator('.star-choice[title="2 sao"]').click();
      await page.locator('[name="comment"]').fill('Kiểm tra dữ liệu biểu mẫu đánh giá.');
      await Promise.all([page.waitForURL('**/User/Comment'), page.getByRole('button', { name: 'Gửi đánh giá' }).click()]);
      assert.equal(submitted.method, 'POST');
      assert.equal(submitted.data.get('rating'), '2');
      assert.equal(submitted.data.get('productId'), '1');
      assert.equal(submitted.data.get('comment'), 'Kiểm tra dữ liệu biểu mẫu đánh giá.');
      results.push({ name: 'original-review-form-contract', status: 'PASS', note: 'Request intercepted; no data saved.' });
      await page.goto(base + '/Product/2');
      assert.equal(await page.locator('.reviews-empty').count(), 1);
      assert.equal(await page.locator('.review-list .review').count(), 0);
      assert.equal(await page.getByRole('radio').count(), 5);
      results.push({ name: 'empty-reviews', status: 'PASS' });
    }
    assert.deepEqual(pageErrors, []);
    await context.close();
  }
  const report = { passed: results.length, failed: 0, source: 'Isolated UI fixtures; no real DB writes or real-account login.', results };
  await writeFile(path.join(output, 'results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report));
} finally {
  await browser.close();
}

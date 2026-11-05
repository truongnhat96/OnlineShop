import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';

const base = process.env.PERSONALIZATION_BASE || 'http://127.0.0.1:15188';
const realSql = process.env.PERSONALIZATION_REAL_SQL === '1';
const mainSql = process.env.PERSONALIZATION_MAIN === '1';
let helmetId = 60, secondHelmetId = 61, careId = 54;
const dir = '../../docs/ui-review/personalization';
await mkdir(dir, {recursive:true});
const accountFile = mainSql ? `${dir}/main-test-accounts-${Date.now()}.json` : null;
const testAccounts = [];
if (accountFile) await writeFile(accountFile, JSON.stringify(testAccounts), {flag:'wx'});
const browser = await chromium.launch({channel:'msedge',headless:true});
try {
  const page = await browser.newPage({viewport:{width:390,height:900}});
  await page.goto(base + '/');
  await page.locator('[data-personalization-enable]').waitFor();
  if (realSql) {
    const find = async query => {
      const response = await page.request.post(base + '/api/recommendations', {data:{query,limit:6}});
      assert.ok(response.ok()); return (await response.json()).items;
    };
    const helmets = (await find('mũ bảo hiểm')).filter(item => /mũ bảo hiểm/i.test(item.name));
    const care = await find('dưỡng sên');
    assert.ok(helmets.length >= 2 && care.length >= 1, 'Catalog lacks test products');
    helmetId=helmets[0].id; secondHelmetId=helmets[1].id; careId=care[0].id;
  }
  assert.equal((await (await page.request.get(base + '/api/recommendations/preferences')).json()).chosen, false);
  assert.equal((await (await page.request.get(base + '/api/recommendations/personalized')).json()).mode, 'Explore');
  assert.equal((await page.request.post(base + '/api/recommendations/events', {data:{ticket:'invalid'}})).status(), 400);
  await page.locator('[data-personalization-enable]').click();
  await page.locator('[data-personalization-disable]').waitFor();
  await page.goto(base + '/Product/' + helmetId);
  await page.waitForTimeout(9500);
  const personal = await (await page.request.get(base + '/api/recommendations/personalized')).json();
  assert.equal(personal.mode, 'Personalized');
  assert.ok(personal.items.some(item => /mũ bảo hiểm/i.test(item.name)));
  await page.goto(base + '/Recommendations');
  await page.locator('#rec-title').getByText('Dành cho bạn').waitFor();
  await page.screenshot({path:dir + '/fixture-mobile.png',fullPage:true});
  await page.locator('summary').filter({hasText:'Điều chỉnh nhu cầu'}).click();
  await page.locator('#rec-vehicle option').nth(1).waitFor({state:'attached'});
  await page.locator('#rec-vehicle').selectOption({index:1});
  await page.locator('#rec-year').fill('2020');
  await page.locator('#rec-save-vehicle').click();
  await page.locator('#rec-vehicle-status').getByText('Đã lưu xe đã xác nhận.').waitFor();
  assert.ok((await (await page.request.get(base + '/api/recommendations/preferences')).json()).vehicleModelId);
  await page.locator('[data-personalization-disable]').click();
  await page.locator('[data-personalization-enable]').waitFor();
  assert.equal((await (await page.request.get(base + '/api/recommendations/personalized')).json()).mode, 'Explore');
  await page.locator('[data-personalization-enable]').click();
  await page.locator('[data-personalization-clear]').waitFor();
  page.once('dialog', dialog => dialog.accept());
  await Promise.all([page.waitForResponse(r => r.url().endsWith('/api/recommendations/history/delete') && r.ok()), page.locator('[data-personalization-clear]').click()]);
  await page.waitForLoadState('networkidle');
  assert.equal((await (await page.request.get(base + '/api/recommendations/personalized')).json()).mode, 'Explore');
  const cleared = await (await page.request.get(base + '/api/recommendations/preferences')).json();
  assert.equal(cleared.enabled, true); assert.equal(cleared.vehicleModelId, null);
  await page.goto(base + '/Search/page/2?keyword=' + encodeURIComponent('mũ bảo hiểm'));
  assert.equal((await (await page.request.get(base + '/api/recommendations/personalized')).json()).mode,'Explore');
  await page.goto(base + '/Search?keyword=' + encodeURIComponent('mũ bảo hiểm'));
  assert.equal((await (await page.request.get(base + '/api/recommendations/personalized')).json()).mode,'Personalized');
  await page.goto(base + '/Recommendations');
  await page.locator('[data-personalization-clear]').waitFor();
  page.once('dialog', dialog => dialog.accept());
  await Promise.all([page.waitForResponse(r => r.url().endsWith('/api/recommendations/history/delete') && r.ok()), page.locator('[data-personalization-clear]').click()]);
  await page.waitForLoadState('networkidle');
  await page.goto(base + '/Cart-Add/' + secondHelmetId + '/test?quantity=1');
  const fromCart = await (await page.request.get(base + '/api/recommendations/personalized')).json();
  assert.equal(fromCart.mode,'Personalized');
  assert.ok(!fromCart.items.some(item => item.id === secondHelmetId));
  for (const width of [360,390,768,1024,1440]) {
    await page.setViewportSize({width,height:900});
    await page.goto(base + '/');
    await page.locator('[data-home-results] .product-card').first().waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1),true, `overflow at ${width}`);
    await page.screenshot({path:`${dir}/${mainSql ? 'main' : realSql ? 'sql-test' : 'fixture'}-${width}.png`,fullPage:true});
  }
  const loginMember = async number => {
    if (!realSql) { await page.goto(base + '/_fixture/identity/member-' + number); return null; }
    const username = `PersonalTest${Date.now()}${number}`;
    if (accountFile) { testAccounts.push(username); await writeFile(accountFile, JSON.stringify(testAccounts)); }
    const password = 'TestOnly' + Math.random().toString(36).slice(2);
    await page.goto(base + '/Signup');
    await page.locator('#Username').fill(username); await page.locator('#DisplayName').fill('Test');
    await page.locator('#Email').fill(username + '@example.invalid'); await page.locator('#Password').fill(password);
    await page.locator('#ConfirmPassword').fill(password); await page.locator('form[action="/Signup"] button[type=submit]').click();
    await page.waitForURL('**/Login');
    await page.locator('#Username').fill(username); await page.locator('#Password').fill(password);
    await page.locator('form[action="/Login"] button[type=submit]').click(); await page.waitForURL(base + '/');
    return {username, password};
  };
  const firstCredentials = await loginMember(2);
  await page.locator('[data-personalization-enable]').click();
  await page.locator('[data-personalization-disable]').waitFor();
  await page.goto(base + '/Product/' + helmetId); await page.waitForTimeout(9500);
  const firstUser = await (await page.request.get(base + '/api/recommendations/personalized')).json();
  assert.equal(firstUser.mode, 'Personalized');
  for (const width of [360,390,768,1024,1440]) {
    await page.setViewportSize({width,height:900});
    await page.goto(base + '/Recommendations');
    await page.locator('#rec-title').waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1),true, `member overflow at ${width}`);
    await page.screenshot({path:`${dir}/${mainSql ? 'main' : realSql ? 'sql-test' : 'fixture'}-member-${width}.png`,fullPage:true});
  }
  if (realSql) await page.goto(base + '/Logout');
  await loginMember(3);
  assert.equal((await (await page.request.get(base + '/api/recommendations/preferences')).json()).enabled,false);
  await page.locator('[data-personalization-enable]').click();
  await page.locator('[data-personalization-disable]').waitFor();
  await page.goto(base + '/Product/' + careId); await page.waitForTimeout(9500);
  const secondUser = await (await page.request.get(base + '/api/recommendations/personalized')).json();
  assert.equal(secondUser.mode,'Personalized');
  assert.notEqual(firstUser.items[0].id,secondUser.items[0].id);
  const clearAccount = async () => {
    await page.goto(base + '/Recommendations');
    await page.locator('[data-personalization-clear]').waitFor();
    page.once('dialog', dialog => dialog.accept());
    await Promise.all([page.waitForResponse(r => r.url().endsWith('/api/recommendations/history/delete') && r.ok()), page.locator('[data-personalization-clear]').click()]);
    await page.locator('[data-personalization-disable]').click();
    await page.locator('[data-personalization-enable]').waitFor();
    await page.waitForLoadState('networkidle');
  };
  await clearAccount();
  if (realSql) {
    await page.goto(base + '/Logout'); await page.goto(base + '/Login');
    await page.locator('#Username').fill(firstCredentials.username); await page.locator('#Password').fill(firstCredentials.password);
    await page.locator('form[action="/Login"] button[type=submit]').click(); await page.waitForURL(base + '/');
  } else await page.goto(base + '/_fixture/identity/member-2');
  await clearAccount();
  if (!realSql) {
    await page.goto(base + '/_fixture/identity/admin');
    assert.equal((await (await page.request.get(base + '/api/recommendations/preferences')).json()).enabled,false);
    for (const width of [360,390,768,1024,1440]) {
      await page.setViewportSize({width,height:900});
      await page.goto(base + '/Recommendations');
      await page.locator('#rec-title').waitFor();
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1),true, `admin overflow at ${width}`);
      await page.screenshot({path:`${dir}/fixture-admin-${width}.png`,fullPage:true});
    }
  }
  await page.close();
  console.log('PASS: guest/member isolation, consent, view, ranking, vehicle, opt-out, deletion and five viewport sizes.');
  if (accountFile) console.log('Test account list: ' + accountFile);
} finally { await browser.close(); }

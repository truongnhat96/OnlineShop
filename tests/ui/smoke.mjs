import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(root, 'docs/ui-review');
const base = process.env.MOTOSHOP_UI_URL || 'http://127.0.0.1:15187';
const fixtureProbe = await fetch(base + '/Login');
if (fixtureProbe.headers.get('x-motoshop-test-fixture') !== 'true')
  throw new Error('Refusing UI fixture tests against the real application. Start the test-only host with --ui-fixtures on port 15187.');
await mkdir(path.join(output, 'screenshots'), { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const interactionsOnly = process.env.MOTOSHOP_UI_INTERACTIONS_ONLY === '1';
const previous = interactionsOnly ? JSON.parse(await readFile(path.join(output,'results.json'),'utf8')) : null;
const results = previous ? previous.results.filter(result => /-(360|390|768|1024|1440)$/.test(result.name)) : [];
const errors = [];
const contexts = new Map();
const postId = '11111111-1111-1111-1111-111111111111';
const widths = [360, 390, 768, 1024, 1440];
const targets = [
  ['guest','home','/'], ['guest','search','/Search?keyword=Mũ'], ['guest','search-empty','/Search?keyword=no-fixture-match'],
  ['guest','category','/Category/1'], ['guest','detail','/Product/1'], ['guest','out-of-stock','/Product/2'],
  ['guest','cart-empty','/_fixture/cart/empty'], ['guest','cart-filled','/_fixture/cart/filled'],
  ['guest','login','/Login'], ['guest','signup','/Signup'], ['guest','forgot','/ForgotPassword'],
  ['guest','confirm','/_fixture/view/confirm'], ['guest','forbidden','/Forbidden'],
  ['guest','checkout','/Pay/0'], ['guest','order','/_fixture/view/order'],
  ['guest','vnpay-success','/PaymentCallbackVnpay?fixture=success'], ['guest','vnpay-fail','/PaymentCallbackVnpay?fixture=fail'], ['guest','vnpay-missing','/PaymentCallbackVnpay?fixture=malformed'],
  ['guest','momo-success','/PaymentCallbackMomo?errorCode=0&transId=DEMO-TXN'], ['guest','momo-fail','/PaymentCallbackMomo?fixture=fail'], ['guest','momo-missing','/PaymentCallbackMomo?fixture=malformed'],
  ['guest','qr-dormant','/_fixture/view/qr'], ['guest','privacy','/Home/Privacy'], ['guest','error','/_fixture/view/error'],
  ['guest','posts','/Posts'], ['guest','post','/Post/'+postId], ['guest','sales-public','/api/AHP'],
  ['admin','manage','/Manage'], ['admin','products','/Product-List'], ['admin','edit','/Edit/1'],
  ['admin','blog-list','/BlogList'], ['admin','blog-new','/Article'], ['admin','blog-edit','/Article/'+postId],
  ['admin','sales-admin','/api/AHP'],
  ['member','member-cart','/Cart'], ['member','member-checkout','/Pay/1'], ['member','member-detail','/Product/1']
];
async function contextFor(role) {
  if (contexts.has(role)) return contexts.get(role);
  const context = await browser.newContext({ locale:'vi-VN', viewport:{width:1440,height:1000} });
  await context.route('**/*', route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
  const login = await context.newPage();
  await login.goto(base+'/_fixture/identity/'+role);
  await login.close();
  contexts.set(role, context);
  return context;
}
async function check(name, action) {
  try { await action(); results.push({ name, status:'PASS' }); }
  catch (error) { results.push({ name, status:'FAIL', message:error.message }); errors.push(name+': '+error.message); }
}
try {
  for (const [role, name, route] of interactionsOnly ? [] : targets) {
    const context = await contextFor(role);
    const page = await context.newPage();
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));
    for (const width of widths) {
      await check(name+'-'+width, async () => {
        await page.setViewportSize({width,height:1000});
        const response = await page.goto(base+route, {waitUntil:'networkidle'});
        assert.equal(response.status(),200,'HTTP status');
        assert.equal(response.headers()['x-motoshop-test-fixture'],'true','Must use isolated fixture host');
        assert.equal(await page.locator('#main-content').count(),1,'Main landmark');
        assert.equal(await page.locator('html').getAttribute('lang'),'vi');
        assert.match(await page.title(),/MotoShop/);
        assert.equal(await page.locator('#chatbot, #chat-container, .chat-widget').count(),0,'Chatbot stays hidden');
        assert.deepEqual(pageErrors,[],'No page script errors');
        await page.locator('img[loading="lazy"]').evaluateAll(images => images.forEach(image => image.loading = 'eager'));
        await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0), {timeout:5000});
        const dimensions = await page.evaluate(() => ({width:document.documentElement.clientWidth, scroll:document.documentElement.scrollWidth}));
        assert.ok(dimensions.scroll <= dimensions.width + 1,'Horizontal page overflow: '+JSON.stringify(dimensions));
        assert.equal(await page.locator('img').evaluateAll(images => images.filter(image => !image.complete || image.naturalWidth === 0).length),0,'Images render/fallback');
        if (name === 'out-of-stock') assert.equal(await page.locator('.buy-form button').isDisabled(),true);
        if (name.endsWith('missing')) assert.ok((await page.locator('body').innerText()).includes('Chưa có'));
        if (name === 'qr-dormant') assert.equal(await page.locator('form').count(),1,'Only global search; no QR operations');
        await page.screenshot({path:path.join(output,'screenshots',name+'-'+width+'.png'),fullPage:true});
      });
    }
    await page.close();
  }
  const admin = await contextFor('admin');
  const page = await admin.newPage();
  await check('admin-search-stock-sort-pagination',async()=>{
    await page.goto(base+'/Product-List');
    assert.equal(await page.locator('[data-row]:visible').count(),20);
    await page.locator('[data-table-next]').click();
    assert.equal(await page.locator('[data-row]:visible').count(),6);
    await page.locator('[data-table-stock]').selectOption('out');
    assert.equal(await page.locator('[data-row]:visible').count(),1);
    await page.locator('[data-table-stock]').selectOption('');
    await page.locator('[data-table-search]').fill('mu bao hiem');
    assert.equal(await page.locator('[data-row]:visible').count(),1);
    await page.locator('[data-table-search]').fill('');
    await page.locator('[data-table-sort]').selectOption('price');
    assert.match(await page.locator('[data-row]:visible').first().innerText(),/Mũ bảo hiểm/);
  });
  await check('delete-modal-cancel-and-keyboard',async()=>{
    const shown = page.evaluate(() => new Promise(resolve => document.getElementById('confirm-delete').addEventListener('shown.bs.modal',resolve,{once:true})));
    await page.locator('[data-confirm-delete]').first().click();
    await shown;
    assert.match(await page.locator('#confirm-delete-text').innerText(),/Mũ bảo hiểm/);
    await page.keyboard.press('Escape');
    await page.locator('#confirm-delete').waitFor({state:'hidden'});
    assert.equal(await page.locator('[data-row]').count(),26);
  });
  await check('admin-mobile-drawer',async()=>{
    await page.goto(base+'/Product-List');
    await page.setViewportSize({width:390,height:844});
    assert.equal(await page.locator('#admin-sidebar').isVisible(),false,'Closed sidebar is hidden from keyboard/screen readers');
    await page.locator('.admin-menu-trigger').click();
    assert.equal(await page.locator('.admin-menu-trigger').getAttribute('aria-expanded'),'true');
    await page.keyboard.press('Escape');
    assert.equal(await page.locator('.admin-menu-trigger').getAttribute('aria-expanded'),'false');
    await page.setViewportSize({width:1440,height:1000});
  });
  await check('original-product-form-contract',async()=>{
    await page.goto(base+'/Manage');
    assert.equal(await page.locator('form[action="/Add"]').count(),1);
    assert.equal(await page.locator('[name=Price]').getAttribute('min'),null);
    assert.equal(await page.locator('[name=Quantity]').getAttribute('min'),null);
    assert.equal(await page.locator('[name=CategoryId] option').count(),3);
  });
  await check('product-image-preview',async()=>{
    await page.goto(base+'/Manage');
    await page.locator('input[data-image-preview="product-image-preview"]').setInputFiles(path.join(root,'Infrastructure/wwwroot/images/motoshop-logo.svg'));
    assert.match(await page.locator('#product-image-preview').getAttribute('src'),/^blob:/);
  });
  await check('edit-form-fields-and-category',async()=>{
    await page.goto(base+'/Edit/1');
    assert.equal(await page.locator('[name=CategoryId]').inputValue(),'2');
    assert.equal(await page.locator('[name=Image]').count(),1);
    assert.equal(await page.locator('a[href*="EditAccountInfor"]').count(),0);
    assert.equal(await page.locator('[name=Sold]').isEditable(),false);
  });
  await check('blog-search-and-paging',async()=>{
    await page.goto(base+'/BlogList');
    assert.equal(await page.locator('[data-row]:visible').count(),20);
    await page.locator('[data-table-next]').click();
    assert.equal(await page.locator('[data-row]:visible').count(),5);
    await page.locator('[data-table-search]').fill('phu kien 01');
    assert.equal(await page.locator('[data-row]:visible').count(),1);
  });
  await check('summernote-preview-and-empty-validation',async()=>{
    await page.goto(base+'/Article');
    assert.equal(await page.locator('.note-editor').count(),1);
    await page.locator('input[name=ImageUrl]').setInputFiles(path.join(root,'Infrastructure/wwwroot/images/motoshop-logo.svg'));
    assert.match(await page.locator('#post-image-preview').getAttribute('src'),/^blob:/);
    await page.locator('[name=Title]').fill('Bài viết mẫu');
    await page.locator('#post-save').click();
    assert.match(await page.locator('#editor-error').innerText(),/nhập nội dung/);
  });
  const guest = await contextFor('guest');
  const customer = await guest.newPage();
  await check('guest-cart-update-request-and-remove',async()=>{
    await customer.goto(base+'/_fixture/cart/filled');
    await customer.locator('[data-cart-quantity]').fill('2');
    const request=customer.waitForRequest(r=>r.url().endsWith('/Cart-Update')&&r.method()==='POST');
    const updated=customer.waitForNavigation({waitUntil:'networkidle'});
    await customer.locator('#cart-update').click();
    assert.deepEqual((await request).postDataJSON(),{UpdateItem:[{ProductId:1,Quantity:2}]});
    await updated;
    assert.equal(await customer.locator('[data-cart-quantity]').inputValue(),'2');
    const removed=customer.waitForNavigation({waitUntil:'networkidle'});
    await customer.locator('.remove-item').click();
    await removed;
    assert.match(await customer.locator('body').innerText(),/Giỏ hàng đang trống/);
  });
  await check('guest-add-to-cart-and-checkout-contract',async()=>{
    await customer.goto(base+'/Product/1');
    await customer.locator('[name=quantity]').fill('2');
    await customer.locator('.buy-form button').click();
    await customer.waitForURL(base+'/Cart');
    assert.equal(await customer.locator('[data-cart-quantity]').inputValue(),'2');
    await customer.locator('a[href="/Pay/0"]').click();
    assert.equal(await customer.locator('#checkout-form').getAttribute('action'),'/CF-Order');
    assert.equal(await customer.locator('#checkout-form [name=name],#checkout-form [name=phone],#checkout-form [name=email],#checkout-form [name=note],#checkout-form [name=totalAmount]').count(),5);
    assert.equal(await customer.locator('form[action="/Payment/PayByMomo"]').count(),0);
  });
  await check('catalog-query-preserved',async()=>{
    await customer.goto(base+'/Category/1?keyword=Phụ&min_price=100&max_price=999999&orderby=Default');
    assert.equal(await customer.locator('#category-keyword').inputValue(),'Phụ');
    const href=await customer.locator('.pagination a').first().getAttribute('href');
    assert.ok(href.includes('keyword=')&&href.includes('min_price=100')&&href.includes('max_price=999999')&&href.includes('orderby=Default'));
  });
  await check('mobile-catalog-filter-and-store-menu',async()=>{
    await customer.setViewportSize({width:390,height:844});
    await customer.goto(base+'/Category/1');
    await customer.locator('[data-filter-toggle]').click();
    assert.equal(await customer.locator('#catalog-filters').isVisible(),true);
    const shown = customer.evaluate(() => new Promise(resolve => document.getElementById('store-menu').addEventListener('shown.bs.offcanvas',resolve,{once:true})));
    await customer.locator('.menu-trigger').click();
    await shown;
    await customer.keyboard.press('Escape');
    await customer.locator('#store-menu').waitFor({state:'hidden'});
  });
  await check('auth-password-toggle-and-invalid-login',async()=>{
    await customer.goto(base+'/Login');
    await customer.locator('[data-password-toggle]').click();
    assert.equal(await customer.locator('[name=Password]').getAttribute('type'),'text');
    await customer.locator('[name=Username]').fill('invalid');
    await customer.locator('[name=Password]').fill('invalid');
    await customer.locator('button[type=submit]').click();
    assert.equal(await customer.locator('[name=Username]').inputValue(),'invalid');
    assert.match(await customer.locator('.validation-summary-errors').innerText(),/không chính xác/);
  });
  await check('guest-admin-access-remains-protected',async()=>{
    await customer.goto(base+'/Manage');
    assert.match(customer.url(),/\/Login/);
  });
  const member = await contextFor('member');
  const memberPage=await member.newPage();
  await check('member-admin-access-forbidden',async()=>{
    await memberPage.goto(base+'/Manage');
    assert.match(memberPage.url(),/\/Forbidden/);
  });
  await check('member-payment-forms-contract',async()=>{
    await memberPage.goto(base+'/Pay/1');
    assert.equal(await memberPage.locator('form[action="/Payment/PayByMomo"] [name=FullName],form[action="/Payment/PayByMomo"] [name=Amount],form[action="/Payment/PayByMomo"] [name=OrderInfo]').count(),3);
    assert.equal(await memberPage.locator('form[action="/Payment/PayByVnpay"] [name=Name],form[action="/Payment/PayByVnpay"] [name=Amount],form[action="/Payment/PayByVnpay"] [name=OrderDescription],form[action="/Payment/PayByVnpay"] [name=OrderType]').count(),4);
  });
  await check('original-cart-checkout-navigation',async()=>{
    await customer.goto(base+'/_fixture/cart/filled');
    await customer.locator('[data-cart-quantity]').fill('999');
    assert.equal(await customer.locator('#cart-update').isDisabled(),false);
    assert.equal(await customer.locator('[data-checkout-link]').getAttribute('aria-disabled'),null);
    await customer.locator('[data-cart-quantity]').fill('2');
    assert.equal(await customer.locator('#cart-update').isDisabled(),false);
    assert.equal(await customer.locator('[data-checkout-link]').getAttribute('href'),'/Pay/0');
  });
  await check('member-coupon-error-retains-input',async()=>{
    await memberPage.goto(base+'/Cart');
    await memberPage.locator('[name=coupon]').fill('INVALID-DEMO');
    await memberPage.locator('form[action="/Discount"] button').click();
    await memberPage.locator('.notice-error').waitFor();
    assert.match(await memberPage.locator('.notice-error').innerText(),/không tồn tại|đã được sử dụng/);
    assert.equal(await memberPage.locator('[name=coupon]').inputValue(),'INVALID-DEMO');
  });
  await check('cart-failed-update-shows-retry',async()=>{
    await customer.route('**/Cart-Update',route=>route.fulfill({status:500,contentType:'application/json',body:'{}'}));
    await customer.locator('#cart-update').click();
    await customer.locator('#cart-update-error').filter({hasText:'Chưa cập nhật'}).waitFor();
    assert.equal(await customer.locator('#cart-update').isDisabled(),false);
    await customer.unroute('**/Cart-Update');
  });
  await check('valid-login-and-forgot-confirm',async()=>{
    await customer.goto(base+'/Login');
    await customer.locator('[name=Username]').fill('fixture');
    await customer.locator('[name=Password]').fill('fixture-only');
    await customer.locator('[name=IsRememberLoginInfor][type=checkbox]').check();
    await customer.locator('button[type=submit]').click();
    await customer.waitForURL(base+'/');
    await customer.goto(base+'/ForgotPassword');
    await customer.locator('[name=UsernameOrEmail]').fill('fixture@example.invalid');
    await customer.locator('button[type=submit]').click();
    await customer.locator('h2').filter({hasText:'Kiểm tra email'}).waitFor();
    assert.match(await customer.locator('.auth-card').innerText(),/fixture@example.invalid/);
  });
  await check('product-create-upload-and-edit-submit',async()=>{
    await page.goto(base+'/Manage');
    await page.locator('[name=Name]').fill('Sản phẩm submit · DEMO');
    await page.locator('[name=Brand]').fill('Thương hiệu thử');
    await page.locator('[name=CategoryId]').selectOption('1');
    await page.locator('[name=Price]').fill('125000');
    await page.locator('[name=Quantity]').fill('6');
    await page.locator('.note-editable').fill('Mô tả thử nghiệm.');
    await page.locator('input[name=ImageUrl]').setInputFiles(path.join(root,'Infrastructure/wwwroot/images/motoshop-logo.svg'));
    await page.locator('button[type=submit]').click();
    await page.locator('.notice').filter({hasText:'Thêm sản phẩm thành công'}).waitFor();
    await page.goto(base+'/Edit/27');
    assert.equal(await page.locator('[name=Name]').inputValue(),'Sản phẩm submit · DEMO');
    const existingImage=await page.locator('#product-image-preview').getAttribute('src');
    assert.match(existingImage,/uploads\/products\/.*\.svg/);
    await page.locator('[name=Name]').fill('Sản phẩm sửa · DEMO');
    await page.locator('button[type=submit]').click();
    await page.locator('.notice').filter({hasText:'Cập nhật sản phẩm thành công'}).waitFor();
    assert.equal(await page.locator('[name=Name]').inputValue(),'Sản phẩm sửa · DEMO');
    assert.equal(await page.locator('[name=CategoryId]').inputValue(),'1');
    assert.equal(await page.locator('#product-image-preview').getAttribute('src'),existingImage);
  });
  await check('blog-edit-original-fields',async()=>{
    await page.goto(base+'/Article/'+postId);
    assert.equal(await page.locator('[name=Image]').count(),0);
    await page.evaluate(()=>jQuery('#post-content').summernote('code','<p>Nội dung chỉnh sửa thử nghiệm.</p>'));
    await page.locator('button[type=submit]').click();
    await page.locator('.notice').filter({hasText:'Cập nhật bài viết thành công'}).waitFor();
  });
  await check('blog-create-original-handler',async()=>{
    await page.goto(base+'/Article');
    await page.locator('[name=Title]').fill('Bài viết submit · DEMO');
    await page.evaluate(()=>jQuery('#post-content').summernote('code','<p>Nội dung bài viết mẫu.</p>'));
    await page.locator('input[name=ImageUrl]').setInputFiles(path.join(root,'Infrastructure/wwwroot/images/motoshop-logo.svg'));
    await page.locator('button[type=submit]').click();
    await page.locator('.notice').filter({hasText:'Đăng bài viết thành công'}).waitFor();
  });
} finally {
  await browser.close();
  const summary={fixture:true,base,widths,interactionRun:interactionsOnly,passed:results.filter(r=>r.status==='PASS').length,failed:results.filter(r=>r.status==='FAIL').length,results};
  await writeFile(path.join(output,'results.json'),JSON.stringify(summary,null,2));
  console.log(JSON.stringify({passed:summary.passed,failed:summary.failed,report:'docs/ui-review/results.json'}));
  if(errors.length) { console.error(errors.join('\n')); process.exitCode=1; }
}

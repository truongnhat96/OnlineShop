import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const output=path.join(root,'docs/ui-review/real-runtime');
const base=process.env.MOTOSHOP_REAL_URL || 'https://localhost:7042';
const results=[];
await mkdir(output,{recursive:true});
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
  const page=await browser.newPage({viewport:{width:1440,height:1000},locale:'vi-VN'});
  const errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  for(const [name,route] of [['home','/'],['login','/Login'],['product-1','/Product/1']]) {
    const response=await page.goto(base+route,{waitUntil:'networkidle'});
    assert.equal(response.status(),200);
    assert.equal(response.headers()['x-motoshop-test-fixture'],undefined);
    if(name==='product-1') assert.match(await page.locator('h1').innerText(),/Office 365/i);
    if(name==='home') {
      assert.ok(await page.locator('.product-card').count()>0);
      assert.equal((await page.locator('.product-card').allTextContents()).some(text=>text.includes('DEMO')),false);
    }
    await page.screenshot({path:path.join(output,name+'.png'),fullPage:true});
    results.push({name,status:'PASS',source:'real application / existing SQL Server'});
  }
  const fixture=await page.evaluate(async url=>{
    const response=await fetch(url,{redirect:'manual'});
    return {status:response.status,url:response.url};
  },base+'/_fixture/identity/admin');
  assert.equal(fixture.status,404);
  assert.equal(fixture.url,base+'/_fixture/identity/admin');
  results.push({name:'no-fixture-auth-endpoint',status:'PASS'});
  await page.goto(base+'/Manage');
  assert.match(page.url(),/\/Login/);
  results.push({name:'admin-requires-real-auth',status:'PASS'});
  await page.locator('[name=Username]').fill('runtime-check-'+randomUUID());
  await page.locator('[name=Password]').fill(randomUUID());
  await page.locator('button[type=submit]').click();
  await page.locator('.validation-summary-errors').waitFor();
  assert.match(await page.locator('.validation-summary-errors').innerText(),/không chính xác/);
  results.push({name:'invalid-login-real-handler',status:'PASS'});
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({passed:results.length,failed:0,base,source:'real SQL Server',successfulLogin:'NOT_RUN: no user credentials requested',results}));
  await writeFile(path.join(output,'results.json'),JSON.stringify({passed:results.length,failed:0,base,source:'real SQL Server',successfulLogin:'NOT_RUN: no user credentials requested',results},null,2));
} finally { await browser.close(); }

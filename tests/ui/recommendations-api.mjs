import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';

const base = process.env.REC_BASE || 'http://127.0.0.1:15201';
const manifest = JSON.parse(await readFile('../../Infrastructure.SqlServer/Seed/recommendations.json', 'utf8'));
const options = await (await fetch(base + '/api/recommendations/options')).json();
const byName = new Map(manifest.products.map(p => [p.name, p]));
const helmet = ['M35.1 SCUDO II', 'V33', 'HX36F VERTIGO', 'D-VISOR M30.3 LCR', 'STRADA 2 V20.2', 'D-VISOR M30.3', 'M10.2 VISTA'];
const rain = ['ARN04', 'ARN03', 'ARS4T', 'RRS08'];
const cases = [
 ['mũ có dấu','mũ bảo hiểm',helmet],['mũ không dấu','mu bao hiem',helmet],['nón','bao hiem',helmet],
 ['áo mưa','áo mưa',rain],['áo mưa không dấu','ao mua',rain],['bộ đồ mưa','quần áo mưa',rain],
 ['dưỡng sên','dưỡng sên',['C2','C3']],['dưỡng sên không dấu','duong sen',['C2','C3']],
 ['vệ sinh mũ','vệ sinh mũ',['M1','M2']],['vệ sinh mũ không dấu','ve sinh mu',['M1','M2']],
 ['Forza kính','kính chắn gió',['D1166ST'],'forza',2019],['Forza tay lái','chắn gió tay lái',['DF1166'],'forza',2020],
 ['CB500X kính','kính chắn gió',['D1171ST','D1171S'],'cb500x',2020],['CB500X khung','khung bảo vệ động cơ',['TN1171','TNH1171'],'cb500x',2022],
 ['CB500X két','bảo vệ két nước',['PR1121','PR1171'],'cb500x',2020],['CB500X két đời cuối','bảo vệ két nước',['PR1171'],'cb500x',2023],
 ['CB650R két','bảo vệ két nước',['PR1208'],'cb650r',2025],['Tracer kính','kính chắn gió',['D2130S'],'tracer',2018],
 ['Tracer khung','khung bảo vệ động cơ',['TN2130'],'tracer',2018],['PCX kính','kinh chan gio',['D1190ST'],'pcx',2022]
];
const vehicleId = key => {
  const m = manifest.vehicles.find(v => v.key === key);
  const matches = options.vehicles.filter(v => v.brand === m.brand && v.model === m.model && v.variant === m.variant && v.market === m.market);
  assert.equal(matches.length, 1); return matches[0].id;
};
const ask = async q => { const r = await fetch(base + '/api/recommendations', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(q) }); assert.equal(r.status, 200); return r.json(); };
let sum = 0; const results = [];
for (const [name,query,relevant,vehicle,year] of cases) {
  const result = await ask({ query, vehicleModelId:vehicle ? vehicleId(vehicle) : null, year:year ?? null, limit:3 });
  assert.equal(result.status, 'Ready', name); assert.ok(result.items.length, name);
  const codes = result.items.map(item => { const p = byName.get(item.name); assert.ok(p, item.name); assert.ok((p.stock ?? 8) > 0); assert.ok(p.kind === 1 || (p.kind === 2 && p.vehicle === vehicle && p.from <= year && p.to >= year)); return p.code; });
  const precision = codes.filter(c => relevant.includes(c)).length / codes.length; sum += precision;
  results.push({ name, query, vehicle:vehicle ?? null, year:year ?? null, expected:relevant, actual:codes, precision });
}
const negative = [
 ['thiếu đời',{query:'kính chắn gió',vehicleModelId:vehicleId('forza')},'NeedMoreInfo'],
 ['thiếu xe',{query:'kính chắn gió'},'NeedMoreInfo'], ['năm không xe',{year:2022},'NeedMoreInfo'],
 ['xe không tồn tại',{vehicleModelId:999999,year:2022},'NeedMoreInfo'], ['giá âm',{maxPrice:-1},'NeedMoreInfo'],
 ['giá đảo ngược',{minPrice:500000,maxPrice:100000},'NeedMoreInfo'], ['giá không đủ',{query:'mũ bảo hiểm',maxPrice:1},'NoMatches'],
 ['ngoài đời',{query:'kính chắn gió',vehicleModelId:vehicleId('forza'),year:2018},'NoMatches'],
 ['từ khóa không có',{query:'xyzkhongtonthai'},'NoMatches'],['giới hạn sai',{limit:0},'NeedMoreInfo']
];
for (const [name,query,status] of negative) { const result = await ask(query); assert.equal(result.status,status,name); assert.equal(result.items.length,0,name); results.push({name,query,expected:status,actual:result.status}); }
const budget = await ask({ query:'áo mưa', minPrice:200000, maxPrice:250000, preferLowPrice:true, limit:6 });
assert.ok(budget.items.length > 0);
assert.ok(budget.items.every(p => p.quantity > 0 && p.price >= 200000 && p.price <= 250000));
assert.ok(budget.items.every((p, i, all) => i === 0 || all[i - 1].price <= p.price));
const precision = sum / cases.length; assert.ok(precision >= .8, `Precision@3 ${precision}`);
await writeFile('../../docs/ui-review/recommendations/results-main.json', JSON.stringify({ database:'OnlineShop', scenarios:results.length, positive:cases.length, negative:negative.length, macroPrecisionAt3:precision, budgetCheck: budget.items.map(p => ({name:p.name, price:p.price, quantity:p.quantity})), results },null,2));
console.log(`PASS: main DB API, ${results.length} cases, macro P@3 ${(precision*100).toFixed(1)}%, stock and fitment constraints.`);

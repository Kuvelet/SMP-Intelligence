import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {createCatalog} from '../sample/Smp.Sample.Browser/Client/sqlite-catalog.mjs';

const require = createRequire(import.meta.url);
const initSqlJs = require('../sample/Smp.Sample.Browser/wwwroot/vendor/sql-wasm.js');
const SQL = await initSqlJs({locateFile:file => fileURLToPath(new URL(`../sample/Smp.Sample.Browser/wwwroot/vendor/${file}`, import.meta.url))});
const fixture = JSON.parse(await readFile(new URL('../sample/Smp.Sample.Core/Data/catalog.json', import.meta.url), 'utf8'));
const catalog = createCatalog(SQL, fixture);
const query = sql => JSON.parse(catalog.execute(sql));

test('reference lookup returns both parts', () => {
  assert.deepEqual(query("SELECT [Part Number] FROM Interchange WHERE [Interchange Name]='DEMO-OE-1001' ORDER BY [Part Number]").rows,
    [['DEMO-PART-101'],['DEMO-PART-102']]);
});
test('vehicle follow-up returns five records', () => {
  assert.equal(query("SELECT * FROM ApplicationViO WHERE Part IN ('DEMO-PART-101','DEMO-PART-102')").rows.length, 5);
});
test('distinct counts do not count every model-year row', () => {
  assert.deepEqual(query('SELECT Make,COUNT(DISTINCT Part) FROM ApplicationViO GROUP BY Make ORDER BY Make').rows,
    [['Example Motors',3],['Fictional Vehicles',2],['Sample Auto',3]]);
});
test('query-only mode prevents writes independently of the C# guard', () => {
  assert.throws(() => query('DELETE FROM Interchange'), /readonly/);
  assert.equal(query('SELECT * FROM Interchange').rows.length, 8);
});
test('row cap and empty-result headers match the local executor', () => {
  const large = query('SELECT a.Part,b.Part FROM ApplicationViO a INNER JOIN ApplicationViO b ON 1=1');
  assert.equal(large.rows.length,100);
  assert.equal(large.truncated,true);
  assert.deepEqual(query("SELECT Part FROM ApplicationViO WHERE Part='DEMO-PART-108'"), {columns:['Part'],rows:[],truncated:false});
});

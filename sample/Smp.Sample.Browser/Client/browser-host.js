import {createCatalog} from './sqlite-catalog.js';

// This adapter uses the compiled C# pipeline. It does not emulate validation in JS.
const ready = (async () => {
  if (document.readyState === 'loading') await new Promise(resolve => document.addEventListener('DOMContentLoaded', resolve, {once:true}));
  const response = await fetch('catalog.json');
  if (!response.ok) throw new Error('The sample catalog could not load.');
  const fixture = await response.json();
  const SQL = await window.initSqlJs({locateFile: file => new URL(`vendor/${file}`, document.baseURI).href});
  window.smpSql = createCatalog(SQL, fixture);
  await window.Blazor.start();
})();

window.smpBrowserRequest = async (path, body) => {
  await ready;
  const response = JSON.parse(await window.DotNet.invokeMethodAsync('Smp.Sample.Browser', 'Request', path, JSON.stringify(body)));
  if (!response.ok) throw Object.assign(new Error(response.data.message), {data:response.data});
  return response.data;
};

ready.then(() => {
  document.querySelector('#runtime-status').textContent = 'Ready to explore · No sign-in or installation needed';
  document.querySelectorAll('button').forEach(button => { button.disabled = false; });
}).catch(error => {
  document.querySelector('#runtime-status').textContent = 'The demo could not load. Refresh the page or try a current browser.';
  document.querySelector('#runtime-status').classList.add('runtime-error');
  console.error('Sample initialization failed', error);
});

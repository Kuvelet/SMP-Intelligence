const $ = (selector) => document.querySelector(selector);
let previousParts = [];
let busy = false;

function node(tag, text, className) {
  const element = document.createElement(tag);
  if (text !== undefined) element.textContent = text;
  if (className) element.className = className;
  return element;
}

// All questions, SQL, errors and database cells are inserted as text, never HTML.
function renderTables(target, tables) {
  for (const result of tables) {
    target.append(node('div', result.title, 'result-label'));
    const wrap = node('div', undefined, 'table-wrap');
    wrap.tabIndex = 0;
    wrap.setAttribute('role', 'region');
    wrap.setAttribute('aria-label', result.title);
    const table = node('table');
    const head = node('thead');
    const header = node('tr');
    for (const column of result.columns) {
      const th = node('th', column);
      th.scope = 'col';
      header.append(th);
    }
    head.append(header);
    table.append(head);
    const body = node('tbody');
    for (const row of result.rows) {
      const tr = node('tr');
      for (const cell of row) tr.append(node('td', cell === null ? '—' : String(cell)));
      body.append(tr);
    }
    table.append(body);
    wrap.append(table);
    target.append(wrap, node('p', `${result.rows.length} row${result.rows.length === 1 ? '' : 's'}${result.truncated ? ' shown · display limit reached' : ''} · Fictional sample data`, 'row-count'));
    const details = node('details', undefined, 'sql-details');
    details.append(node('summary', 'Inspect the validated SQL'), node('p', 'Validated T-SQL'), node('pre', result.sql));
    if (result.executedSql !== result.sql) details.append(node('p', 'SQL executed by the local SQLite sample'), node('pre', result.executedSql));
    target.append(details);
  }
}

async function post(path, body) {
  // The hosted demo runs the same C# pipeline in WebAssembly, without an API server.
  if (window.smpBrowserRequest) return window.smpBrowserRequest(path, body);
  const response = await fetch(path, {method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(body)});
  const data = await response.json();
  if (!response.ok) throw Object.assign(new Error(data.message || 'The request could not be processed.'), {data});
  return data;
}

function setBusy(value) {
  busy = value;
  for (const button of document.querySelectorAll('.example, #ask, #reset, #run-sql')) button.disabled = value;
}

async function ask(question) {
  if (busy || !question.trim()) return;
  setBusy(true);
  $('#welcome')?.remove();
  const turn = node('article', undefined, 'turn');
  turn.append(node('p', question, 'question-text'));
  const answer = node('div', undefined, 'answer');
  const pending = node('p', 'Checking the sample catalog…');
  answer.append(pending);
  turn.append(answer);
  $('#conversation').append(turn);
  $('#question').value = '';
  try {
    const result = await post('/api/lookup', {question, previousParts});
    pending.textContent = result.message;
    if (result.status === 'ok') previousParts = result.parts;
    renderTables(answer, result.tables);
  } catch (error) {
    pending.textContent = error.message;
    pending.className = 'error-text';
  } finally {
    setBusy(false);
    $('#conversation').scrollTop = $('#conversation').scrollHeight;
    $('#question').focus({preventScroll: true});
  }
}

$('#lookup-form').addEventListener('submit', (event) => { event.preventDefault(); ask($('#question').value); });
for (const button of document.querySelectorAll('[data-question]')) button.addEventListener('click', () => ask(button.dataset.question));
$('#reset').addEventListener('click', () => {
  previousParts = [];
  const welcome = node('p', 'Conversation cleared. Start with a sample reference such as DEMO-OE-1001.', 'empty-sql');
  welcome.id = 'welcome';
  $('#conversation').replaceChildren(welcome);
  $('#question').value = '';
  $('#question').focus();
});

for (const mode of ['lookup', 'sql']) {
  $(`#${mode}-tab`).addEventListener('click', () => {
    for (const id of ['lookup', 'sql']) {
      const active = id === mode;
      $(`#${id}-panel`).hidden = !active;
      $(`#${id}-tab`).classList.toggle('active', active);
      $(`#${id}-tab`).setAttribute('aria-pressed', String(active));
    }
  });
}

for (const button of document.querySelectorAll('.sql-example')) button.addEventListener('click', () => {
  $('#sql-input').value = button.dataset.sql;
  $('#sql-input').focus();
});

$('#sql-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  if (busy) return;
  setBusy(true);
  const target = $('#sql-result');
  target.replaceChildren(node('p', 'Validating the query…', 'empty-sql'));
  try {
    const tables = await post('/api/sql', {sql: $('#sql-input').value});
    const card = node('div', undefined, 'success-card');
    card.append(node('strong', 'Accepted and executed'), node('span', 'The query passed both checks and ran against the fictional catalog.'));
    target.replaceChildren(card);
    renderTables(target, tables);
  } catch (error) {
    const data = error.data;
    const card = node('div', undefined, 'error-card');
    card.append(node('strong', data?.executed === false ? 'Blocked before execution' : 'Query could not complete'), node('span', error.message));
    if (data?.stage) card.append(node('small', data.stage));
    target.replaceChildren(card);
  } finally { setBusy(false); }
});

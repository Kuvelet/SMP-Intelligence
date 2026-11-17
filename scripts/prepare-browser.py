"""Prepare static demo assets from the shared UI and a pinned SQLite WASM package."""
import argparse
import base64
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--base-path', default='/SMP-Intelligence/')
args = parser.parse_args()
if not re.fullmatch(r'/[A-Za-z0-9_/-]*', args.base_path) or not args.base_path.endswith('/'):
    parser.error('Use a slash-delimited relative base path such as /SMP-Intelligence/.')

target = ROOT / 'sample/Smp.Sample.Browser/wwwroot'
target.mkdir(parents=True, exist_ok=True)
shared = ROOT / 'sample/Smp.Sample.Web/wwwroot'
for name in ('app.js', 'styles.css'):
    shutil.copyfile(shared / name, target / name)
for path in (ROOT / 'sample/Smp.Sample.Browser/Client').iterdir():
    # .js is served with the correct module MIME type by basic Windows file servers too.
    shutil.copyfile(path, target / (path.stem + '.js' if path.suffix == '.mjs' else path.name))
shutil.copyfile(ROOT / 'sample/Smp.Sample.Core/Data/catalog.json', target / 'catalog.json')

html = (shared / 'index.html').read_text(encoding='utf-8')
html = html.replace('<title>', f'<base href="{args.base_path}">\n  <title>', 1)
html = html.replace('Runnable sample', 'Live demo').replace('href="/"', 'href="./"')
html = html.replace('CODE SAMPLE / BY CAN KUVELET', 'LIVE DEMO / BY CAN KUVELET')
html = html.replace('Runs entirely on your computer', 'Runs privately in your browser')
html = html.replace('Fictional records. No API key.', 'Fictional records. No setup.')
html = html.replace('<script src="app.js" defer></script>', '''<script src="vendor/sql-wasm.js" defer></script>
  <script src="_framework/blazor.webassembly.js" autostart="false" defer></script>
  <script src="browser-host.js" type="module"></script>
  <script src="app.js" defer></script>''')
html = html.replace('<main>', '<main>\n    <p id="runtime-status" class="runtime-status" role="status">Loading the demo… The first visit may take a few moments.</p>')
html = html.replace('<button ', '<button disabled ')
(target / 'index.html').write_text(html, encoding='utf-8')
with (target / 'styles.css').open('a', encoding='utf-8') as style:
    style.write('\n.runtime-status{font-size:11px;color:#47695b;margin:19px 0 -19px}.runtime-error{color:#8f352f}\n')
(target / '.nojekyll').write_text('', encoding='utf-8')

# Integrity is locked in source, not accepted dynamically at build time.
lock = json.loads((ROOT / 'scripts/browser-dependency.json').read_text(encoding='utf-8'))
archive = urllib.request.urlopen(lock['url'], timeout=60).read()
actual = 'sha512-' + base64.b64encode(hashlib.sha512(archive).digest()).decode()
if actual != lock['integrity']:
    raise SystemExit('SQLite package integrity mismatch.')
vendor = target / 'vendor'
vendor.mkdir(exist_ok=True)
with tarfile.open(fileobj=io.BytesIO(archive), mode='r:gz') as bundle:
    for member, name in [('package/dist/sql-wasm.js', 'sql-wasm.js'),
                         ('package/dist/sql-wasm.wasm', 'sql-wasm.wasm'),
                         ('package/LICENSE', 'sql.js-LICENSE.txt')]:
        stream = bundle.extractfile(member)
        if stream is None:
            raise SystemExit(f'Missing dependency file: {member}')
        (vendor / name).write_bytes(stream.read())
print(f'Prepared browser assets with {lock["name"]} {lock["version"]}; base path {args.base_path}')

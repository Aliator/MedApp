// Phase 1's check of the backend in the browser (docs/full-browser-demo-plan.md): serves a published wwwroot, opens the
// harness page in Chromium, and checks each answer is the API's.
//   dotnet publish MedApp.Browser -c Release -o <out>      (needs the wasm-tools workload)
//   NODE_PATH=tools/ui-parity/node_modules node MedApp.Browser/harness/check.cjs <out>/wwwroot
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.resolve(process.argv[2] ?? 'wwwroot');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.dat': 'application/octet-stream' };

const server = http.createServer((req, res) => {
  const file = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
    const index = path.join(file, 'index.html');
    if (fs.existsSync(index)) return send(res, index);
    res.writeHead(404).end();
    return;
  }
  send(res, file);
});

const served = { files: 0, raw: 0, gzip: 0, brotli: 0 };
const sizeOf = file => (fs.existsSync(file) ? fs.statSync(file).size : null);

function send(res, file) {
  const raw = sizeOf(file);
  served.files++;
  served.raw += raw;
  served.gzip += sizeOf(file + '.gz') ?? raw;
  served.brotli += sizeOf(file + '.br') ?? raw;
  res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}

const expected = [
  ['GET /api/auth/whoami', 200, j => j.isAuthenticated === false],
  ['POST /api/auth/login', 200, j => typeof j.sessionId === 'string'],
  ['GET /api/auth/whoami', 200, j => j.name === 'admin' && j.roles.includes('Admin') && j.permissions.includes('ManageUsers')],
  ['POST /api/auth/users', 200, j => j.username === 'clinician'],
  ['PUT /api/auth/users/clinician/roles', 204],
  ['POST /api/auth/users', 200, j => j.username === 'lab.tech'],
  ['PUT /api/auth/users/lab.tech/roles', 204],
  ['POST /api/auth/logout', 204],
  ['POST /api/auth/login', 200],
  ['GET /api/auth/whoami', 200, j => j.name === 'clinician' && j.permissions.includes('EditPatients')],
  ['POST /api/patients', 201, j => j.lastName === 'McTest' && j.nhsNumber === '9990000018'],
  ['GET /api/patients/search', 200, j => j.totalCount === 1 && j.items[0].lastName === 'McTest'],
  ['GET /api/patients/', 200, j => j.firstName === 'Testy'],
  ['GET /api/pathways/patient-pathways/search', 200, j => j.totalCount === 0],
  ['POST /api/patients', 400, j => j.type?.endsWith('section-15.5.1') && j.errors.FirstName?.[0] === 'The FirstName field is required.'],
  ['POST /api/patients', 400, j => j.errors['$.firstName'] && j.errors.request?.[0] === 'The request field is required.'],
  ['POST /api/patients', 400, j => j.title === 'One or more validation errors occurred.' && j.errors.Email],
  ['POST /api/patients', 409, j => j.title?.includes('NHS number')],
  ['GET /api/patients/', 404, j => j.title === 'Not Found' && j.status === 404],
  ['GET /api/nothing-here', 404, j => j === null],
  ['POST /api/auth/logout', 204],
  ['POST /api/auth/login', 200],
  ['POST /api/patients', 403, j => j.code === 'permission_missing' && j.permission === 'EditPatients'],
  ['POST /api/auth/logout', 204],
  ['GET /api/patients/search', 401, j => j === null],
  ['POST /api/auth/login', 200],
  ['GET /api/activity/search', 200, j => j.totalCount > 0],
  ['POST /api/auth/logout', 204],
];

(async () => {
  await new Promise(resolve => server.listen(0, resolve));
  const url = `http://localhost:${server.address().port}/`;
  const browser = await chromium.launch();
  const page = await browser.newPage();
  page.on('console', message => message.type() !== 'debug' && console.log('console:', message.type(), message.text().slice(0, 500)));
  page.on('pageerror', error => console.log('pageerror:', error.message));
  const started = Date.now();
  await page.goto(url);
  await page.waitForFunction(() => window.harness?.done === true, null, { timeout: 60000 });
  const harness = await page.evaluate(() => window.harness);
  await browser.close();
  server.close();

  let failures = 0;
  harness.steps.forEach((step, i) => {
    const [request, status, check] = expected[i] ?? [];
    const ok = request && `${step.method} ${step.url}`.startsWith(request) && step.status === status && (!check || check(step.json));
    if (!ok) failures++;
    console.log(`${ok ? 'ok  ' : 'FAIL'} ${step.method} ${step.url} -> ${step.status} (${step.ms} ms)${ok ? '' : ' ' + JSON.stringify(step.json)}`);
  });
  if (harness.steps.length !== expected.length) failures++;
  const mb = bytes => Math.round(bytes / 1e5) / 10;
  console.log(`downloaded ${served.files} files: ${mb(served.raw)} MB, ${mb(served.gzip)} MB gzipped, ${mb(served.brotli)} MB with Brotli`);
  console.log(JSON.stringify({ ...harness.timings, pageLoadToDoneMs: Date.now() - started, error: harness.error }));
  console.log(failures === 0 && !harness.error ? 'PASSED' : `FAILED (${failures})`);
  process.exit(failures === 0 && !harness.error ? 0 : 1);
})();

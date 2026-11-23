// The check of the backend in the browser (docs/full-browser-demo-plan.md): serves a published wwwroot and the demo
// database, opens the harness page in Chromium, and checks each answer is the API's.
//   dotnet publish MedApp.Browser -c Release -o <out>                              (needs the wasm-tools workload)
//   dotnet run --project tools/MedApp.BrowserDb -c Release -- --out <db>           (the demo database)
//   NODE_PATH=tools/ui-parity/node_modules node MedApp.Browser/harness/check.cjs <out>/wwwroot <db>
// Runs, in order: a new empty database (Phase 1's checks); the published demo database, downloaded, with each demo
// account's answers and a note added; a reload, from the copy saved in IndexedDB, with the note; a newer published
// database (db-version.json's date moved on), which replaces the saved copy; and the smoke check (smoke.js), on a
// newly downloaded copy: as each of four demo accounts, every GET in MedApp.Web/src/api/schema.d.ts it can fill in,
// then a set of changes, failing on any 5xx or answer that isn't the API's.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.resolve(process.argv[2] ?? 'wwwroot');
const dbFolder = path.resolve(process.argv[3] ?? root);
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.dat': 'application/octet-stream', '.gz': 'application/gzip' };
const DATABASE_FILES = ['medapp.db.gz', 'db-version.json'];
const SCHEMA = path.join(__dirname, '..', '..', 'MedApp.Web', 'src', 'api', 'schema.d.ts');
let newerBuild = null;
let operations = [];

const server = http.createServer((req, res) => {
  const name = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (name === '/smoke-operations.json') {
    res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(operations));
    return;
  }
  if (name === '/db-version.json' && newerBuild) {
    const version = JSON.parse(fs.readFileSync(path.join(dbFolder, 'db-version.json'), 'utf8'));
    res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({ ...version, builtOn: newerBuild }));
    return;
  }
  const file = DATABASE_FILES.includes(name.slice(1)) ? path.join(dbFolder, name) : path.join(root, name);
  if (!(file.startsWith(root) || file.startsWith(dbFolder)) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
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
  if (path.dirname(file) === root || file.includes('_framework')) {
    served.files++;
    served.raw += raw;
    served.gzip += sizeOf(file + '.gz') ?? raw;
    served.brotli += sizeOf(file + '.br') ?? raw;
  }
  res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Content-Length': raw });
  fs.createReadStream(file).pipe(res);
}

const signedIn = name => j => j.isAuthenticated === true && j.name === name;
const count = j => Object.values(j ?? {}).filter(v => typeof v === 'number').reduce((a, b) => a + b, 0);

const empty = [
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
  ['POST /api/patients', 400, j => j.title === 'One or more validation errors occurred.' && j.errors.FirstName?.[0] === 'First name must not be empty.'],
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

// What each demo account sees (docs/demo-data.md): Leah Corrin found (patients aren't behind team walls), their Needs
// your attention counts (403 where their role has no such list), and today's calendar.
const leah = j => j.totalCount === 1 && j.items[0].firstName === 'Leah' && j.items[0].lastName === 'Corrin';
const forbidden = permission => j => j.code === 'permission_missing' && j.permission === permission;
const calendarEntries = j => ['blocks', 'appointments', 'meetings', 'sessions'].map(k => `${j[k]?.length ?? 0} ${k}`).join(', ');
const calendarToday = j => j.days?.length === 1 && j.days[0].isToday && (j.blocks.length + j.appointments.length + j.meetings.length + j.sessions.length) > 0;
const ACCOUNTS = {
  admin: { search: [200, leah], counts: [200, 200, 200, [403, forbidden('ProcessSpecimens')], 200] },
  'n.thornbury': { search: [200, leah], counts: [200, 200, 200, [403, forbidden('ProcessSpecimens')], [403, forbidden('ManagePrivacy')]] },
  'r.aldous': { search: [200, leah], counts: [200, 200, 200, 200, [403, forbidden('ManagePrivacy')]] },
  's.varma': { search: [200, leah], counts: [200, 200, 200, [403, forbidden('ProcessSpecimens')], [403, forbidden('ManagePrivacy')]] },
};
const COUNT_URLS = ['GET /api/results/count', 'GET /api/letters/count', 'GET /api/referrals/awaiting-triage/count', 'GET /api/lab-cases/count', 'GET /api/data-requests/count'];
const attention = {};

function demoSteps(note) {
  const steps = [['GET /api/auth/whoami', 200, j => j.isAuthenticated === false]];
  for (const [username, expect] of Object.entries(ACCOUNTS)) {
    const label = (attention[username] = { count: 0 });
    steps.push(['POST /api/auth/login', 200, j => typeof j.sessionId === 'string']);
    steps.push(['GET /api/auth/whoami', 200, signedIn(username)]);
    steps.push(['GET /api/patients/search', ...expect.search]);
    COUNT_URLS.forEach((url, i) => {
      const [status, check] = Array.isArray(expect.counts[i]) ? expect.counts[i] : [expect.counts[i], null];
      steps.push([url, status, check ?? (j => (label.count += count(j)) >= 0 && Object.values(j).some(v => typeof v === 'number'))]);
    });
    steps.push(['GET /api/calendar?', 200, j => { label.calendar = calendarEntries(j); return calendarToday(j); }]);
    steps.push(['POST /api/auth/logout', 204]);
  }
  steps.push(['POST /api/auth/login', 200]);
  steps.push(['GET /api/pathways/insights', 200, j => j.open.current > 0 && j.stageTimes.some(t => t.stages?.length > 0)]);
  steps.push(['POST /api/auth/logout', 204]);
  steps.push(['POST /api/auth/login', 200]);
  steps.push(['POST /api/pathways/patient-pathways/', 201, j => j.text === note]);
  steps.push(['GET /api/pathways/patient-pathways/', 200, j => j.noteList?.some(n => n.text === note)]);
  steps.push(['POST /api/auth/logout', 204]);
  return steps;
}

const reloadedSteps = (note, kept) => [
  ['POST /api/auth/login', 200],
  ['GET /api/pathways/patient-pathways/', 200, j => j.noteList?.some(n => n.text === note) === kept],
  ['POST /api/auth/logout', 204],
];

let failures = 0;

function checkRun(name, harness, expected, extra = () => true) {
  console.log(`\n== ${name} (${harness.source}) ==`);
  harness.steps.forEach((step, i) => {
    const [request, status, check] = expected[i] ?? [];
    const ok = request && `${step.method} ${step.url}`.startsWith(request) && step.status === status && (!check || check(step.json));
    if (!ok) failures++;
    console.log(`${ok ? 'ok  ' : 'FAIL'} ${step.method} ${step.url} -> ${step.status} (${step.ms} ms)${ok ? '' : ' ' + JSON.stringify(step.json)?.slice(0, 600)}`);
  });
  if (harness.steps.length !== expected.length) {
    failures++;
    console.log(`FAIL ${harness.steps.length} requests, expected ${expected.length}`);
  }
  if (harness.error) {
    failures++;
    console.log(`FAIL ${harness.error}`);
  }
  if (!extra()) failures++;
  const { backend, ...timings } = harness.timings;
  console.log(JSON.stringify({ ...timings, ...backend }));
}

async function open(page, run) {
  const started = Date.now();
  await page.goto(`${url}?run=${run}`);
  await page.waitForFunction(() => window.harness?.done === true, null, { timeout: 120000 });
  const harness = await page.evaluate(() => window.harness);
  harness.timings.pageLoadToDoneMs = Date.now() - started;
  return harness;
}

const expect = (ok, what) => {
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${what}`);
  return ok;
};

function checkSmoke(harness) {
  console.log(`\n== The smoke check (${harness.source}) ==`);
  if (harness.error || !harness.smoke) {
    failures++;
    console.log(`FAIL ${harness.error ?? 'no results'}`);
    return;
  }
  const gets = operations.filter(op => op.method === 'GET').length;
  for (const [username, results] of Object.entries(harness.smoke.reads)) {
    const counts = {};
    for (const r of results) counts[r.skipped ? 'not filled in' : r.status] = (counts[r.skipped ? 'not filled in' : r.status] ?? 0) + 1;
    const bad = results.filter(r => r.problem);
    failures += bad.length;
    console.log(`${bad.length ? 'FAIL' : 'ok  '} ${username}: ${results.length - (counts['not filled in'] ?? 0)} of ${gets} GETs, ${JSON.stringify(counts)} (${harness.timings[`reads.${username}Ms`]} ms)`);
    for (const r of bad) console.log(`       FAIL GET ${r.url} -> ${r.status}: ${r.problem}`);
    for (const r of results.filter(r => r.skipped)) console.log(`       not filled in: GET ${r.url}`);
    const slowest = results.filter(r => !r.skipped).sort((a, b) => b.ms - a.ms).slice(0, 3);
    console.log(`       slowest: ${slowest.map(r => `${r.url} ${r.ms} ms`).join(', ')}`);
  }
  for (const r of harness.smoke.changes) {
    if (r.problem) failures++;
    console.log(`${r.problem ? 'FAIL' : 'ok  '} [${r.area}] ${r.label} (${r.username}): ${r.method} ${r.url} -> ${r.status}${r.problem ? ' ' + r.problem : ''}`);
  }
  console.log(`${harness.smoke.changes.length} changes in ${harness.timings.changesMs} ms`);
}

let url;
(async () => {
  const { schemaOperations } = await import(path.join(__dirname, '..', 'wwwroot', 'smoke.js'));
  operations = schemaOperations(fs.readFileSync(SCHEMA, 'utf8'));
  await new Promise(resolve => server.listen(0, resolve));
  url = `http://localhost:${server.address().port}/`;
  const browser = await chromium.launch();
  const watch = page => {
    page.on('console', message => message.type() === 'error' && console.log('console:', message.text().slice(0, 500)));
    page.on('pageerror', error => console.log('pageerror:', error.message));
  };

  const emptyPage = await browser.newPage();
  watch(emptyPage);
  const first = await open(emptyPage, 'empty');
  checkRun('A new empty database', first, empty, () => expect(first.source === 'empty', 'started on an empty database'));
  const mb = bytes => Math.round(bytes / 1e5) / 10;
  console.log(`downloaded ${served.files} files: ${mb(served.raw)} MB, ${mb(served.gzip)} MB gzipped, ${mb(served.brotli)} MB with Brotli`);
  await emptyPage.close();

  const context = await browser.newContext();
  const page = await context.newPage();
  watch(page);
  const demo = await open(page, 'demo');
  checkRun('The published demo database', demo, demoSteps(demo.note), () =>
    expect(demo.source === 'downloaded', 'downloaded the published database')
    && expect(demo.accounts?.length === 9 && !JSON.stringify(demo.accounts).includes('password'), 'nine accounts listed, no passwords')
    && expect(demo.progress.some(p => p.database.total > 0 && p.database.loaded === p.database.total), 'reported the database\'s download')
    && expect(demo.progress.some(p => p.backend.total > 0 && p.backend.loaded === p.backend.total), `reported the backend's download (${demo.progress.at(-1)?.backend.total} files)`)
    && Object.entries(attention).every(([user, a]) =>
      expect(a.count > 0, `${user}: Needs your attention ${a.count}; today's calendar ${a.calendar}`)));

  const reloaded = await open(page, 'reloaded');
  checkRun('Reloaded, from the copy saved in IndexedDB', reloaded, reloadedSteps(demo.note, true), () =>
    expect(reloaded.source === 'saved', 'started from the saved copy'));

  newerBuild = '2099-01-01';
  const newer = await open(page, 'reloaded');
  checkRun('A newer published database', newer, reloadedSteps(demo.note, false), () =>
    expect(newer.source === 'downloaded' && newer.builtOn === newerBuild, 'downloaded the newer database, replacing the saved copy'));

  newerBuild = null;
  const smokeContext = await browser.newContext();
  const smokePage = await smokeContext.newPage();
  watch(smokePage);
  const started = Date.now();
  await smokePage.goto(`${url}?run=smoke`);
  await smokePage.waitForFunction(() => window.harness?.done === true, null, { timeout: 600000 });
  const smoke = await smokePage.evaluate(() => window.harness);
  smoke.timings.pageLoadToDoneMs = Date.now() - started;
  checkSmoke(smoke);
  const { backend: smokeBackend, ...smokeTimings } = smoke.timings;
  console.log(JSON.stringify({ ...smokeTimings, ...smokeBackend }));

  await browser.close();
  server.close();
  console.log(failures === 0 ? '\nPASSED' : `\nFAILED (${failures})`);
  process.exit(failures === 0 ? 0 : 1);
})();

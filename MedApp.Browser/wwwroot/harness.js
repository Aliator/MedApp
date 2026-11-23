// The checks of the backend in the browser (docs/full-browser-demo-plan.md), driven by harness/check.cjs: each run
// (?run=…) sends its requests through backend.js and puts them, with their answers, on the page and in
// window.harness, which the Playwright check reads.
//   ?run=empty      Phase 1's: a new empty database, with only the first admin.
//   ?run=demo       Phase 2's: the published demo database; each demo account's answers, the insights (SQLite's
//                   own functions), then a note added.
//   ?run=reloaded   The saved copy: the note is still there.
//   ?run=smoke      Phase 3's: every GET in the API's schema each demo account can fill in, then a set of changes
//                   (smoke.js), on a newly downloaded demo database.
import { fetchApi, flush, startBackend } from './backend.js';
import { ACCOUNTS, smokeChanges, smokeReads } from './smoke.js';

const log = document.getElementById('log');
const status = document.getElementById('status');
const run = new URLSearchParams(location.search).get('run') ?? 'demo';
const harness = (window.harness = { run, steps: [], done: false, error: null, timings: {}, progress: [] });
const t0 = performance.now();

async function send(method, url, body, raw) {
  const bytes = raw !== undefined ? raw : body === undefined ? undefined : JSON.stringify(body);
  const headers = { Host: location.host };
  if (bytes !== undefined) headers['Content-Type'] = 'application/json';
  const started = performance.now();
  const response = await fetchApi(method, url, headers, bytes);
  const json = response.body && (response.headers['Content-Type'] ?? '').includes('json') ? JSON.parse(response.body) : null;
  const step = { method, url, status: response.status, ms: Math.round(performance.now() - started), json };
  harness.steps.push(step);
  log.textContent += `${method} ${url} -> ${step.status} (${step.ms} ms)\n  ${(response.body ?? '').slice(0, 400)}\n`;
  return json;
}

const ukToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/London' }).format(new Date());

async function emptyChecks() {
  await send('GET', '/api/auth/whoami');
  harness.timings.firstAnswerMs = Math.round(performance.now() - t0);
  await send('POST', '/api/auth/login', { username: 'admin', password: 'any' });
  await send('GET', '/api/auth/whoami');
  await send('POST', '/api/auth/users', { username: 'clinician' });
  await send('PUT', '/api/auth/users/clinician/roles', { roles: ['Clinician'] });
  await send('POST', '/api/auth/users', { username: 'lab.tech' });
  await send('PUT', '/api/auth/users/lab.tech/roles', { roles: ['Lab'] });
  await send('POST', '/api/auth/logout');

  await send('POST', '/api/auth/login', { username: 'clinician', password: 'any' });
  await send('GET', '/api/auth/whoami');
  const patient = await send('POST', '/api/patients', {
    firstName: 'Testy', lastName: 'McTest', dateOfBirth: '1980-02-03', email: 'testy@example.com', nhsNumber: '999 000 0018',
  });
  await send('GET', '/api/patients/search?lastName=mctest&page=1&pageSize=10');
  await send('GET', `/api/patients/${patient?.id}`);
  await send('GET', '/api/pathways/patient-pathways/search?pageSize=5');
  await send('POST', '/api/patients', { firstName: '', lastName: 'X', dateOfBirth: '1980-02-03', email: 'x@example.com' });
  await send('POST', '/api/patients', undefined, '{"firstName": 3, ');
  await send('POST', '/api/patients', { firstName: 'Ok', lastName: 'X', dateOfBirth: '1980-02-03', email: 'not an email' });
  await send('POST', '/api/patients', { firstName: 'Dupe', lastName: 'X', dateOfBirth: '1980-02-03', email: 'd@example.com', nhsNumber: '9990000018' });
  await send('GET', '/api/patients/00000000-0000-4000-8000-000000000000');
  await send('GET', '/api/nothing-here');
  await send('POST', '/api/auth/logout');

  await send('POST', '/api/auth/login', { username: 'lab.tech', password: 'any' });
  await send('POST', '/api/patients', { firstName: 'No', lastName: 'Way', dateOfBirth: '1980-02-03', email: 'n@example.com' });
  await send('POST', '/api/auth/logout');
  await send('GET', '/api/patients/search');

  await send('POST', '/api/auth/login', { username: 'admin', password: 'any' });
  await send('GET', '/api/activity/search?pageSize=5');
  await send('POST', '/api/auth/logout');
}

const NEEDS_ATTENTION = ['/api/results/count', '/api/letters/count', '/api/referrals/awaiting-triage/count', '/api/lab-cases/count', '/api/data-requests/count'];
const NOTE = 'A note from the harness (synthetic), kept in this browser.';

async function demoChecks(version) {
  await send('GET', '/api/auth/whoami');
  harness.timings.firstAnswerMs = Math.round(performance.now() - t0);
  for (const username of ['admin', 'n.thornbury', 'r.aldous', 's.varma']) {
    await send('POST', '/api/auth/login', { username, password: 'any' });
    await send('GET', '/api/auth/whoami');
    await send('GET', '/api/patients/search?lastName=corrin&page=1&pageSize=10');
    for (const url of NEEDS_ATTENTION) await send('GET', url);
    await send('GET', `/api/calendar?from=${ukToday()}&to=${ukToday()}`);
    await send('POST', '/api/auth/logout');
  }

  await send('POST', '/api/auth/login', { username: 'admin', password: 'any' });
  await send('GET', '/api/pathways/insights?weeks=12');
  await send('POST', '/api/auth/logout');

  await send('POST', '/api/auth/login', { username: 'n.thornbury', password: 'any' });
  await send('POST', `/api/pathways/patient-pathways/${version.tour.relay}/notes`, { text: NOTE });
  await send('GET', `/api/pathways/patient-pathways/${version.tour.relay}`);
  await send('POST', '/api/auth/logout');
}

async function smokeSend(method, url, body, extraHeaders = {}) {
  const headers = { Host: location.host, ...extraHeaders };
  const bytes = body === undefined ? undefined : body instanceof Uint8Array || typeof body === 'string' ? body : JSON.stringify(body);
  if (bytes !== undefined && !headers['Content-Type']) headers['Content-Type'] = 'application/json';
  const response = await fetchApi(method, url, headers, bytes);
  const type = response.headers['Content-Type'] ?? '';
  let json = null;
  try {
    json = response.body && type.includes('json') ? JSON.parse(response.body) : null;
  } catch {
    json = null;
  }
  const size = response.body != null ? new TextEncoder().encode(response.body).length : response.bodyBase64 ? atob(response.bodyBase64).length : 0;
  return { status: response.status, headers: response.headers, json, text: response.body ?? null, bytes: size };
}

async function smokeChecks(version) {
  const operations = await (await fetch('smoke-operations.json')).json();
  harness.smoke = { reads: {}, changes: [] };
  for (const username of ACCOUNTS) {
    await smokeSend('POST', '/api/auth/login', { username, password: 'any' });
    const started = performance.now();
    harness.smoke.reads[username] = await smokeReads(smokeSend, operations, version.tour, username);
    harness.timings[`reads.${username}Ms`] = Math.round(performance.now() - started);
    await smokeSend('POST', '/api/auth/logout');
  }
  const started = performance.now();
  harness.smoke.changes = await smokeChanges(smokeSend, version.tour);
  harness.timings.changesMs = Math.round(performance.now() - started);
}

async function reloadedChecks(version) {
  await send('POST', '/api/auth/login', { username: 'n.thornbury', password: 'any' });
  await send('GET', `/api/pathways/patient-pathways/${version.tour.relay}`);
  await send('POST', '/api/auth/logout');
}

try {
  const { version, source, timings } = await startBackend({
    database: run !== 'empty',
    onProgress: p => harness.progress.push(p),
  });
  Object.assign(harness, { source, builtOn: version?.builtOn ?? null, note: NOTE, accounts: version?.accounts ?? null });
  Object.assign(harness.timings, timings);
  status.textContent = `Started (${source}) in ${timings.readyMs} ms.`;

  if (run === 'empty') await emptyChecks();
  else if (run === 'reloaded') await reloadedChecks(version);
  else if (run === 'smoke') await smokeChecks(version);
  else await demoChecks(version);

  const saving = performance.now();
  await flush();
  harness.timings.lastSaveMs = Math.round(performance.now() - saving);
  status.textContent = `Done: ${harness.steps.length} requests (${source}). Ready in ${timings.readyMs} ms.`;
} catch (e) {
  harness.error = String(e?.stack ?? e);
  status.textContent = `Failed: ${harness.error}`;
} finally {
  harness.done = true;
}

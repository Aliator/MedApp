// Phase 1's check (docs/full-browser-demo-plan.md): the backend in this page answers real API requests. Each step's
// request and response go to the page and to window.harness, which the Playwright check reads.
import { dotnet } from './_framework/dotnet.js';

const log = document.getElementById('log');
const status = document.getElementById('status');
const harness = (window.harness = { steps: [], done: false, error: null, timings: {} });

const t0 = performance.now();
const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
const backend = (await getAssemblyExports(getConfig().mainAssemblyName)).MedAppBackend;
await runMain();
harness.timings.runtimeMs = Math.round(performance.now() - t0);
harness.timings.start = JSON.parse(await backend.StartAsync());
harness.timings.readyMs = Math.round(performance.now() - t0);

async function send(method, url, body, raw) {
  const bytes = raw !== undefined ? new TextEncoder().encode(raw) : body === undefined ? new Uint8Array() : new TextEncoder().encode(JSON.stringify(body));
  const headers = { Host: location.host };
  if (bytes.length > 0) headers['Content-Type'] = 'application/json';
  const started = performance.now();
  const response = JSON.parse(await backend.FetchAsync(method, url, JSON.stringify(headers), bytes));
  const json = response.body && (response.headers['Content-Type'] ?? '').includes('json') ? JSON.parse(response.body) : null;
  const step = { method, url, status: response.status, ms: Math.round(performance.now() - started), contentType: response.headers['Content-Type'] ?? null, json };
  harness.steps.push(step);
  log.textContent += `${method} ${url} -> ${step.status} (${step.ms} ms)\n  ${response.body ?? ''}\n`;
  return json;
}

try {
  if (harness.steps.length === 0) {
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
  status.textContent = `Done: ${harness.steps.length} requests. Ready in ${harness.timings.readyMs} ms.`;
} catch (e) {
  harness.error = String(e?.stack ?? e);
  status.textContent = `Failed: ${harness.error}`;
} finally {
  harness.done = true;
}

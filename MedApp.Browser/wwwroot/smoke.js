// The smoke check of every page's reads, and a set of changes, run by the harness (harness/check.cjs, ?run=smoke) as each
// demo account on the published demo database. send(method, url, body?, headers?) -> { status, headers, json, text }.
// operations: the schema's GETs ({ path }), from MedApp.Web/src/api/schema.d.ts. tour: db-version.json's tour IDs.

export const ACCOUNTS = ['admin', 'n.thornbury', 'r.aldous', 's.varma'];

const ukDate = (offsetDays = 0) =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/London' }).format(new Date(Date.now() + offsetDays * 86400000));

/** The schema's operations: each path's methods that have an operation (not `get?: never`). */
export function schemaOperations(schema) {
  const operations = [];
  let path = null;
  for (const line of schema.split('\n')) {
    if (line === '}') break;
    const p = line.match(/^ {4}"(\/api\/[^"]*)": \{$/);
    if (p) path = p[1];
    const m = path && line.match(/^ {8}(get|put|post|delete|patch): \{$/);
    if (m) operations.push({ method: m[1].toUpperCase(), path });
  }
  return operations;
}

const first = list => (Array.isArray(list) ? list : list?.items ?? [])[0];

/** How to fill each GET's path and query: a function of the context, returning a URL, or null when nothing fits. */
function fills(ctx) {
  const { tour, id, today, weekAhead } = ctx;
  const leah = async () => (await ctx.json('/api/patients/search?lastName=corrin')).items?.[0]?.id;
  const me = async () => (await ctx.json('/api/calendar/staff')).find?.(s => s.username === ctx.username)?.userId
    ?? (await ctx.json('/api/calendar/staff'))[0]?.userId;
  const someStaff = async () => (await ctx.json('/api/calendar/staff')).find?.(s => s.username === 'n.thornbury')?.userId;
  const attachment = async () => {
    for (const pathway of [tour.photosToTake, tour.gpReport, tour.relay]) {
      const found = first(await ctx.json(`/api/attachments?pathwayId=${pathway}`));
      if (found) return found.id;
    }
    const patients = (await ctx.json('/api/patients/search?pageSize=100')).items ?? [];
    for (const p of patients) {
      const found = first(await ctx.json(`/api/attachments?patientId=${p.id}`));
      if (found) return found.id;
    }
    return null;
  };
  const meeting = async () => {
    const calendar = await ctx.json(`/api/calendar?from=${ukDate(-14)}&to=${ukDate(14)}`);
    return calendar?.meetings?.[0]?.id ?? null;
  };
  return {
    '/api/activity/search': () => '?pageSize=5',
    '/api/appointments/{id}': async () => id(first(await ctx.json('/api/appointments/search?pageSize=1'))?.id),
    '/api/appointments/search': () => '?pageSize=5',
    '/api/appointments/{id}/messages': async () => id(first(await ctx.json('/api/appointments/search?pageSize=1'))?.id),
    '/api/attachments': async () => `?patientId=${await leah()}`,
    '/api/attachments/{id}/content': async () => id(await attachment()),
    '/api/attachments/{id}/thumbnail': async () => id(await attachment()),
    '/api/auth/users/search': () => '?pageSize=5',
    '/api/auth/users/{username}': () => ({ username: ctx.username }),
    '/api/auth/users/{username}/roles': () => ({ username: 'n.thornbury' }),
    '/api/auth/users/{username}/teams': () => ({ username: 'n.thornbury' }),
    '/api/calendar': () => `?from=${today}&to=${weekAhead}`,
    '/api/calendar/team': () => `?date=${today}`,
    '/api/calendar/availability': async () => `?staffUserIds=${await someStaff()}&from=${today}&to=${weekAhead}&durationMinutes=30`,
    '/api/calendar/staff/{userId}/week-template': async () => ({ userId: await me() }),
    '/api/calendar/staff/{userId}/hours': async () => ({ userId: await me() }),
    '/api/calendar/staff/{userId}/day-templates': async () => ({ userId: await me() }),
    '/api/calendar/staff/{userId}/week-templates': async () => ({ userId: await me() }),
    '/api/calendar/activity-types/{id}': async () => id(first(await ctx.json('/api/calendar/activity-types'))?.id),
    '/api/calendar/closed-days/bookings': () => `?date=${weekAhead}`,
    '/api/calendar/day-templates/{id}': async () =>
      id(first(await ctx.json(`/api/calendar/staff/${await me()}/day-templates`))?.id),
    '/api/calendar/week-templates/{id}': async () =>
      id(first(await ctx.json(`/api/calendar/staff/${await me()}/week-templates`))?.id),
    '/api/calendar/meetings/{id}': async () => id(await meeting()),
    '/api/clinics/sessions': () => `?from=${today}&to=${weekAhead}`,
    '/api/clinics/sessions/{id}': async () => id(first(await ctx.json(`/api/clinics/sessions?from=${ukDate(-30)}&to=${ukDate(30)}`))?.id),
    '/api/data-requests/search': () => '?pageSize=5',
    '/api/data-requests/{id}': () => id(tour.dataRequest),
    '/api/data-requests/{id}/review-items': () => id(tour.dataRequest),
    '/api/data-requests/{id}/pack': () => id(tour.dataRequest),
    '/api/fhir/Patient/{id}': async () => id(await leah()),
    '/api/fhir/Patient': async () => {
      const nhs = (await ctx.json('/api/patients/search?lastName=corrin')).items?.[0]?.nhsNumber;
      return `?identifier=${encodeURIComponent(`https://fhir.nhs.uk/Id/nhs-number|${nhs}`)}`;
    },
    '/api/hospital-requests/{id}': () => id(tour.hospitalRequest),
    '/api/lab-cases/{id}': () => id(tour.labCaseToReport),
    '/api/letter-templates/{id}': async () => id(first(await ctx.json('/api/letter-templates'))?.id),
    '/api/letters/search': () => '?view=AwaitingMyApproval&pageSize=5',
    '/api/letters/{id}': () => id(tour.letterToApprove),
    '/api/letters/{id}/preview': () => id(tour.letterToApprove),
    '/api/pathways/note-deletion-requests/{id}': () => null,
    '/api/pathways/audit/search': () => '?pageSize=5',
    '/api/pathways/insights': () => '?weeks=12',
    '/api/pathways/patient-pathway-stage/definitions/{id}': async () =>
      id(first(await ctx.json('/api/pathways/patient-pathway-stage/definitions'))?.id),
    '/api/pathways/patient-pathway-stage/templates/{id}': async () =>
      id(first(await ctx.json('/api/pathways/patient-pathway-stage/templates'))?.id),
    '/api/pathways/patient-pathways/{id}': () => id(tour.relay),
    '/api/pathways/patient-pathways/search': () => '?pageSize=5',
    '/api/patients/{id}/timeline': async () => id(await leah()),
    '/api/patients/{id}/timeline/overview': async () => id(await leah()),
    '/api/patients/{id}': async () => id(await leah()),
    '/api/patients/search': () => '?pageSize=5',
    '/api/patients/{id}/export': async () => id(await leah()),
    '/api/patients/retention-review': () => '?pageSize=5',
    '/api/practices/{id}': async () => id(first(await ctx.json('/api/practices'))?.id),
    '/api/referrals/search': () => '?pageSize=5',
    '/api/referrals/{id}': () => id(tour.referralToTriage),
    '/api/results/search': () => '?pageSize=5',
    '/api/results/{id}': () => id(tour.resultToSignOff),
    '/api/roles/{id}': async () => id(first(await ctx.json('/api/roles'))?.id),
    '/api/sites/{id}': async () => id(first(await ctx.json('/api/sites'))?.id),
    '/api/sites/{id}/hours': async () => id(first(await ctx.json('/api/sites'))?.id),
    '/api/teams/{id}': async () => id(first(await ctx.json('/api/teams'))?.id),
    '/api/pathways/{id}/consent-records': () => id(tour.photosToTake),
    '/api/specimens/search': () => '?pageSize=5',
    '/api/specimens/{id}': () => id(tour.priyaSpecimen),
    '/api/mdt/meetings/{meetingId}/items': () => ({ meetingId: tour.mdtToday }),
    '/api/consults/pathways/{pathwayId}': () => ({ pathwayId: tour.branchToChoose }),
    '/api/mdt/pathways/{pathwayId}/items': async () =>
      ({ pathwayId: first(await ctx.json(`/api/mdt/meetings/${tour.mdtToday}/items`))?.pathwayId }),
  };
}

/** Whether an answer is one the API gives: a success, or its own error shapes (ProblemDetails, FHIR's OperationOutcome). */
export function judge(response, path) {
  const { status, json } = response;
  if (status < 300) return null;
  if (status >= 500) return `a ${status}`;
  if (path.startsWith('/api/fhir')) return json?.resourceType === 'OperationOutcome' ? null : 'a FHIR error that is not an OperationOutcome';
  if (status === 401) return 'signed out';
  if (status === 403) return json?.status === 403 ? null : 'a 403 without a ProblemDetails';
  if (status === 404) return json?.status === 404 ? null : 'a 404 with no ProblemDetails (no route?)';
  if (status === 409) return json?.status === 409 ? null : 'a 409 without a ProblemDetails';
  return `a ${status}: ${JSON.stringify(json)?.slice(0, 300)}`;
}

/** Every GET in the schema that can be filled in, as one account. Returns { username, results: [{ url, status, problem }] }. */
export async function smokeReads(send, operations, tour, username) {
  const cache = new Map();
  const ctx = {
    tour,
    username,
    today: ukDate(),
    weekAhead: ukDate(6),
    id: value => (value ? { id: value } : null),
    json: async url => {
      if (!cache.has(url)) cache.set(url, send('GET', url).then(r => (r.status < 300 ? r.json : null)));
      return cache.get(url);
    },
  };
  const table = fills(ctx);
  const results = [];
  for (const { path } of operations.filter(op => op.method === 'GET')) {
    let url = path;
    const fill = table[path];
    const filled = fill ? await fill() : path.includes('{') ? null : '';
    if (filled === null || (typeof filled === 'object' && Object.values(filled).some(v => !v))) {
      results.push({ url: path, status: null, problem: null, skipped: true });
      continue;
    }
    if (typeof filled === 'object') for (const [name, value] of Object.entries(filled)) url = url.replace(`{${name}}`, encodeURIComponent(value));
    else url += filled;
    const started = Date.now();
    const response = await send('GET', url);
    results.push({ url, status: response.status, problem: judge(response, path), ms: Date.now() - started });
  }
  return results;
}

// A tiny PNG (1 x 1, synthetic) and a one-page PDF, for uploads.
const PNG = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='), c => c.charCodeAt(0));
const SVG = new TextEncoder().encode('<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>');

/**
 * Representative changes, at least one per area, each as the API answers it: a success, or the API's own refusal for
 * a rule (a 400, 403, 404 or 409). Returns [{ area, label, method, url, status, expected, problem }].
 */
export async function smokeChanges(send, tour) {
  const results = [];
  let signedIn = null;
  const as = async username => {
    if (signedIn === username) return;
    if (signedIn) await send('POST', '/api/auth/logout');
    await send('POST', '/api/auth/login', { username, password: 'any' });
    signedIn = username;
  };
  const step = async (area, label, username, method, url, body, expected, headers) => {
    await as(username);
    const response = await send(method, url, body, headers);
    const problem = !expected.includes(response.status)
      ? `expected ${expected.join(' or ')}: ${(response.text ?? '').slice(0, 300)}`
      : response.status >= 400 && !(response.json?.status === response.status || response.json?.resourceType === 'OperationOutcome')
        ? 'an error without a ProblemDetails'
        : null;
    results.push({ area, label, username, method, url, status: response.status, expected, problem });
    return response;
  };
  const get = async (username, url) => {
    await as(username);
    return (await send('GET', url)).json;
  };
  const day = offset => ukDateOf(offset);

  // Patients
  const patient = (await step('patients', 'register a patient', 'n.thornbury', 'POST', '/api/patients',
    { firstName: 'Smoke', lastName: 'Testcase', dateOfBirth: '1990-01-02', email: 'smoke@example.com' }, [201])).json;
  await step('patients', 'change their details', 'n.thornbury', 'PATCH', `/api/patients/${patient?.id}`,
    { firstName: 'Smoke', lastName: 'Testcase', dateOfBirth: '1990-01-03', email: 'smoke@example.com' }, [200, 204]);
  await step('patients', 'a lab account may not register one', 'r.aldous', 'POST', '/api/patients',
    { firstName: 'No', lastName: 'Way', dateOfBirth: '1990-01-02', email: 'no@example.com' }, [403]);

  // Pathways and stages
  const templates = await get('n.thornbury', '/api/pathways/patient-pathway-stage/templates');
  const template = templates?.find?.(t => !t.isArchived && t.teamName === 'Dermatology') ?? templates?.[0];
  const pathway = (await step('pathways', 'start a pathway', 'n.thornbury', 'POST', '/api/pathways/patient-pathways',
    { patientId: patient?.id, templateId: template?.id, title: 'Smoke check, left forearm', dueDate: day(60), priority: 'Medium' }, [201])).json;
  const stage = pathway?.stages?.find?.(s => s.isAvailable ?? true) ?? pathway?.stages?.[0];
  await step('pathways', 'start its first stage', 'n.thornbury', 'POST', `/api/pathways/patient-pathways/${pathway?.id}/stages/${stage?.id}/start`, undefined, [200, 204, 409]);
  await step('pathways', 'across the team wall', 's.varma', 'GET', `/api/pathways/patient-pathways/${pathway?.id}`, undefined, [404]);

  // Notes
  const note = (await step('notes', 'add a note', 'n.thornbury', 'POST', `/api/pathways/patient-pathways/${tour.relay}/notes`,
    { text: 'A smoke check note (synthetic).' }, [201])).json;
  await step('notes', 'edit it', 'n.thornbury', 'PUT', `/api/pathways/patient-pathways/${tour.relay}/notes/${note?.id}`,
    { text: 'A smoke check note, edited (synthetic).', expectedVersion: note?.version ?? 1 }, [200]);
  await step('notes', 'edit it from an old version', 'n.thornbury', 'PUT', `/api/pathways/patient-pathways/${tour.relay}/notes/${note?.id}`,
    { text: 'Too late.', expectedVersion: note?.version ?? 1 }, [409]);
  await step('notes', 'ask for it to be deleted', 'n.thornbury', 'POST',
    `/api/pathways/patient-pathways/${tour.relay}/notes/${note?.id}/deletion-requests`, { reason: 'Duplicate' }, [201]);
  const told = await get('admin', '/api/notifications');
  const requestId = (Array.isArray(told) ? told : told?.items ?? []).find(n => n.noteDeletionRequestId)?.noteDeletionRequestId;
  const deletion = (await step('notes', 'an admin reads the request', 'admin', 'GET', `/api/pathways/note-deletion-requests/${requestId}`, undefined, [200])).json;
  await step('notes', 'and deletes the note', 'admin', 'POST', `/api/pathways/note-deletion-requests/${requestId}/decision`,
    { deleteNote: true, expectedVersion: deletion?.note?.version }, [200]);

  // Referrals and triage
  const referral = await get('n.thornbury', `/api/referrals/${tour.referralToTriage}`);
  await step('referrals', 'accept a referral at triage', 'n.thornbury', 'POST', `/api/referrals/${tour.referralToTriage}/accept`,
    { templateId: template?.id, priority: 'High', expectedVersion: referral?.version }, [201]);
  await step('referrals', 'accept it again', 'n.thornbury', 'POST', `/api/referrals/${tour.referralToTriage}/accept`,
    { templateId: template?.id, priority: 'High', expectedVersion: referral?.version }, [409]);

  // Calendar blocks and meetings, at times the availability suggests
  const staff = await get('n.thornbury', '/api/calendar/staff');
  const nadia = staff?.find?.(s => s.username === 'n.thornbury')?.userId;
  const ben = staff?.find?.(s => s.username === 'b.sandiford')?.userId;
  const types = await get('n.thornbury', '/api/calendar/activity-types');
  const adminTime = types?.find?.(t => t.name === 'Admin time') ?? types?.[0];
  const free = await get('admin', `/api/calendar/availability?staffUserIds=${nadia}&from=${day(1)}&to=${day(10)}&durationMinutes=30`);
  const slot = free?.suggestions?.[0];
  const block = (await step('calendar', 'block off time', 'n.thornbury', 'POST', '/api/calendar/blocks',
    { staffUserId: nadia, activityTypeId: adminTime?.id, startUtc: slot?.startUtc, endUtc: slot?.endUtc, note: 'Smoke check' }, [201])).json;
  await step('calendar', 'block the same time twice', 'n.thornbury', 'POST', '/api/calendar/blocks',
    { staffUserId: nadia, activityTypeId: adminTime?.id, startUtc: slot?.startUtc, endUtc: slot?.endUtc }, [409]);
  await step('calendar', 'remove the block', 'n.thornbury', 'DELETE', `/api/calendar/blocks/${block?.blockId ?? block?.id}`, undefined, [204]);
  const both = await get('admin', `/api/calendar/availability?staffUserIds=${nadia}&staffUserIds=${ben}&from=${day(1)}&to=${day(10)}&durationMinutes=30`);
  const meetingSlot = both?.suggestions?.[1] ?? both?.suggestions?.[0];
  await step('meetings', 'arrange a meeting', 'admin', 'POST', '/api/calendar/meetings',
    { title: 'Smoke check meeting', startUtc: meetingSlot?.startUtc, endUtc: meetingSlot?.endUtc, attendeeUserIds: [nadia, ben] }, [201]);

  // Appointments
  const booked = (await get('n.thornbury', '/api/appointments/search?pageSize=100'))?.items?.find?.(a => a.status === 'Booked');
  await step('appointments', 'cancel a booked appointment', 'n.thornbury', 'POST', `/api/appointments/${booked?.id}/cancel`,
    { reason: 'Smoke check', tellPatient: false }, [200]);

  // Letters (and their PDFs)
  const letter = await get('n.thornbury', `/api/letters/${tour.letterToApprove}`);
  const preview = await step('letters', 'preview a letter (PDF)', 'n.thornbury', 'GET', `/api/letters/${tour.letterToApprove}/preview`, undefined, [200]);
  if (preview.status === 200 && !isPdf(preview)) results.at(-1).problem = `not a PDF (${preview.headers['Content-Type']})`;
  await step('letters', 'approve it', 'n.thornbury', 'POST', `/api/letters/${tour.letterToApprove}/approve`, { expectedVersion: letter?.version }, [200]);
  const templatePreview = await step('letters', "preview a template (PDF)", 'admin', 'POST', '/api/letter-templates/preview',
    { name: 'Smoke', letterhead: 'Smoke check (synthetic)', bodySkeleton: 'Dear colleague,\n\n**Re: {{patient.name}}**', closing: 'Yours sincerely', largePrint: false }, [200]);
  if (templatePreview.status === 200 && !isPdf(templatePreview)) results.at(-1).problem = 'not a PDF';

  // Results sign-off
  const result = await get('n.thornbury', `/api/results/${tour.resultToSignOff}`);
  await step('results', 'sign off a result', 'n.thornbury', 'POST', `/api/results/${tour.resultToSignOff}/sign-off`,
    { action: 'NoFurtherAction', expectedVersion: result?.version }, [200]);

  // Specimens and lab cases
  const specimen = await get('r.aldous', `/api/specimens/${tour.specimenAtLab}`);
  await step('specimens', 'the lab receives a specimen', 'r.aldous', 'POST', `/api/specimens/${tour.specimenAtLab}/received`,
    { expectedVersion: specimen?.version }, [200, 409]);
  await step('specimens', 'the worklist', 'r.aldous', 'POST', '/api/specimens/search', { page: 1, pageSize: 10 }, [200]);
  await step('specimens', 'find by number', 'r.aldous', 'POST', '/api/specimens/find', { number: specimen?.number ?? 'none' }, [200, 404]);
  await step('lab cases', 'the lab cases list', 'r.aldous', 'POST', '/api/lab-cases/search', { page: 1, pageSize: 10 }, [200]);
  await step('hospital requests', 'the hospital requests list', 'n.thornbury', 'POST', '/api/hospital-requests/search', { page: 1, pageSize: 10 }, [200]);

  // Files
  const upload = await step('attachments', 'attach a picture', 'n.thornbury', 'POST', `/api/attachments?pathwayId=${tour.relay}`, PNG, [201],
    { 'Content-Type': 'application/octet-stream', 'X-File-Name': encodeURIComponent('smoke check.png') });
  const download = await step('attachments', 'download it', 'n.thornbury', 'GET', `/api/attachments/${upload.json?.id}/content`, undefined, [200]);
  if (download.status === 200 && (download.bytes !== PNG.length || !/attachment/.test(download.headers['Content-Disposition'] ?? '')))
    results.at(-1).problem = `downloaded ${download.bytes} bytes, ${download.headers['Content-Disposition']}`;
  await step('attachments', 'an SVG is refused', 'n.thornbury', 'POST', `/api/attachments?pathwayId=${tour.relay}`, SVG, [415, 422],
    { 'Content-Type': 'application/octet-stream', 'X-File-Name': 'smoke.svg' });
  await step('attachments', 'delete it', 'n.thornbury', 'DELETE', `/api/attachments/${upload.json?.id}`, undefined, [204]);

  // Consent
  const photos = await get('n.thornbury', `/api/pathways/patient-pathways/${tour.photosToTake}`);
  const forms = await get('n.thornbury', '/api/consent-forms');
  await step('consent', 'record consent', 'n.thornbury', 'POST', `/api/pathways/${tour.photosToTake}/consent-records`,
    { stageId: photos?.stages?.find?.(s => s.needsConsent && s.canRecordConsent)?.id, consentFormId: forms?.[0]?.id, signedOn: day(0), signedBy: 'Patient' }, [201]);

  // MDT
  const items = (await get('n.thornbury', `/api/mdt/meetings/${tour.mdtToday}/items`))?.items ?? [];
  const item = items.find(i => i.id === tour.mdtItemPresented) ?? items.find(i => i.canRecordOutcome);
  await step('mdt', 'record an MDT outcome', 'n.thornbury', 'POST', `/api/mdt/items/${item?.id}/outcome`,
    { outcome: 'Discussed: proceed as planned (smoke check).', presentUserIds: [nadia], expectedVersion: item?.version }, [200]);

  // Consults
  const consults = await get('r.aldous', '/api/consults/incoming');
  const consult = consults?.find?.(c => c.id === tour.consult) ?? consults?.[0];
  await step('consults', 'the lab replies to a consult', 'r.aldous', 'POST', `/api/consults/${consult?.id}/reply`,
    { reply: 'A punch biopsy is fine here (smoke check).', expectedVersion: consult?.version }, [200]);

  // Setup pages
  await step('setup', 'add an activity type', 'admin', 'POST', '/api/calendar/activity-types', { name: 'Smoke check', colour: 'purple' }, [201]);
  await step('setup', 'add a closed day', 'admin', 'POST', '/api/calendar/closed-days', { date: day(90), name: 'Smoke check day' }, [201]);
  await step('setup', 'a clinician may not', 'n.thornbury', 'POST', '/api/calendar/activity-types', { name: 'No', colour: 'blue' }, [403]);

  // Users and roles
  await step('users', 'create an account', 'admin', 'POST', '/api/auth/users', { username: 'smoke.user' }, [200]);
  await step('users', 'give it a role', 'admin', 'PUT', '/api/auth/users/smoke.user/roles', { roles: ['Nurse'] }, [204]);
  await step('roles', 'create a role', 'admin', 'POST', '/api/roles', { name: 'Smoke checker', description: 'Smoke check', permissions: ['UseApp', 'ViewPatients'] }, [201]);

  // Privacy and data requests
  const request = await get('admin', `/api/data-requests/${tour.dataRequest}`);
  await step('privacy', 'the pack before the review', 'admin', 'GET', `/api/data-requests/${tour.dataRequest}/pack`, undefined, [409]);
  await step('privacy', 'review the record', 'admin', 'PUT', `/api/data-requests/${tour.dataRequest}/review`, { withheld: [], expectedVersion: request?.version }, [200]);
  const pack = await step('privacy', 'make the pack (a zip with PDFs)', 'admin', 'GET', `/api/data-requests/${tour.dataRequest}/pack`, undefined, [200]);
  if (pack.status === 200 && !/zip|pdf/.test(pack.headers['Content-Type'] ?? '')) results.at(-1).problem = `a ${pack.headers['Content-Type']}`;
  await step('privacy', 'export a patient', 'admin', 'GET', `/api/patients/${patient?.id}/export`, undefined, [200]);

  // Notifications, notepad, settings
  await step('notifications', 'mark them all read', 'n.thornbury', 'POST', '/api/notifications/read', undefined, [200, 204]);
  const notepad = await get('n.thornbury', '/api/me/notepad');
  await step('notepad', 'write in the notepad', 'n.thornbury', 'PUT', '/api/me/notepad', { text: 'Smoke check.', expectedVersion: notepad?.version ?? 0 }, [200]);
  await step('settings', 'save settings', 'n.thornbury', 'PUT', '/api/auth/me/settings', { settings: '{"theme":"dark"}' }, [200, 204]);

  // FHIR and the demographics lookup (not configured, as by default)
  await step('fhir', 'read a patient', 'n.thornbury', 'GET', `/api/fhir/Patient/${patient?.id}`, undefined, [200]);
  await step('fhir', 'an unknown patient', 'n.thornbury', 'GET', '/api/fhir/Patient/6f1d7c2e-0000-4000-8000-000000000001', undefined, [404]);
  await step('fhir', 'XML is refused', 'n.thornbury', 'GET', '/api/fhir/metadata?_format=xml', undefined, [406]);
  await step('demographics', 'no lookup here', 'n.thornbury', 'POST', '/api/demographics/lookup', { nhsNumber: '9990000018' }, [404]);

  // Scheduled jobs: Run now
  const jobs = await get('admin', '/api/jobs');
  for (const job of Array.isArray(jobs) ? jobs : jobs?.jobs ?? [])
    await step('jobs', `run ${job.name} now`, 'admin', 'POST', `/api/jobs/${job.name}/run`, undefined, [200]);

  if (signedIn) await send('POST', '/api/auth/logout');
  return results;
}

function ukDateOf(offsetDays) {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/London' }).format(new Date(Date.now() + offsetDays * 86400000));
}

function isPdf(response) {
  return (response.headers['Content-Type'] ?? '').startsWith('application/pdf') && response.bytes > 1000;
}

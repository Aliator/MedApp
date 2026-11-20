import {QueryClient} from '@tanstack/react-query';
import {call, startLightDemo} from '../test/lightDemo';
import {createAppRouter} from '../router';
import {accountTypeOfPerson, ADMIN, DEMO_ACCOUNTS, LAB, STAFF} from './accounts';
import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {PATHWAYS, PATIENTS, REFERRALS, TEAMS, USERS} from './ids';
import {seed} from './seed';
import {LAB_CASE_IDS} from './slices/labCases';
import {CONSENT} from './slices/clinical';
import {db} from './store';
import {today} from './time';
import {COACH_MARKS} from './coach/coachMarks';
import {TOUR, TOUR_PARTS} from './tour';

/** Every page in the app's route tree, as a pattern (`/pathways/$id` matches `/pathways/<any id>`). */
function routePatterns(): RegExp[] {
  const router = createAppRouter(new QueryClient());
  return Object.keys(router.routesByPath).map(
    (path) => new RegExp(`^${path.replace(/\/$/, '').replace(/\$[A-Za-z]+/g, '[^/]+') || ''}/?$`),
  );
}

const pathOf = (href: string) => href.split('?')[0];

/** The records a tour link opens, by the kind of page: `/pathways/<id>` is a pathway. */
const COLLECTIONS = {
  patients: 'patients',
  pathways: 'pathways',
  referrals: 'referrals',
  results: 'results',
  specimens: 'specimens',
  letters: 'letters',
  'hospital-requests': 'hospitalRequests',
} as const;

describe('the walkthrough', () => {
  const links = TOUR.flatMap((step) => (step.path ? [step.path] : []));

  it('opens only pages in the route tree', () => {
    const patterns = routePatterns();
    // The check itself can tell a page that isn't there.
    expect(patterns.some((pattern) => pattern.test('/no-such-page/here'))).toBe(false);
    for (const link of links) expect(patterns.some((pattern) => pattern.test(pathOf(link))), link).toBe(true);
  });

  it('opens only records the seed has, of the kind the page shows', () => {
    const state = seed();
    for (const link of links) {
      const [, kind, id] = pathOf(link).split('/');
      if (!id || !(kind in COLLECTIONS)) continue;
      const records = state[COLLECTIONS[kind as keyof typeof COLLECTIONS]] as {id?: string}[];
      expect(records.some((record) => record.id === id), link).toBe(true);
    }
  });

  it('opens only lab cases the seed has (MC15)', () => {
    const state = seed();
    const caseLinks = links.filter((link) => pathOf(link).startsWith('/lab/cases/'));
    expect(caseLinks.length).toBeGreaterThan(0);
    for (const link of caseLinks) expect(state.labCases.some((c) => c.id === pathOf(link).split('/')[3]), link).toBe(true);
  });

  it('asks only for the demo accounts', () => {
    for (const step of TOUR) if (step.account) expect(DEMO_ACCOUNTS.map((a) => a.username)).toContain(step.account);
  });

  it('has the staff member triage in her own team, and the admin see Insights', () => {
    const state = seed();
    const referral = state.referrals.find((r) => r.id === REFERRALS.dermatologyToTriage)!;
    expect(STAFF.teams.map((t) => t.teamId)).toContain(referral.teamId);
    expect(ADMIN.permissions).toContain('ViewInsights');
    expect(STAFF.permissions).not.toContain('ViewInsights');
  });

  it('does clinical work only as staff: the admin is read only, in no team', () => {
    const clinical = ['WorkPathways', 'TriageReferrals', 'BookAppointments', 'ProcessSpecimens', 'RecordMdtOutcomes', 'SignOffResults'] as const;
    for (const permission of clinical) expect(ADMIN.permissions).not.toContain(permission);
    expect(ADMIN.teams).toEqual([]);
    expect(accountTypeOfPerson(ADMIN)).toBe('Admin');
    expect(accountTypeOfPerson(STAFF)).toBe('ClinicAdmin');
    expect(accountTypeOfPerson(LAB)).toBe('Staff');
    const byKey = (key: string) => TOUR.find((step) => step.key === key)!;
    expect(byKey('relay-triage').account).toBe(STAFF.username);
    expect(byKey('relay-lab').account).toBe(LAB.username);
    expect(byKey('relay-back').account).toBe(STAFF.username);
    expect(LAB.permissions).toContain('AuthoriseLabReports');
    // The admin's steps are what an admin does: arranging a meeting, reading every team's list, and Insights.
    for (const step of TOUR.filter((s) => s.account === ADMIN.username)) expect(['team-calendar', 'on-hold-overdue', 'insights']).toContain(step.key);
    expect(byKey('team-walls').account).toBe(LAB.username);
  });

  it('tells the relay in three steps, its pathway four stages (the owner, 19 November 2026)', () => {
    expect(TOUR.filter((step) => step.part === 'story').map((step) => step.key)).toEqual(['relay-triage', 'relay-lab', 'relay-back']);
    expect(seed().pathways.find((p) => p.id === PATHWAYS.relay)!.stages.map((s) => s.name)).toEqual(['Triage', 'Testing', 'Results appointment', 'Refer to GP for medication']);
    expect(TOUR.map((step) => step.key)).not.toContain('timeline');
  });

  it('runs the relay: consent and Triage, the lab’s results then its own finish of Testing, then back for the appointment', async () => {
    const [triage, lab, back] = ['relay-triage', 'relay-lab', 'relay-back'].map((key) => TOUR.find((step) => step.key === key)!);
    expect(triage.whyNot!(seed())).toBeNull();
    expect(lab.whyNot!(seed())).toMatch(/Finish her Triage first/);
    expect(back.whyNot!(seed())).toMatch(/hasn’t sent it back/);

    startLightDemo(STAFF);
    const path = `/api/pathways/patient-pathways/${PATHWAYS.relay}`;
    const current = async () => (await call('GET', path)).body.stages.find((s: {isCurrent: boolean}) => s.isCurrent);
    const first = await current();
    expect(first.name).toBe('Triage');
    const finish = () => call('POST', `${path}/stages/${first.id}/finish`, {});
    // Refused until her consent is recorded.
    expect((await finish()).status).toBe(409);
    const consent = {stageId: first.id, consentFormId: CONSENT.adultForm, signedOn: today(), signedBy: 'Patient'};
    expect((await call('POST', `/api/pathways/${PATHWAYS.relay}/consent-records`, consent)).status).toBe(201);
    expect((await finish()).status).toBe(200);
    expect(triage.whyNot!(db())).toMatch(/already/);
    expect(lab.whyNot!(db())).toBeNull();

    // The lab: her case is ready for its results; authorising puts them on Testing, and the lab finishes it by hand.
    db().user = LAB.username;
    const caseUrl = `/api/lab-cases/${LAB_CASE_IDS.relay}`;
    const labCase = (await call('GET', caseUrl)).body;
    expect(labCase.canAuthorise).toBe(true);
    expect((await call('POST', `${caseUrl}/report`, {results: 'Synthetic results.', expectedVersion: labCase.version})).status).toBe(200);
    expect(lab.whyNot!(db())).toMatch(/finish Stage on Testing|Finish Stage on Testing/);
    expect(back.whyNot!(db())).toMatch(/hasn’t sent it back/);
    const testing = await current();
    expect(testing).toMatchObject({name: 'Testing', result: expect.objectContaining({text: 'Synthetic results.'})});
    expect((await call('POST', `${path}/stages/${testing.id}/finish`, {})).status).toBe(200);
    expect(lab.whyNot!(db())).toMatch(/already/);

    // Back with Dermatology, at the Results appointment, which needs a visit.
    db().user = STAFF.username;
    expect(await current()).toMatchObject({name: 'Results appointment', visitRequirement: 'Required'});
    const backFromLab = (await call('GET', '/api/pathways/patient-pathways/search?View=BackFromLab')).body.items;
    expect(backFromLab.map((p: {id: string}) => p.id)).toContain(PATHWAYS.relay);
    expect(back.whyNot!(db())).toBeNull();
  });

  it('has the first time for the admin and Nadia Thornbury clash with Bea Sandiford, and the team’s suggestions move on', async () => {
    expect(COACH_MARKS['relay-lab'].map((m) => m.text).join(' ')).toMatch(/Authorise and Open Pathway.*Finish Stage on Testing/);
    expect(COACH_MARKS['team-calendar'].map((m) => m.text).join(' ')).toMatch(/b\.sandiford and t\.hallworth/);
    startLightDemo(ADMIN);
    const availability = async (ids: string[]) =>
      (await call('GET', `/api/calendar/availability?${ids.map((id) => `StaffUserIds=${id}`).join('&')}&From=${today()}&DurationMinutes=30`)).body;
    const first = (await availability([ADMIN.id, STAFF.id])).suggestions[0];
    expect(first).toBeDefined();
    const team = await availability([ADMIN.id, STAFF.id, USERS.sandiford, USERS.hallworth]);
    const overlapsFirst = (b: {startUtc: string; endUtc: string}) => b.startUtc < first.endUtc && b.endUtc > first.startUtc;
    expect(team.busy.filter(overlapsFirst).map((b: {username: string}) => b.username)).toContain('b.sandiford');
    expect(team.suggestions.length).toBeGreaterThan(0);
    expect(team.suggestions.some(overlapsFirst)).toBe(false);
  });

  it('has the notepad step copy sample notes into the relay’s pathway, pinned in the relay, which she can add notes to', async () => {
    const step = TOUR.find((s) => s.key === 'notepad')!;
    expect(step).toMatchObject({switchTo: STAFF.username});
    const marks = COACH_MARKS.notepad;
    expect(marks.map((m) => m.text).join(' ')).toMatch(/Shift-click Notepad/);
    expect(marks.some((m) => m.target && 'css' in m.target && m.target.css === '[data-notepad-toggle]')).toBe(true);
    expect(step.path).toBeUndefined();
    expect(step.sampleText!.length).toBeGreaterThan(0);
    expect(marks.map((m) => m.text).join(' ')).toMatch(/Add Note to Pathway/);
    const pathway = seed().pathways.find((p) => p.id === PATHWAYS.relay)!;
    expect(marks.some((m) => m.path === `/pathways/${pathway.id}`)).toBe(true);
    expect(marks.map((m) => m.text).join(' ')).toMatch(/pinned under Pathways/);
    expect(COACH_MARKS['relay-triage'].some((m) => m.target && 'name' in m.target && m.target.name === 'Pin')).toBe(true);
    expect(pathway).toMatchObject({teamId: TEAMS.dermatology, status: 'InProgress', patientId: PATIENTS.leahCorrin});
    startLightDemo(STAFF);
    expect((await call('GET', `/api/pathways/patient-pathways/${pathway.id}`)).body.canAddNotes).toBe(true);
  });

  it('opens a patient to pin, and says when they are pinned already', () => {
    const pin = TOUR.find((step) => step.key === 'pin-patient')!;
    expect(pin.path).toBe(`/patients/${PATIENTS.samuelOkafor}`);
    expect(TOUR.findIndex((step) => step.key === 'pin-patient')).toBe(TOUR.findIndex((step) => step.key === 'half-view') + 1);
    try {
      appSettings.set(PINS_KEY, null);
      expect(pin.whyNot!(seed())).toBeNull();
      appSettings.set(PINS_KEY, JSON.stringify({patients: [PATIENTS.samuelOkafor], pathways: []}));
      expect(pin.whyNot!(seed())).toMatch(/already/);
    } finally {
      appSettings.set(PINS_KEY, null);
    }
  });

  it('keeps each part’s steps together, in the parts’ order, with the owner’s first steps first', () => {
    const parts = TOUR_PARTS.map((p) => p.key);
    const order = TOUR.map((step) => parts.indexOf(step.part));
    expect(order.every((n) => n >= 0)).toBe(true);
    expect(order).toEqual([...order].sort((a, b) => a - b));
    expect(new Set(TOUR.map((step) => step.part)).size).toBe(parts.length);
    expect(TOUR[0]).toMatchObject({key: 'half-view', path: '/account'});
    expect(new Set(TOUR.map((step) => step.key)).size).toBe(TOUR.length);
  });
});

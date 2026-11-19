import {QueryClient} from '@tanstack/react-query';
import {call, startLightDemo} from '../test/lightDemo';
import {createAppRouter} from '../router';
import {accountTypeOfPerson, ADMIN, DEMO_ACCOUNTS, LAB, STAFF} from './accounts';
import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {PATHWAYS, PATIENTS, REFERRALS, TEAMS} from './ids';
import {seed} from './seed';
import {LAB_CASE_IDS} from './slices/labCases';
import {CONSENT} from './slices/clinical';
import type {DemoState} from './state';
import {db} from './store';
import {TOUR} from './tour';

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

/** Ellis Varden's consent to his biopsy, recorded again (the tour's consent step). */
function validConsent(state: DemoState) {
  const withdrawn = state.consentRecords.find((c) => c.id === CONSENT.ellisWithdrawn)!;
  state.consentRecords.push({...withdrawn, id: 'ellis-again', status: 'Valid', withdrawnOn: null, consentFormId: CONSENT.adultForm});
}

describe('the Read Me tour', () => {
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

  it('opens only lab cases the seed has (MC15), and says when the case has been worked already', () => {
    const state = seed();
    const caseLinks = links.filter((link) => pathOf(link).startsWith('/lab/cases/'));
    expect(caseLinks.length).toBeGreaterThan(0);
    for (const link of caseLinks) expect(state.labCases.some((c) => c.id === pathOf(link).split('/')[3]), link).toBe(true);
    const step = TOUR.find((s) => s.key === 'lab-case')!;
    expect(step.whyNot!(state)).toBeNull();
    for (const c of state.labCases) c.status = 'Reported';
    expect(step.whyNot!(state)).toMatch(/finished this case/);
  });

  it('asks only for the three demo accounts', () => {
    for (const step of TOUR) if (step.account) expect(DEMO_ACCOUNTS.map((a) => a.username)).toContain(step.account);
  });

  it('says why a step that has been done already won’t do it again', () => {
    const state = seed();
    const byKey = (key: string) => TOUR.find((step) => step.key === key)!;
    expect(byKey('triage').whyNot!(state)).toBeNull();
    expect(byKey('finish-stage').whyNot!(state)).toBeNull();
    expect(byKey('book').whyNot!(state)).toBeNull();
    for (const key of ['sign-off', 'consent', 'branch', 'mdt']) expect(byKey(key).whyNot!(state), key).toBeNull();

    state.referrals.find((r) => r.id === REFERRALS.dermatologyToTriage)!.status = 'Accepted';
    state.labCases.find((c) => c.id === LAB_CASE_IDS.stageToReport)!.status = 'Reported';
    state.appointments.push({pathwayId: PATHWAYS.biopsyToBook, stageName: 'Take biopsy', status: 'Booked', teamId: null});
    validConsent(state);
    expect(byKey('triage').whyNot!(state)).toMatch(/already/);
    expect(byKey('consent').whyNot!(state)).toMatch(/already/);
    expect(byKey('finish-stage').whyNot!(state)).toMatch(/already/);
    expect(byKey('book').whyNot!(state)).toMatch(/already/);
  });

  it('opens this morning’s MDT, at an item still to discuss', () => {
    const state = seed();
    const mdt = TOUR.find((step) => step.key === 'mdt')!;
    const meetingId = new URLSearchParams(mdt.path!.split('?')[1]).get('meeting');
    expect(state.meetings.find((m) => m.id === meetingId)?.isMdt).toBe(true);
    expect(state.mdtItems.some((i) => i.meetingId === meetingId && i.status === 'Listed')).toBe(true);
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
    expect(byKey('lab').account).toBe(LAB.username);
    expect(byKey('mdt').account).toBe(STAFF.username);
    expect(LAB.permissions).toContain('ProcessSpecimens');
    // The admin's steps are what an admin does: reading every team's list is one (the team walls).
    for (const step of TOUR.filter((s) => s.account === ADMIN.username))
      expect(['account-types', 'draw-routes', 'dermatology-templates', 'assessment-only', 'meeting', 'insights', 'on-hold-overdue', 'lab-tests']).toContain(step.key);
    expect(byKey('team-walls').account).toBe(STAFF.username);
  });

  it('runs the relay in order: Dermatology hands Ellis Varden’s pathway over, and it is new for the lab', async () => {
    const keys = TOUR.map((step) => step.key);
    const relay = ['relay-hand-over', 'relay-arrives', 'finish-stage', 'relay-back'].map((key) => keys.indexOf(key));
    expect(relay).toEqual([...relay].sort((a, b) => a - b));
    expect(relay[3] - relay[0]).toBe(3);
    const handOver = TOUR.find((step) => step.key === 'relay-hand-over')!;
    expect(handOver.whyNot!(seed())).toBeNull();

    startLightDemo(STAFF);
    const path = `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`;
    const biopsy = (await call('GET', path)).body.stages.find((s: {isCurrent: boolean}) => s.isCurrent);
    expect(biopsy.name).toBe('Take biopsy');
    // Refused until his consent is recorded again (the tour's step before).
    expect(TOUR.findIndex((step) => step.key === 'consent')).toBe(TOUR.findIndex((step) => step.key === 'relay-hand-over') - 1);
    const finish = () => call('POST', `${path}/stages/${biopsy.id}/finish`, {withoutVisit: true, reason: 'Done in clinic'});
    expect((await finish()).status).toBe(409);
    validConsent(db());
    expect((await finish()).status).toBe(200);
    expect(handOver.whyNot!(db())).toMatch(/already/);
    db().user = LAB.username;
    const newForLab = (await call('GET', '/api/pathways/patient-pathways/search?View=NewForUs')).body.items;
    expect(newForLab.map((p: {id: string}) => p.id)).toContain(PATHWAYS.biopsyToBook);
  });

  it('has the notepad step copy sample notes into a Dermatology pathway in progress that she can add notes to', async () => {
    const step = TOUR.find((s) => s.key === 'notepad')!;
    expect(step).toMatchObject({opensNotepad: true, isNumbered: true});
    expect(step.path).toBeUndefined();
    expect(step.sampleText!.length).toBeGreaterThan(0);
    expect(step.steps.join(' ')).toMatch(/Add Note to Pathway/);
    const pathway = seed().pathways.find((p) => p.id === PATHWAYS.followUpBooked)!;
    expect(step.steps.join(' ')).toContain(pathway.title);
    expect(pathway).toMatchObject({teamId: TEAMS.dermatology, status: 'InProgress', patientId: PATIENTS.ilonaPetrova});
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
});

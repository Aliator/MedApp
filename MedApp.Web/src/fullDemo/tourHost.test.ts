import {COACH_MARKS} from '../demo/coach/coachMarks';
import {PATHWAYS, PATIENTS, TEAMS} from '../demo/ids';
import {LAB_CASE_IDS} from '../demo/slices/labCases';
import {TOUR} from '../demo/tour';
import {FULL_DEMO_VERSION, fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {json, mockApi, pageOf} from '../test/render';
import {FULL_DEMO_CHECKS, resolvePath, TOUR_RECORDS} from './tourHost';

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

/** `db-version.json`'s `tour` keys from the demo database's build (tools/MedApp.DevMcp/DemoData's `seed.Tour`). */
const SEEDED_TOUR_KEYS = ['relay', 'labCaseRelay', 'onHold', 'unassigned', 'referralToTriage', 'gpReport', 'mdtToday', 'dataRequest'];

describe('the walkthrough in the full demo', () => {
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => resetFullDemoForTests());

  it('knows where every record the steps open is in the full demo’s database', () => {
    const paths = [...TOUR.map((step) => step.path), ...Object.values(COACH_MARKS).flatMap((marks) => marks.map((mark) => mark.path))];
    const ids = new Set(paths.flatMap((path) => path?.match(GUID) ?? []).map((id) => id.toLowerCase()));
    expect(ids.size).toBeGreaterThan(0);
    for (const id of ids) expect(TOUR_RECORDS[id], `no full demo record for ${id}`).toBeDefined();
  });

  it('takes the records the seed names from db-version.json, and looks up the others', () => {
    for (const record of Object.values(TOUR_RECORDS)) {
      if (SEEDED_TOUR_KEYS.includes(record.tour)) continue;
      expect(record.patient ?? record.teamOfPathway, `${record.tour} can't be found`).toBeDefined();
      if (record.teamOfPathway) expect(SEEDED_TOUR_KEYS).toContain(record.teamOfPathway);
    }
    expect(TOUR_RECORDS[PATHWAYS.relay].tour).toBe('relay');
    expect(TOUR_RECORDS[LAB_CASE_IDS.relay].tour).toBe('labCaseRelay');
  });

  it('swaps the light demo’s IDs in a step’s path for the full demo’s', async () => {
    fullDemoReady();
    const fetch = mockApi({
      'GET /api/patients/search': () => json(pageOf([{id: 'samuel-full'}])),
      'GET /api/pathways/patient-pathways/*': () => json({id: FULL_DEMO_VERSION.tour.relay, teamId: 'dermatology-full'}),
    });
    expect(await resolvePath(`/pathways/${PATHWAYS.relay}`)).toBe(`/pathways/${FULL_DEMO_VERSION.tour.relay}`);
    expect(await resolvePath(`/lab/cases/${LAB_CASE_IDS.relay}`)).toBe(`/lab/cases/${FULL_DEMO_VERSION.tour.labCaseRelay}`);
    expect(await resolvePath(`/patients/${PATIENTS.samuelOkafor}`)).toBe('/patients/samuel-full');
    expect(await resolvePath(`/calendar?view=team&team=${TEAMS.dermatology}`)).toBe('/calendar?view=team&team=dermatology-full');
    expect(await resolvePath('/account')).toBe('/account');
    const search = new URL((fetch.mock.calls[0][0] as Request).url);
    expect(search.searchParams.get('FirstName')).toBe('Samuel');
    expect(search.searchParams.get('LastName')).toBe('Okafor');
  });

  it('checks the relay’s steps against the pathway and the lab case', async () => {
    fullDemoReady();
    let triageDone = false;
    let caseStatus = 'Open';
    let pathwayStatus = 200;
    mockApi({
      'GET /api/pathways/patient-pathways/*': () =>
        json(
          {stages: [{name: 'Triage', completedAtUtc: triageDone ? '2026-11-23T10:00:00Z' : null}, {name: 'Testing', completedAtUtc: null}]},
          pathwayStatus,
        ),
      'GET /api/lab-cases/*': () => json({status: caseStatus}),
    });
    expect(await FULL_DEMO_CHECKS['relay-triage']()).toBeNull();
    expect(await FULL_DEMO_CHECKS['relay-back']()).toMatch(/hasn’t sent it back yet/);

    pathwayStatus = 404;
    expect(await FULL_DEMO_CHECKS['relay-lab']()).toMatch(/Finish her Triage first/);
    pathwayStatus = 200;
    triageDone = true;
    expect(await FULL_DEMO_CHECKS['relay-triage']()).toMatch(/finished her Triage already/);
    expect(await FULL_DEMO_CHECKS['relay-lab']()).toBeNull();
    caseStatus = 'Reported';
    expect(await FULL_DEMO_CHECKS['relay-lab']()).toMatch(/entered the results already/);
  });

  it('checks whether Samuel Okafor is pinned already', async () => {
    fullDemoReady();
    mockApi({'GET /api/patients/search': () => json(pageOf([{id: 'samuel-full'}]))});
    expect(await FULL_DEMO_CHECKS['pin-patient']()).toBeNull();
    localStorage.setItem('medapp.pins', JSON.stringify({patients: ['samuel-full']}));
    const {appSettings} = await import('../app/settingsStore');
    appSettings.reset();
    expect(await FULL_DEMO_CHECKS['pin-patient']()).toMatch(/pinned Samuel Okafor already/);
  });
});

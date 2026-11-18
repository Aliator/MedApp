import {QueryClient} from '@tanstack/react-query';
import {createAppRouter} from '../router';
import {ADMIN, DEMO_ACCOUNTS, STAFF} from './accounts';
import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {PATHWAYS, PATIENTS, REFERRALS} from './ids';
import {seed} from './seed';
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
} as const;

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

  it('asks only for the two demo accounts', () => {
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
    state.pathways.find((p) => p.id === PATHWAYS.histologyToFinish)!.stages[2].completedAtUtc = '2026-11-18T10:00:00.000Z';
    state.appointments.push({pathwayId: PATHWAYS.biopsyToBook, stageName: 'Biopsy or sample', status: 'Booked'});
    expect(byKey('triage').whyNot!(state)).toMatch(/already/);
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
    expect(STAFF.teamIds).toContain(referral.teamId);
    expect(ADMIN.permissions).toContain('ViewInsights');
    expect(STAFF.permissions).not.toContain('ViewInsights');
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

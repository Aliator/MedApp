import {useQuery} from '@tanstack/react-query';
import {api, unwrap} from '../api/client';
import type {TourHost} from '../demo/coach/tourHost';
import {PATHWAYS, PATIENTS, TEAMS} from '../demo/ids';
import {LAB_CASE_IDS} from '../demo/slices/labCases';
import {isPatientPinned, type TourStep} from '../demo/tour';
import {accountLabel} from './accounts';
import {resetFullDemo, switchAccount} from './session';
import {startupState} from './startup';

/**
 * Where one of the walkthrough's records is in the full demo's database: its `tour` key in `db-version.json` (the
 * seed's IDs: `tools/MedApp.DevMcp/DemoData`), and, for a record the seed doesn't name there, how to find it through
 * the API instead (a patient by name; a team by the pathway it holds).
 */
export interface FullDemoRecord {
  tour: string;
  patient?: {firstName: string; lastName: string};
  /** The `tour` key of a pathway the team holds. */
  teamOfPathway?: string;
}

/**
 * The light demo's IDs in `TOUR`'s and `COACH_MARKS`' paths (`src/demo/ids.ts`), and the full demo's record for each
 * (`tourHost.test.ts` checks every ID in those paths is here). `relay` and `labCaseRelay` are `db-version.json`'s
 * keys; the two patients and the team aren't keys there (yet), so they're looked up.
 */
export const TOUR_RECORDS: Record<string, FullDemoRecord> = {
  [PATHWAYS.relay]: {tour: 'relay'},
  [LAB_CASE_IDS.relay]: {tour: 'labCaseRelay'},
  [PATIENTS.samuelOkafor]: {tour: 'samuelOkafor', patient: {firstName: 'Samuel', lastName: 'Okafor'}},
  [PATIENTS.tobiasLund]: {tour: 'tobiasLund', patient: {firstName: 'Tobias', lastName: 'Lund'}},
  [TEAMS.dermatology]: {tour: 'dermatology', teamOfPathway: 'relay'},
};

const tourIds = () => startupState().version?.tour ?? {};

/** The record's ID in the full demo's database, or null when it can't be found (or the signed-in person can't see it). */
export async function resolveRecord(record: FullDemoRecord): Promise<string | null> {
  const id = tourIds()[record.tour];
  if (id) return id;
  if (record.patient) {
    const page = await unwrap(
      api.GET('/api/patients/search', {params: {query: {FirstName: record.patient.firstName, LastName: record.patient.lastName, PageSize: 2}}}),
    );
    return page.items?.[0]?.id ?? null;
  }
  const pathwayId = record.teamOfPathway ? tourIds()[record.teamOfPathway] : undefined;
  if (pathwayId) {
    const pathway = await unwrap(api.GET('/api/pathways/patient-pathways/{id}', {params: {path: {id: pathwayId}}}));
    return pathway.teamId ?? null;
  }
  return null;
}

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

/**
 * A walkthrough path with the light demo's IDs swapped for the full demo's records' (looked up as the signed-in
 * person). An ID that isn't one of the tour's, or can't be found, stays as it is (its page then says it's not found).
 */
export async function resolvePath(path: string): Promise<string> {
  const ids = [...new Set(path.match(GUID) ?? [])];
  let resolved = path;
  for (const id of ids) {
    const record = TOUR_RECORDS[id.toLowerCase()];
    const found = record ? await resolveRecord(record).catch(() => null) : null;
    if (found) resolved = resolved.split(id).join(found);
  }
  return resolved;
}

const RESET_HINT = 'Reset the Demo, below, brings it back.';

type Pathway = Awaited<ReturnType<typeof readPathway>>;

async function readPathway(key: string) {
  const id = tourIds()[key];
  if (!id) return null;
  const result = await api.GET('/api/pathways/patient-pathways/{id}', {params: {path: {id}}});
  return result.response.status === 404 ? null : unwrap(Promise.resolve(result));
}

const stageDone = (pathway: Pathway, name: string) => !!pathway?.stages?.find((s) => s.name === name)?.completedAtUtc;

/**
 * The full demo's own checks of the steps the light demo checks against its state (`TOUR`'s `whyNot`), read from the
 * API: whether Samuel Okafor is pinned, and how far the relay has got. The relay's last step no longer checks whether
 * the results appointment is booked (it would need a search of the pathway's appointments): booking a second says so.
 */
export const FULL_DEMO_CHECKS: Record<string, () => Promise<string | null>> = {
  'pin-patient': async () => {
    const id = await resolveRecord(TOUR_RECORDS[PATIENTS.samuelOkafor]);
    return id && isPatientPinned(id)
      ? `You've pinned Samuel Okafor already: he's under Patients in the side nav. Unpin him from his row's menu (the burger at the end of the row) to try again.`
      : null;
  },
  'relay-triage': async () =>
    stageDone(await readPathway('relay'), 'Triage')
      ? `You've finished her Triage already in your copy of the demo: the pathway is with the lab. ${RESET_HINT}`
      : null,
  'relay-lab': async () => {
    // Until Triage is finished the pathway isn't with the lab, which can't see it (a 404 across the team wall).
    const pathway = await readPathway('relay');
    if (stageDone(pathway, 'Testing')) return `You've sent it back already in your copy of the demo: the pathway is with Dermatology. ${RESET_HINT}`;
    const caseId = tourIds().labCaseRelay;
    if (caseId) {
      const result = await api.GET('/api/lab-cases/{id}', {params: {path: {id: caseId}}});
      if (result.data && result.data.status !== 'Open')
        return 'You’ve entered the results already: open her pathway and choose Finish Stage on Testing to send it back.';
    }
    return stageDone(pathway, 'Triage') ? null : 'Finish her Triage first (the step before): until then the pathway isn’t with the lab.';
  },
  'relay-back': async () => (stageDone(await readPathway('relay'), 'Testing') ? null : 'The lab hasn’t sent it back yet: do the step before first.'),
};

/** The walkthrough in the full demo: its records looked up in the database, its checks read from the API. */
export const fullTourHost: TourHost = {
  usePage(path) {
    const query = useQuery({
      queryKey: ['fullDemoTour', 'page', path ?? ''],
      queryFn: () => resolvePath(path!),
      enabled: path !== undefined,
      staleTime: Infinity,
    });
    if (path === undefined) return undefined;
    return query.data ?? (query.isError ? path : null);
  },
  useWhyStepCantOpen(step: TourStep, isAsked: boolean) {
    const check = FULL_DEMO_CHECKS[step.key];
    const query = useQuery({
      queryKey: ['fullDemoTour', 'why', step.key],
      queryFn: () => check!(),
      enabled: isAsked && check !== undefined,
      staleTime: 0,
    });
    return isAsked ? (query.data ?? null) : null;
  },
  switchAccount: (username, path) => switchAccount(username, path, resolvePath),
  resetDemo: () => resetFullDemo('/'),
  accountLabel: (username) => {
    const account = startupState().version?.accounts.find((a) => a.username === username);
    return account && accountLabel(account);
  },
};

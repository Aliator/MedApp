import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {ApiError} from './errors';
import {api, unwrap, type Schemas} from './client';
import {hospitalRequestKeys} from './hospitals';
import {pathwayKeys} from './pathways';
import {resultKeys} from './results';
import {specimenKeys} from './specimens';

/**
 * Lab cases (MC15 of `docs/multi-clinic-plan.md`): the lab's unit of work, one request's samples, its steps and one
 * report, for a clinic's samples and the hospital's alike. A case is worked in order: Accessioned, the choice "Sample
 * adequate?", the test's steps, the choice "Needs more work?" (extra tests or a second opinion add their steps, then it's
 * asked again), then the results are entered and authorised (`AuthoriseLabReports`). The lab member records each
 * outcome; MedApp never chooses or suggests one. Every change carries the version shown (`expectedVersion`).
 */
export type LabCase = Schemas['LabCaseResponse'];
export type LabCaseStep = Schemas['LabCaseStepResponse'];
export type LabCaseSummary = Schemas['LabCaseSummaryResponse'];
export type LabCaseSearch = Schemas['LabCaseSearchRequest'];
export type LabCaseSurgeryDetails = Schemas['LabCaseSurgeryDetailsResponse'];
export type SurgeryDetailsRequest = Schemas['AddSurgeryDetailsRequest'];

/**
 * The lab cases page's tabs, by the names the API's `view` takes (At risk and Overdue: MC19's clocks; Awaiting surgery
 * details: MC20's hospital cases whose report has gone back).
 */
export const LAB_CASE_VIEWS = ['Open', 'AtRisk', 'Overdue', 'ToReport', 'Reported', 'AwaitingSurgeryDetails', 'RepeatRequested', 'All'] as const;
export type LabCaseView = (typeof LAB_CASE_VIEWS)[number];

const VIEW_LABELS: Record<LabCaseView, string> = {
  Open: 'Open',
  AtRisk: 'At risk',
  Overdue: 'Breached',
  ToReport: 'To report',
  Reported: 'Reported',
  AwaitingSurgeryDetails: 'Awaiting surgery details',
  RepeatRequested: 'Repeat requested',
  All: 'All',
};

export const labCaseViewLabel = (view: LabCaseView) => VIEW_LABELS[view];

/** A choice's outcomes, by the names the API takes, in words. */
const OUTCOME_LABELS: Record<string, string> = {
  Adequate: 'Adequate',
  Insufficient: 'Insufficient – repeat needed',
  NoMoreWork: 'No further work',
  ExtraTests: 'Extra tests (special stains)',
  SecondOpinion: 'Send for second opinion',
};

export const labOutcomeLabel = (outcome: string | null | undefined) => OUTCOME_LABELS[outcome ?? ''] ?? outcome ?? '';

/** The limits the API checks. */
export const MAX_LAB_DETAIL = 500;
export const MAX_SECOND_OPINION_WHERE = 200;
export const MAX_LAB_RESULTS = 4000;
export const MAX_SURGERY_PROCEDURE = 200;
export const MAX_SURGERY_TEXT = 500;

export const labCaseKeys = {
  all: ['lab-cases'] as const,
  one: (id: string) => ['lab-cases', id] as const,
  search: (search: LabCaseSearch) => ['lab-cases', 'search', search] as const,
  count: ['lab-cases', 'count'] as const,
};

/** One lab case (`GET api/lab-cases/{id}`): opening it is recorded as viewing it. */
export function labCaseQuery(id: string) {
  return queryOptions({
    queryKey: labCaseKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/lab-cases/{id}', {params: {path: {id}}})),
  });
}

/** A page of lab cases (`POST api/lab-cases/search`), so a searched case or NHS number stays out of URLs. */
export function labCaseSearchQuery(search: LabCaseSearch) {
  const body = Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined && v !== null && v !== '')) as LabCaseSearch;
  return queryOptions({
    queryKey: labCaseKeys.search(body),
    queryFn: () => unwrap(api.POST('/api/lab-cases/search', {body})),
    placeholderData: keepPreviousData,
  });
}

/** The home page's counts of open cases at risk, overdue and ready to report (`GET api/lab-cases/count`): names no patients, records nothing. */
export const labCaseCountQuery = queryOptions({
  queryKey: labCaseKeys.count,
  queryFn: () => unwrap(api.GET('/api/lab-cases/count')),
});

/** After a change: the case as answered, and every list it shows in (its samples, results, hospital request and pathway too). */
function useRefresh(id?: string) {
  const queryClient = useQueryClient();
  return {
    onSuccess: async (labCase: LabCase) => {
      if (labCase.id) queryClient.setQueryData(labCaseKeys.one(labCase.id), labCase);
      await Promise.all([
        queryClient.invalidateQueries({queryKey: labCaseKeys.all, predicate: (q) => q.queryKey[1] !== labCase.id}),
        queryClient.invalidateQueries({queryKey: specimenKeys.all}),
        queryClient.invalidateQueries({queryKey: resultKeys.all}),
        queryClient.invalidateQueries({queryKey: hospitalRequestKeys.all}),
        // Authorising finishes a pathway's lab stage, and receiving picks it up (MC16).
        queryClient.invalidateQueries({queryKey: pathwayKeys.all}),
      ]);
    },
    onError: (error: unknown) =>
      id && error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: labCaseKeys.one(id)}) : undefined,
  };
}

/** Opens a case by hand for a received sample with none, with the test the lab chooses. */
export function useOpenLabCase() {
  return useMutation({
    mutationFn: (body: {specimenId: string; labTestId: string; expectedVersion: number}) => unwrap(api.POST('/api/lab-cases', {body})),
    ...useRefresh(),
  });
}

/** The changes to a case: a step, a choice's outcome, the report, and entered in error. */
export function useLabCaseActions(id: string) {
  const refresh = useRefresh(id);
  const path = {params: {path: {id}}};
  return {
    step: useMutation({
      mutationFn: (body: {testStepId: string | null; fixedStep: string | null; atUtc?: string; detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/lab-cases/{id}/steps', {...path, body: {...body, atUtc: body.atUtc ?? null, detail: body.detail.trim() || null}})),
      ...refresh,
    }),
    outcome: useMutation({
      mutationFn: (body: {choice: string; outcome: string; detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/lab-cases/{id}/outcomes', {...path, body: {...body, detail: body.detail.trim() || null}})),
      ...refresh,
    }),
    report: useMutation({
      mutationFn: (body: {results: string; expectedVersion: number}) => unwrap(api.POST('/api/lab-cases/{id}/report', {...path, body})),
      ...refresh,
    }),
    stepEnteredInError: useMutation({
      mutationFn: ({stepId, ...body}: {stepId: string; detail: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/lab-cases/{id}/steps/{stepId}/entered-in-error', {params: {path: {id, stepId}}, body})),
      ...refresh,
    }),
    enteredInError: useMutation({
      mutationFn: (body: {detail: string; expectedVersion: number}) => unwrap(api.POST('/api/lab-cases/{id}/entered-in-error', {...path, body})),
      ...refresh,
    }),
    /** MC20: the hospital's surgery details, as written; closes a case awaiting them. */
    surgeryDetails: useMutation({
      mutationFn: (body: SurgeryDetailsRequest) => unwrap(api.POST('/api/lab-cases/{id}/surgery-details', {...path, body})),
      ...refresh,
    }),
  };
}

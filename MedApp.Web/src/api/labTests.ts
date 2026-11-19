import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * The lab's test catalogue (MC13, `api/lab-tests`, holders of ManageSetup): each test's name and kind, its SNOMED CT
 * procedure code, its time limits in calendar days (the target, the hard limit, and how many days before the due date
 * it's at risk) and its ordered steps (each standard, extra tests, sent for or back from a second opinion, and
 * optional or not). One used by a lab case is archived, never deleted (`canBeDeleted`). MedApp never suggests or
 * picks a test (decision 191).
 */
export type LabTest = Schemas['LabTestResponse'];
export type LabTestStep = Schemas['LabTestStepResponse'];

/** A test's kinds, by the API's names (stored numbers `LabTestKind` 0 to 5, never renumbered). */
export const LAB_TEST_KINDS = ['Histology', 'Cytology', 'Haematology', 'ClinicalChemistry', 'Microbiology', 'Other'] as const;
export type LabTestKind = (typeof LAB_TEST_KINDS)[number];

const KIND_LABELS: Record<LabTestKind, string> = {
  Histology: 'Histology',
  Cytology: 'Cytology',
  Haematology: 'Haematology',
  ClinicalChemistry: 'Clinical chemistry',
  Microbiology: 'Microbiology',
  Other: 'Other',
};

/** A kind in words: "ClinicalChemistry" → "Clinical chemistry". */
export const labTestKindLabel = (kind: string | null | undefined) => KIND_LABELS[kind as LabTestKind] ?? kind ?? '';

/** A step's kinds, by the API's names (stored numbers `LabStepKind` 0 to 3, never renumbered). */
export const LAB_STEP_KINDS = ['Standard', 'ExtraTests', 'SentForSecondOpinion', 'SecondOpinionReturned'] as const;
export type LabStepKind = (typeof LAB_STEP_KINDS)[number];

const STEP_KIND_LABELS: Record<LabStepKind, string> = {
  Standard: 'Standard',
  ExtraTests: 'Extra tests',
  SentForSecondOpinion: 'Sent for a second opinion',
  SecondOpinionReturned: 'Second opinion back',
};

/** A step's kind in words: "SentForSecondOpinion" → "Sent for a second opinion". */
export const labStepKindLabel = (kind: string | null | undefined) => STEP_KIND_LABELS[kind as LabStepKind] ?? kind ?? '';

/** The limits the API checks: days 1 to 90 (target ≤ limit), at risk 0 to 30 (under the target), up to 20 steps. */
export const LAB_TEST_LIMITS = {minDays: 1, maxDays: 90, maxAtRiskDays: 30, maxSteps: 20, maxName: 100, maxSnomedCode: 18} as const;

/** A new test's defaults: the RCPath histopathology turnaround (7 days, 10 at most), at risk 2 days before. */
export const LAB_TEST_DEFAULTS = {targetDays: 7, limitDays: 10, atRiskDays: 2} as const;

export const labTestKeys = {
  all: ['lab-tests'] as const,
  list: (includeArchived: boolean) => ['lab-tests', includeArchived] as const,
};

/** The tests by name; archived ones only with `includeArchived` (the setup page). */
export function labTestsQuery(includeArchived = false) {
  return queryOptions({
    queryKey: labTestKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET('/api/lab-tests', {params: {query: {includeArchived}}})),
  });
}

/** What the setup page sends for a test: its steps in order. */
export interface LabTestInput {
  id?: string;
  name: string;
  kind: LabTestKind;
  snomedCode: string | null;
  targetDays: number;
  limitDays: number;
  atRiskDays: number;
  steps: {name: string; kind: LabStepKind; isOptional: boolean}[];
}

/** Adds a test (no `id`) or changes one (its steps replaced in the order sent). A name taken is a 409. */
export function useSaveLabTest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({id, ...body}: LabTestInput) =>
      id ? unwrap(api.PUT('/api/lab-tests/{id}', {params: {path: {id}}, body})) : unwrap(api.POST('/api/lab-tests', {body})),
    onSuccess: () => queryClient.invalidateQueries({queryKey: labTestKeys.all}),
  });
}

/** Archive, restore and delete. Deleting one a lab case used is a 409 (its `canBeDeleted` says so). */
export function useLabTestActions() {
  const queryClient = useQueryClient();
  const onSuccess = () => queryClient.invalidateQueries({queryKey: labTestKeys.all});
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/lab-tests/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/lab-tests/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/lab-tests/{id}', path(id))), onSuccess}),
  };
}

/** A test's time limits in words: "Target 7 days, limit 10, at risk 2 days before". */
export const labTestTimesWords = (test: Pick<LabTest, 'targetDays' | 'limitDays' | 'atRiskDays'>) =>
  `Target ${test.targetDays ?? 0} ${test.targetDays === 1 ? 'day' : 'days'}, limit ${test.limitDays ?? 0}, at risk ${test.atRiskDays ?? 0} ${test.atRiskDays === 1 ? 'day' : 'days'} before`;

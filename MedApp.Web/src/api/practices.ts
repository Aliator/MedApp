import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {oneLineAddress} from '../utils/contactDetails';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';

/**
 * The GP practice directory (docs/centre-phases-3-5-plan.md, decision 79): practices patients are registered with and
 * referrals come from, each with an optional ODS code (unique when given), address, phone and secure email for
 * letters. Anyone who can view patients reads it; holders of EditPatients keep it. One in use (a patient or referral
 * has it) is archived, not deleted: it can't be newly chosen, but those that have it keep it.
 */
export type Practice = Schemas['PracticeResponse'];
/** A practice as a patient's page shows it: name, code, address, phone and whether it's archived. */
export type PracticeSummary = Schemas['PracticeSummaryResponse'];

/** The all-zero ID: on a patient's PATCH, it removes their practice (null leaves it as it is). */
export const NO_PRACTICE_ID = '00000000-0000-0000-0000-000000000000';

/** Every practice query is under ['practices'], so one change refreshes them all. */
export const practiceKeys = {
  all: ['practices'] as const,
  list: (includeArchived: boolean) => ['practices', 'list', includeArchived ? 'all' : 'active'] as const,
};

/** Every practice by name: only the ones not archived, or archived ones too (they carry `isArchived`). */
export function practicesQuery(includeArchived: boolean) {
  return queryOptions({
    queryKey: practiceKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET('/api/practices', {params: {query: {includeArchived}}})),
  });
}

/** A practice's details as a form edits them: name, ODS code, three address lines, postcode, phone and secure email. */
export interface PracticeInput {
  name: string;
  odsCode: string;
  /** Always three, some empty. */
  addressLines: [string, string, string];
  postcode: string;
  phone: string;
  secureEmail: string;
}

export const EMPTY_PRACTICE: PracticeInput = {name: '', odsCode: '', addressLines: ['', '', ''], postcode: '', phone: '', secureEmail: ''};

/** A practice's details as the form starts with them. */
export function practiceInput(practice: Practice | null): PracticeInput {
  if (!practice) return EMPTY_PRACTICE;
  const lines = practice.addressLines ?? [];
  return {
    name: practice.name ?? '',
    odsCode: practice.odsCode ?? '',
    addressLines: [lines[0] ?? '', lines[1] ?? '', lines[2] ?? ''],
    postcode: practice.postcode ?? '',
    phone: practice.phone ?? '',
    secureEmail: practice.secureEmail ?? '',
  };
}

/** What the form sends: blank address lines left out, and anything else blank as none (the API removes it). */
export function toPracticeRequest(input: PracticeInput) {
  const optional = (text: string) => text.trim() || null;
  return {
    name: input.name.trim(),
    odsCode: optional(input.odsCode),
    addressLines: input.addressLines.map((line) => line.trim()).filter(Boolean),
    postcode: optional(input.postcode),
    phone: optional(input.phone),
    secureEmail: optional(input.secureEmail),
  };
}

/** A practice's name with its ODS code, as pickers offer it: "Testbrook Surgery (ZZZ123)"; "(archived)" after one archived. */
export function practiceLabel(practice: Pick<Practice, 'name' | 'odsCode' | 'isArchived'>): string {
  const code = practice.odsCode ? ` (${practice.odsCode})` : '';
  return `${practice.name ?? ''}${code}${practice.isArchived ? ' (archived)' : ''}`;
}

/** A practice's address on one line, its postcode last; empty for none. */
export const practiceAddress = (practice: Pick<Practice, 'addressLines' | 'postcode'>) => oneLineAddress(practice.addressLines, practice.postcode);

/**
 * The practices a picker offers: every one not archived, by name, and the one already chosen (`currentId`) even if it's
 * archived since, marked so (it can be kept, but not newly chosen).
 */
export function practiceChoices(practices: readonly Practice[], currentId?: string | null): Practice[] {
  return practices
    .filter((p) => !p.isArchived || (currentId && p.id === currentId))
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
}

/**
 * What a change refreshes: the directory, and patients and referrals (a patient's page shows their practice). After a
 * 409 (someone changed it meanwhile, or the ODS code is taken) the directory is read again too, so the page shows
 * what's there now, with its new version.
 */
function useRefreshPractices() {
  const queryClient = useQueryClient();
  const onSuccess = async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: practiceKeys.all}),
      queryClient.invalidateQueries({queryKey: ['patients']}),
    ]);
  };
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? onSuccess() : undefined);
  return {onSuccess, onError};
}

/**
 * Adds a practice (no `id`) or replaces one's details from the version the person saw (`expectedVersion`), returning
 * it. The API's 400 names `Name`, `OdsCode`, `AddressLines`, `Postcode`, `Phone` or `SecureEmail`; an ODS code another
 * practice has, or a change made meanwhile, is a 409.
 */
export function useSavePractice() {
  const refresh = useRefreshPractices();
  return useMutation({
    mutationFn: ({id, expectedVersion, input}: {id?: string; expectedVersion?: number; input: PracticeInput}) => {
      const body = toPracticeRequest(input);
      return id
        ? unwrap(api.PUT('/api/practices/{id}', {params: {path: {id}}, body: {...body, expectedVersion}}))
        : unwrap(api.POST('/api/practices', {body}));
    },
    ...refresh,
  });
}

/** A practice and the version the person saw, for archiving or restoring it. */
export interface PracticeVersion {
  id: string;
  expectedVersion: number;
}

/**
 * Archive, restore and delete. Archiving and restoring send the version shown, so a change made meanwhile is a 409;
 * only a practice no patient or referral has can be deleted (`canBeDeleted`; a 409 otherwise).
 */
export function usePracticeActions() {
  const refresh = useRefreshPractices();
  return {
    archive: useMutation({
      mutationFn: ({id, expectedVersion}: PracticeVersion) =>
        unwrap(api.POST('/api/practices/{id}/archive', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    restore: useMutation({
      mutationFn: ({id, expectedVersion}: PracticeVersion) =>
        unwrap(api.POST('/api/practices/{id}/restore', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE('/api/practices/{id}', {params: {path: {id}}})),
      ...refresh,
    }),
  };
}

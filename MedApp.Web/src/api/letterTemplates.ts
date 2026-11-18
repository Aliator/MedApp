import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {teamKeys} from './teams';

/**
 * Letter templates (docs/centre-phases-3-5-plan.md, decision 99): the letterhead, body and closing a letter starts
 * from, for one team or every team, each for a kind of letter. Their text may hold merge fields
 * (`{{patient.name}}`), which copy identity and administrative details only. Anyone can read them; holders of
 * ManageSetup keep them. One no letter has been written from can be deleted (`canBeDeleted`); the rest are archived,
 * as letters keep their text.
 */
export type LetterTemplate = Schemas['LetterTemplateResponse'];
/** A merge field a template's text may hold, written `{{name}}`, with its label, where it comes from, and a made-up example. */
export type LetterMergeField = Schemas['LetterMergeFieldResponse'];

/** What letters from a template are for, by name, as the API sends them. */
export type LetterKind = 'ClinicLetter' | 'DischargeSummary' | 'ReferralResponse' | 'InformationRequest' | 'AppointmentLetter' | 'Other';

/** Each kind in words, in the order pickers offer them. */
export const LETTER_KIND_LABELS: Record<LetterKind, string> = {
  ClinicLetter: 'Clinic letter',
  DischargeSummary: 'Discharge summary',
  ReferralResponse: 'Referral response',
  InformationRequest: 'Request for more information',
  AppointmentLetter: 'Appointment letter',
  Other: 'Other',
};

/** A kind in words; one this copy of the app doesn't know yet as the API named it. */
export const letterKindLabel = (kind: string | null | undefined) => LETTER_KIND_LABELS[kind as LetterKind] ?? kind ?? '';

/** The team a template is for, in words: its name, or "All teams" for one for every team. */
export const letterTemplateTeam = (template: Pick<LetterTemplate, 'teamId' | 'teamName'>) =>
  template.teamId ? (template.teamName ?? 'Unknown team') : 'All teams';

/**
 * The API's order: templates for every team first, then by team name, then by name. For sorting a list after
 * filtering it.
 */
export function compareLetterTemplates(a: LetterTemplate, b: LetterTemplate): number {
  const byName = (x: string | null | undefined, y: string | null | undefined) => (x ?? '').localeCompare(y ?? '', undefined, {sensitivity: 'base'});
  const forAll = (t: LetterTemplate) => (t.teamId ? 1 : 0);
  return forAll(a) - forAll(b) || byName(a.teamName, b.teamName) || byName(a.name, b.name);
}

/** Every letter template query is under ['letter-templates'], so one change refreshes them all. */
export const letterTemplateKeys = {
  all: ['letter-templates'] as const,
  list: (includeArchived: boolean) => ['letter-templates', 'list', includeArchived ? 'all' : 'active'] as const,
  one: (id: string) => ['letter-templates', 'one', id] as const,
  mergeFields: ['letter-templates', 'merge-fields'] as const,
};

/** The templates in the API's order: only those not archived, or archived ones too (they carry `isArchived`). */
export function letterTemplatesQuery(includeArchived: boolean) {
  return queryOptions({
    queryKey: letterTemplateKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET('/api/letter-templates', {params: {query: {includeArchived}}})),
  });
}

/** One template. An unknown one is an ApiError with status 404. */
export function letterTemplateQuery(id: string) {
  return queryOptions({
    queryKey: letterTemplateKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/letter-templates/{id}', {params: {path: {id}}})),
  });
}

/** Every merge field, in the order the template page lists them. They're fixed, so they're read once. */
export const letterMergeFieldsQuery = queryOptions({
  queryKey: letterTemplateKeys.mergeFields,
  queryFn: () => unwrap(api.GET('/api/letter-templates/merge-fields')),
  staleTime: Infinity,
});

/** A template's details as the editor holds them. `teamId` is null for every team. */
export interface LetterTemplateInput {
  name: string;
  kind: LetterKind;
  teamId: string | null;
  letterhead: string;
  bodySkeleton: string;
  closing: string;
}

/** A new template's details, as the editor starts. */
export const EMPTY_LETTER_TEMPLATE: LetterTemplateInput = {name: '', kind: 'ClinicLetter', teamId: null, letterhead: '', bodySkeleton: '', closing: ''};

/** A saved template's details as the editor starts with them. */
export function letterTemplateInput(template: LetterTemplate | null): LetterTemplateInput {
  if (!template) return EMPTY_LETTER_TEMPLATE;
  return {
    name: template.name ?? '',
    kind: (template.kind as LetterKind | null | undefined) ?? 'ClinicLetter',
    teamId: template.teamId ?? null,
    letterhead: template.letterhead ?? '',
    bodySkeleton: template.bodySkeleton ?? '',
    closing: template.closing ?? '',
  };
}

/**
 * What a change refreshes: the templates, and the teams (each counts its letter templates). After a 409 (the
 * name is taken, or someone changed it meanwhile) they're read again too, so the page shows what's there now, with its
 * new version.
 */
function useRefreshLetterTemplates() {
  const queryClient = useQueryClient();
  const onSuccess = async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: letterTemplateKeys.all}),
      queryClient.invalidateQueries({queryKey: teamKeys.all}),
    ]);
  };
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? onSuccess() : undefined);
  return {onSuccess, onError};
}

/**
 * Adds a template (no `id`) or replaces one's details from the version the person saw (`expectedVersion`), returning
 * it. The API's 400 names `Name`, `Kind`, `TeamId`, `Letterhead`, `BodySkeleton` or `Closing` (an unknown merge
 * field is one, on the text holding it); a name taken in that team, an archived team or a change made
 * meanwhile is a 409.
 */
export function useSaveLetterTemplate() {
  const refresh = useRefreshLetterTemplates();
  return useMutation({
    mutationFn: ({id, expectedVersion, input}: {id?: string; expectedVersion?: number; input: LetterTemplateInput}) => {
      const body = {...input, name: input.name.trim()};
      return id
        ? unwrap(api.PUT('/api/letter-templates/{id}', {params: {path: {id}}, body: {...body, expectedVersion}}))
        : unwrap(api.POST('/api/letter-templates', {body}));
    },
    ...refresh,
  });
}

/** A template and the version the person saw, for archiving or restoring it. */
export interface LetterTemplateVersion {
  id: string;
  expectedVersion: number;
}

/**
 * Archive and restore, each from the version shown, so a change made meanwhile is a 409; and delete, for a template no
 * letter has been written from (a 409 once one has).
 */
export function useLetterTemplateActions() {
  const refresh = useRefreshLetterTemplates();
  return {
    archive: useMutation({
      mutationFn: ({id, expectedVersion}: LetterTemplateVersion) =>
        unwrap(api.POST('/api/letter-templates/{id}/archive', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    restore: useMutation({
      mutationFn: ({id, expectedVersion}: LetterTemplateVersion) =>
        unwrap(api.POST('/api/letter-templates/{id}/restore', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        await unwrap(api.DELETE('/api/letter-templates/{id}', {params: {path: {id}}}));
      },
      ...refresh,
    }),
  };
}

/** A template's text to preview, saved or not, and whether in large print. */
export interface LetterTemplatePreviewInput {
  name?: string;
  letterhead: string;
  bodySkeleton: string;
  closing: string;
  largePrint: boolean;
}

/** The file a preview is saved as (the API names it the same). */
export const LETTER_TEMPLATE_PREVIEW_FILE = 'letter-template-preview.pdf';

/**
 * Asks the API for a PDF of the text filled with made-up details (every page is marked as a preview), then saves it in
 * the browser, through a link to it in memory. A 400 (an unknown merge field) is an ApiValidationError on the field
 * holding it; anything else unsuccessful an ApiError.
 */
export async function downloadLetterTemplatePreview(input: LetterTemplatePreviewInput) {
  const body = {...input, name: input.name?.trim() || null};
  const pdf = await unwrap(api.POST('/api/letter-templates/preview', {body, parseAs: 'blob'}));
  const url = URL.createObjectURL(pdf);
  const link = document.createElement('a');
  link.href = url;
  link.download = LETTER_TEMPLATE_PREVIEW_FILE;
  link.click();
  URL.revokeObjectURL(url);
}

/** Downloads a preview, as a mutation so the page can show it's working. */
export const useLetterTemplatePreview = () => useMutation({mutationFn: downloadLetterTemplatePreview});

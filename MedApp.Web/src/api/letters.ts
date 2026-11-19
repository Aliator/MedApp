import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {pathwayKeys} from './pathways';
import {NO_PRACTICE_ID} from './practices';
import type {paths} from './schema';

/**
 * Letters (docs/centre-phases-3-5-plan.md, decisions 100 to 108): written by a holder of WriteLetters from a template,
 * about one of a patient's pathways or referrals; submitted to a named holder of ApproveLetters (or approved by its
 * author if they hold it); then sent to each recipient, by secure email from MedApp or recorded as sent another way.
 * An approved letter found wrong is withdrawn, and a correction written. A draft is its author's alone. Every change
 * carries the version the page showed (`expectedVersion`), so one made meanwhile is a 409. What the caller may do is in
 * the response's `can...` flags: pages show nothing they don't allow.
 */
export type Letter = Schemas['LetterResponse'];
export type LetterSummary = Schemas['LetterSummaryResponse'];
export type LetterRecipient = Schemas['LetterRecipientResponse'];
export type LetterRecipientRequest = Schemas['LetterRecipientRequest'];
export type LetterPage = Schemas['LetterSummaryResponsePagedResponse'];
export type LetterSearch = NonNullable<paths['/api/letters/search']['get']['parameters']['query']>;

/** The letters list's views, by the names the API takes, with their words (decision 108). */
export const LETTER_VIEWS = [
  {value: 'MyDrafts', label: 'My drafts'},
  {value: 'AwaitingMyApproval', label: 'Awaiting my approval'},
  {value: 'ReturnedToMe', label: 'Returned to me'},
  {value: 'ToSend', label: 'To send'},
  {value: 'Sent', label: 'Sent'},
] as const;
export type LetterView = (typeof LETTER_VIEWS)[number]['value'];

/** A view in words ("ToSend" → "To send"). */
export const letterViewLabel = (view: LetterView) => LETTER_VIEWS.find((v) => v.value === view)?.label ?? view;

/** What a letter is, in words, as the API's `LetterKinds.Describe` says it (the letter templates page says "Other" for the last). */
const KIND_WORDS: Record<string, string> = {
  ClinicLetter: 'Clinic letter',
  DischargeSummary: 'Discharge summary',
  ReferralResponse: 'Referral response',
  InformationRequest: 'Request for more information',
  AppointmentLetter: 'Appointment letter',
  OnwardReferral: 'Onward referral',
  Other: 'Letter',
};

/** A letter's kind in words ("DischargeSummary" → "Discharge summary"); one this copy doesn't know as the API named it. */
export const letterKindWords = (kind: string | null | undefined) => KIND_WORDS[kind ?? ''] ?? kind ?? 'Letter';

/** The kinds a stage can need (`NeedsLetterKind`), in the order pickers offer them. */
export const LETTER_KINDS = ['ClinicLetter', 'DischargeSummary', 'ReferralResponse', 'InformationRequest', 'AppointmentLetter', 'OnwardReferral', 'Other'] as const;

/** The kinds a letter written from a referral can be (decision 106). */
export const REFERRAL_LETTER_KINDS: readonly string[] = ['ReferralResponse', 'InformationRequest'];

/**
 * What a stage that needs a letter says: "Needs a discharge summary, approved and sent, before it can be finished."
 * Nothing for a stage that needs none.
 */
export function needsLetterWords(kind: string | null | undefined): string | null {
  if (!kind || kind === 'None') return null;
  const words = letterKindWords(kind).toLowerCase();
  const article = /^[aeiou]/.test(words) ? 'an' : 'a';
  return `Needs ${article} ${words}, approved and sent, before it can be finished.`;
}

/**
 * Where a letter is, in words: "Draft" (or "Returned", a draft its approver sent back), "Awaiting approval",
 * "Approved" (or "To send", while a recipient hasn't been reached) and "Withdrawn".
 */
export function letterStatusWords(letter: Pick<LetterSummary, 'status' | 'isReturned' | 'unsentCount'>): string {
  switch (letter.status) {
    case 'Draft':
      return letter.isReturned ? 'Returned' : 'Draft';
    case 'AwaitingApproval':
      return 'Awaiting approval';
    case 'Approved':
      return (letter.unsentCount ?? 0) > 0 ? 'To send' : 'Approved';
    case 'Withdrawn':
      return 'Withdrawn';
    default:
      return letter.status ?? '';
  }
}

/** Who a letter goes to, by the kinds the API takes, with their words. */
export const RECIPIENT_KINDS = [
  {value: 'GpPractice', label: 'GP practice'},
  {value: 'Patient', label: 'The patient'},
  {value: 'Referrer', label: 'The referrer'},
  /** A hospital (MC28, `LetterRecipientKind.Hospital = 4`): its name and address typed, as someone else's are. */
  {value: 'Hospital', label: 'A hospital'},
  {value: 'Other', label: 'Someone else'},
] as const;
export type RecipientKind = (typeof RECIPIENT_KINDS)[number]['value'];

export const recipientKindWords = (kind: string | null | undefined) => RECIPIENT_KINDS.find((k) => k.value === kind)?.label ?? kind ?? '';

/** The most recipients a letter can have. */
export const MAX_RECIPIENTS = 10;
/** The longest a letter's body can be. */
export const MAX_LETTER_BODY = 20000;
/** The longest a return comment or a withdrawal reason can be. */
export const MAX_LETTER_COMMENT = 500;

/** How a letter reached someone, with its words. Secure email is MedApp's own; the rest are recorded by hand. */
export const DISPATCH_METHODS = [
  {value: 'Post', label: 'Post'},
  {value: 'HandedToPatient', label: 'Handed to the patient'},
  {value: 'SecureEmail', label: 'Secure email from MedApp'},
  {value: 'EmailOutsideMedApp', label: 'Email from our own secure mailbox'},
  {value: 'Other', label: 'Other'},
] as const;

/** The methods "Record as sent" offers (not MedApp's own secure email, which sends it). */
export const RECORDED_METHODS = DISPATCH_METHODS.filter((m) => m.value !== 'SecureEmail');

export const dispatchMethodWords = (method: string | null | undefined) => DISPATCH_METHODS.find((m) => m.value === method)?.label ?? method ?? '';

/** Whether a recipient has the letter: "Not sent", "Sent", or "Failed" (a secure email the mail server refused). */
export const dispatchStatusWords = (status: string | null | undefined) =>
  status === 'Sent' ? 'Sent' : status === 'Failed' ? 'Failed' : status === 'NotSent' ? 'Not sent' : (status ?? '');

/** A recipient's address on lines: its address lines, then its postcode. */
export const recipientAddressLines = (recipient: Pick<LetterRecipient, 'addressLines' | 'postcode'>) =>
  [...(recipient.addressLines ?? '').split('\n'), recipient.postcode ?? ''].map((line) => line.trim()).filter(Boolean);

/** How many days back a letter can be recorded as sent. */
export const RECORD_SENT_DAYS_BACK = 7;

/** Every letter query is under ['letters'], so one change refreshes them all: the lists, the letter and the count. */
export const letterKeys = {
  all: ['letters'] as const,
  one: (id: string) => ['letters', id] as const,
  search: (search: LetterSearch) => ['letters', 'search', search] as const,
  count: ['letters', 'count'] as const,
};

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== '')) as T;
}

/** One letter (`GET api/letters/{id}`), with its text, recipients and what the caller may do. A missing one is a 404. */
export function letterQuery(id: string) {
  return queryOptions({
    queryKey: letterKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/letters/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of letters (`GET api/letters/search`), in the API's order: a view's, or every letter of a patient, pathway or
 * referral. While the next page loads, the current one stays on screen.
 */
export function letterSearchQuery(search: LetterSearch) {
  const query = compact(search);
  return queryOptions({
    queryKey: letterKeys.search(query),
    queryFn: () => unwrap(api.GET('/api/letters/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

/** The side nav's count: the caller's letters to approve, and every approved letter still to send. */
export const letterCountQuery = queryOptions({
  queryKey: letterKeys.count,
  queryFn: () => unwrap(api.GET('/api/letters/count')),
});

/** The most letters a pathway's or referral's card lists. */
export const LETTERS_LISTED = 20;

/** Every letter of a pathway or a referral (their pages' Letters cards). */
export const lettersAboutQuery = (about: {pathwayId?: string; referralId?: string}) =>
  letterSearchQuery({PathwayId: about.pathwayId, ReferralId: about.referralId, PageSize: LETTERS_LISTED});

/** Where a draft's preview PDF is (marked "Draft, not approved" on every page): a download. */
export const letterPreviewUrl = (id: string) => `/api/letters/${id}/preview`;

/** Where a letter's page is. */
export const letterHref = (id: string) => `/letters/${id}`;

/**
 * After a change to a letter: it (when given) is cached as the API returned it, and every letter list and the count,
 * the timelines (letters are on them) and its pathway (a stage may wait for it) are read again.
 */
async function refreshLetters(queryClient: QueryClient, letter?: Letter) {
  if (letter?.id) queryClient.setQueryData(letterKeys.one(letter.id), letter);
  await Promise.all([
    queryClient.invalidateQueries({queryKey: letterKeys.all, predicate: (query) => query.queryKey[1] !== letter?.id}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
    letter?.pathwayId ? queryClient.invalidateQueries({queryKey: ['pathways', letter.pathwayId]}) : undefined,
  ]);
}

/** Starts a letter from a template about a pathway or a referral (`POST api/letters`), returning the new draft. */
export function useCreateLetter() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {templateId: string; pathwayId?: string; referralId?: string}) => unwrap(api.POST('/api/letters', {body})),
    onSuccess: (letter) => refreshLetters(queryClient, letter),
  });
}

/** A recipient as the editor holds it: every field a string, '' for none, with a key of its own for React. */
export interface RecipientInput {
  key: string;
  kind: RecipientKind;
  practiceId: string;
  name: string;
  /** Up to three lines, separated by line breaks. */
  addressLines: string;
  postcode: string;
  email: string;
}

let nextKey = 0;
/** A key for a recipient row, unique on the page. */
export const newRecipientKey = () => `recipient-${++nextKey}`;

/** A saved recipient as the editor starts with it. */
export function recipientInput(recipient: LetterRecipient): RecipientInput {
  return {
    key: recipient.id ?? newRecipientKey(),
    kind: (RECIPIENT_KINDS.find((k) => k.value === recipient.kind)?.value ?? 'Other') as RecipientKind,
    practiceId: recipient.practiceId ?? (recipient.kind === 'GpPractice' ? PATIENTS_UNLISTED_PRACTICE : ''),
    name: recipient.name ?? '',
    addressLines: recipient.addressLines ?? '',
    postcode: recipient.postcode ?? '',
    email: recipient.email ?? '',
  };
}

/**
 * A GP practice recipient's `practiceId` for the patient's own practice that isn't in the directory: sent with no
 * practice ID, and the API fills it from the patient (400 when they have none).
 */
export const PATIENTS_UNLISTED_PRACTICE = 'patients-unlisted-practice';

/** A new recipient of a kind, empty. */
export const emptyRecipient = (kind: RecipientKind): RecipientInput => ({
  key: newRecipientKey(),
  kind,
  practiceId: '',
  name: '',
  addressLines: '',
  postcode: '',
  email: '',
});

/**
 * Whether a referrer's address is typed: only when its referral names no practice from the directory (the API then
 * takes the address from the letter's referral).
 */
export const isReferrerTyped = (recipient: Pick<RecipientInput, 'kind' | 'practiceId'>) => recipient.kind === 'Referrer' && !recipient.practiceId;

const optional = (text: string) => (text.trim() ? text.trim() : null);

/** What a recipient sends: only what its kind takes. */
export function toRecipientRequest(recipient: RecipientInput): LetterRecipientRequest {
  switch (recipient.kind) {
    case 'GpPractice':
      // None chosen goes as the empty ID, which the API refuses with "Choose the GP practice from the directory."
      return {
        kind: 'GpPractice',
        practiceId: recipient.practiceId === PATIENTS_UNLISTED_PRACTICE ? null : recipient.practiceId || NO_PRACTICE_ID,
      };
    case 'Patient':
      return {kind: 'Patient'};
    case 'Referrer':
      return isReferrerTyped(recipient)
        ? {kind: 'Referrer', addressLines: optional(recipient.addressLines), postcode: optional(recipient.postcode)}
        : {kind: 'Referrer'};
    default:
      // Someone else, or a hospital: a name and address typed.
      return {
        kind: recipient.kind === 'Hospital' ? 'Hospital' : 'Other',
        name: optional(recipient.name),
        addressLines: optional(recipient.addressLines),
        postcode: optional(recipient.postcode),
        email: optional(recipient.email),
      };
  }
}

/** A draft as the editor holds it. */
export interface LetterDraftInput {
  letterhead: string;
  body: string;
  closing: string;
  largePrint: boolean;
  recipients: RecipientInput[];
}

/** A letter's draft as the editor starts with it. */
export const letterDraftInput = (letter: Letter): LetterDraftInput => ({
  letterhead: letter.letterhead ?? '',
  body: letter.body ?? '',
  closing: letter.closing ?? '',
  largePrint: letter.largePrint ?? false,
  recipients: (letter.recipients ?? []).map(recipientInput),
});

/** Whether the editor's draft differs from the saved letter. */
export function isDraftChanged(letter: Letter, input: LetterDraftInput): boolean {
  const saved = letterDraftInput(letter);
  const comparable = (draft: LetterDraftInput) =>
    JSON.stringify({...draft, recipients: draft.recipients.map(toRecipientRequest)});
  return comparable(saved) !== comparable(input);
}

/**
 * Every change to one letter, each from the version the page showed (`expectedVersion`), returning the letter (which
 * replaces the cached one). A 409 (changed meanwhile, the mail server refused, the record restricted) reads it again,
 * so the page can show what's there now. Discarding removes it from the cache; a correction is a new letter.
 */
export function useLetterActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = (letter: Letter) => refreshLetters(queryClient, letter);
  const onError = (error: unknown) =>
    error instanceof ApiError && error.status === 409 ? queryClient.invalidateQueries({queryKey: letterKeys.one(id)}) : undefined;
  const path = {params: {path: {id}}};
  return {
    save: useMutation({
      mutationFn: ({expectedVersion, input}: {expectedVersion: number; input: LetterDraftInput}) =>
        unwrap(
          api.PUT('/api/letters/{id}', {
            ...path,
            body: {
              letterhead: input.letterhead,
              body: input.body,
              closing: input.closing,
              largePrint: input.largePrint,
              recipients: input.recipients.map(toRecipientRequest),
              expectedVersion,
            },
          }),
        ),
      onSuccess,
      onError,
    }),
    submit: useMutation({
      mutationFn: (body: {approverUserId: string; expectedVersion: number}) => unwrap(api.POST('/api/letters/{id}/submit', {...path, body})),
      onSuccess,
      onError,
    }),
    approve: useMutation({
      mutationFn: (body: {expectedVersion: number}) => unwrap(api.POST('/api/letters/{id}/approve', {...path, body})),
      onSuccess,
      onError,
    }),
    returnToAuthor: useMutation({
      mutationFn: (body: {comment: string; expectedVersion: number}) => unwrap(api.POST('/api/letters/{id}/return', {...path, body})),
      onSuccess,
      onError,
    }),
    discard: useMutation({
      mutationFn: (body: {expectedVersion: number}) => unwrap(api.POST('/api/letters/{id}/discard', {...path, body})),
      onSuccess: async (letter: Letter) => {
        queryClient.removeQueries({queryKey: letterKeys.one(id), exact: true});
        await refreshLetters(queryClient, {...letter, id: undefined});
      },
      onError,
    }),
    withdraw: useMutation({
      mutationFn: (body: {reason: string; expectedVersion: number}) => unwrap(api.POST('/api/letters/{id}/withdraw', {...path, body})),
      onSuccess,
      onError,
    }),
    correction: useMutation({
      mutationFn: () => unwrap(api.POST('/api/letters/{id}/correction', path)),
      onSuccess: async (correction: Letter) => {
        // The withdrawn letter now links to its correction.
        await Promise.all([refreshLetters(queryClient, correction), queryClient.invalidateQueries({queryKey: letterKeys.one(id)})]);
      },
      onError,
    }),
    sendSecureEmail: useMutation({
      mutationFn: ({recipientId, expectedVersion}: {recipientId: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/letters/{id}/recipients/{recipientId}/send', {params: {path: {id, recipientId}}, body: {expectedVersion}})),
      onSuccess,
      onError,
    }),
    recordSent: useMutation({
      mutationFn: ({recipientId, ...body}: {recipientId: string; method: string; sentOn: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/letters/{id}/recipients/{recipientId}/record-sent', {params: {path: {id, recipientId}}, body})),
      onSuccess,
      onError,
    }),
  };
}


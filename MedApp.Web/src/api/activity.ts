import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {formatShortDate} from '../utils/dates';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';
import {PAGE_SIZE} from './search';

export type ActivitySearch = NonNullable<paths['/api/activity/search']['get']['parameters']['query']>;
export type ActivityEntry = Schemas['ActivityEntryResponse'];

/** The activity log's actions, as the API names them (stored as numbers, never renumbered). */
export const ACTIVITY_ACTIONS = [
  'PatientViewed',
  'PatientListed',
  'PatientsSearched',
  'PatientCreated',
  'PatientUpdated',
  'PatientDeleted',
  'PatientExported',
  'PatientAnonymised',
  'PatientRestricted',
  'PatientRestrictionLifted',
  'PatientDisputed',
  'PatientDisputeCleared',
  'RetentionKept',
  'PathwayViewed',
  'DemographicsLookedUp',
  'SignedIn',
  'SignInFailed',
  'SignedOut',
  'AccountCreated',
  'AccountDeleted',
  'PasswordReset',
  'PasswordChanged',
  'RolesChanged',
  'RoleCreated',
  'RoleDeleted',
  'SignedOutEverywhere',
  'AttachmentsViewed',
  'AttachmentDownloaded',
  'TwoFactorChallenged',
  'TwoFactorEnabled',
  'TwoFactorDisabled',
  'TwoFactorReset',
  'RecoveryCodesRegenerated',
  'RecoveryCodeUsed',
  'PasswordResetRequested',
  'PasswordResetByEmail',
  'EmailChangeRequested',
  'EmailConfirmed',
  'EmailRemoved',
  'TwoFactorRequirementChanged',
  'EmailCodesEnabled',
  'EmailCodesDisabled',
  'StaffSpecialtiesChanged',
  'ReferralViewed',
  'RoleChanged',
  'StaffDetailsChanged',
  'DataRequestPackDownloaded',
  'DataRequestChanged',
] as const;
export type ActivityAction = (typeof ACTIVITY_ACTIONS)[number];

const ACTION_LABELS: Record<ActivityAction, string> = {
  PatientViewed: 'Patient viewed',
  PatientListed: 'In search results',
  PatientsSearched: 'Patients searched',
  PatientCreated: 'Patient added',
  PatientUpdated: 'Patient edited',
  PatientDeleted: 'Patient deleted',
  PatientExported: 'Record exported',
  PatientAnonymised: 'Record anonymised',
  PatientRestricted: 'Record restricted',
  PatientRestrictionLifted: 'Restriction lifted',
  PatientDisputed: 'Marked disputed',
  PatientDisputeCleared: 'Dispute cleared',
  RetentionKept: 'Kept past retention',
  PathwayViewed: 'Pathway viewed',
  DemographicsLookedUp: 'NHS number looked up',
  SignedIn: 'Signed in',
  SignInFailed: 'Sign-in refused',
  SignedOut: 'Signed out',
  AccountCreated: 'Account created',
  AccountDeleted: 'Account deleted',
  PasswordReset: 'Password reset',
  PasswordChanged: 'Password changed',
  RolesChanged: 'Roles changed',
  RoleCreated: 'Role created',
  RoleDeleted: 'Role deleted',
  SignedOutEverywhere: 'Signed out everywhere',
  AttachmentsViewed: 'Files viewed',
  AttachmentDownloaded: 'File downloaded',
  TwoFactorChallenged: 'Code asked for',
  TwoFactorEnabled: 'Two-factor turned on',
  TwoFactorDisabled: 'Two-factor turned off',
  TwoFactorReset: 'Two-factor reset',
  RecoveryCodesRegenerated: 'New recovery codes',
  RecoveryCodeUsed: 'Recovery code used',
  PasswordResetRequested: 'Password reset asked for',
  PasswordResetByEmail: 'Password reset by email',
  EmailChangeRequested: 'Email address change asked for',
  EmailConfirmed: 'Email address confirmed',
  EmailRemoved: 'Email address removed',
  TwoFactorRequirementChanged: 'Two-factor requirement changed',
  EmailCodesEnabled: 'Codes by email turned on',
  EmailCodesDisabled: 'Codes by email turned off',
  StaffSpecialtiesChanged: 'Specialties changed',
  ReferralViewed: 'Referral viewed',
  RoleChanged: 'Role changed',
  StaffDetailsChanged: 'Staff details changed',
  DataRequestPackDownloaded: 'Data request pack downloaded',
  DataRequestChanged: 'Data request changed',
};

/** An action in words: "PatientViewed" → "Patient viewed". */
export const activityActionLabel = (action: string | null | undefined) =>
  ACTION_LABELS[action as ActivityAction] ?? action ?? '';

/** The actions about a patient's attached files: listing them, and downloading one. */
export const FILE_ACTIONS: ActivityAction[] = ['AttachmentsViewed', 'AttachmentDownloaded'];

/** The actions that read a patient's record, for the "who has looked" quick filter. */
export const ACCESS_ACTIONS: ActivityAction[] = [
  'PatientViewed',
  'PatientListed',
  'PathwayViewed',
  'ReferralViewed',
  'PatientExported',
  'DataRequestPackDownloaded',
  ...FILE_ACTIONS,
];

/** The sign-in actions, for their quick filter. */
export const SIGN_IN_ACTIONS: ActivityAction[] = ['SignedIn', 'TwoFactorChallenged', 'SignInFailed', 'SignedOut'];

/** The account and role actions, for their quick filter. */
export const ACCOUNT_ACTIONS: ActivityAction[] = [
  'AccountCreated',
  'AccountDeleted',
  'PasswordReset',
  'PasswordChanged',
  'RolesChanged',
  'RoleCreated',
  'RoleDeleted',
  'SignedOutEverywhere',
  'TwoFactorEnabled',
  'TwoFactorDisabled',
  'TwoFactorReset',
  'RecoveryCodesRegenerated',
  'RecoveryCodeUsed',
  'PasswordResetRequested',
  'PasswordResetByEmail',
  'EmailChangeRequested',
  'EmailConfirmed',
  'EmailRemoved',
  'TwoFactorRequirementChanged',
  'EmailCodesEnabled',
  'EmailCodesDisabled',
  'StaffSpecialtiesChanged',
  'RoleChanged',
  'StaffDetailsChanged',
];

/** Leaves out empty filters, so they aren't sent and equal searches share a cache entry. */
function compact<T extends object>(search: T): T {
  return Object.fromEntries(
    Object.entries(search).filter(([, value]) => value !== undefined && value !== null && value !== ''),
  ) as T;
}

/**
 * One page of the activity log (`GET api/activity/search`, admins only), newest first unless `Ascending`. While the
 * next page loads, the current one stays on screen. Keyed under ['activity']; changes to a patient refresh it.
 */
export function activitySearchQuery(search: ActivitySearch) {
  const query = compact({PageSize: PAGE_SIZE, ...search});
  return queryOptions({
    queryKey: ['activity', query],
    queryFn: () => unwrap(api.GET('/api/activity/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

type Details = Record<string, unknown>;

function parse(details: string | null | undefined): Details {
  try {
    const value: unknown = JSON.parse(details ?? '{}');
    return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Details) : {};
  } catch {
    return {};
  }
}

const FIELD_NAMES: Record<string, string> = {
  all: 'all patients',
  firstName: 'first name',
  lastName: 'last name',
  email: 'email',
  dateOfBirth: 'date of birth',
  dateOfBirthFrom: 'born from',
  dateOfBirthTo: 'born to',
  nhsNumber: 'NHS number',
  addressLines: 'address',
  postcode: 'postcode',
  mobilePhone: 'mobile',
  practice: 'GP practice',
  fullName: 'full name',
  jobTitle: 'job title',
};

/** Why a sign-in, or its second step, was refused. */
const SIGN_IN_FAILURES: Record<string, string> = {
  InvalidCredentials: 'Wrong username or password',
  LockedOut: 'Account locked',
  // An authenticator code, or (for an account using codes by email) an emailed one.
  InvalidCode: 'Wrong two-factor code',
  InvalidRecoveryCode: 'Wrong recovery code',
  InvalidCodeLockedOut: 'Wrong two-factor code: account locked',
  InvalidRecoveryCodeLockedOut: 'Wrong recovery code: account locked',
  ChallengeExpired: 'Code step timed out',
  TooManyCodes: 'Too many codes for the account',
};

/** How a sign-in's second step was made. */
const SECOND_STEPS: Record<string, string> = {
  authenticator: 'With an authenticator code',
  email: 'With an emailed code',
  recoveryCode: 'With a recovery code',
};

const fieldNames = (value: unknown) =>
  Array.isArray(value) ? value.map((f) => FIELD_NAMES[String(f)] ?? String(f)).join(', ') : '';

const roles = (value: unknown) => (Array.isArray(value) && value.length ? value.map(String).join(', ') : 'none');

const text = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : '');

/** What a data request's change was, in words (`DataRequestChanged`'s `change`). */
const DATA_REQUEST_CHANGES: Record<string, string> = {
  Recorded: 'Request logged',
  IdentityChecked: 'Identity checked',
  Extended: 'Due date extended',
  Reviewed: 'Clinician review recorded',
  Sent: 'Response sent',
  Refused: 'Request refused',
  Withdrawn: 'Request withdrawn',
};

const names = (value: unknown) => (Array.isArray(value) ? value.map(String).join(', ') : '');

/** A role change in words: "Nurse: renamed from Nurses; added AssignPathways; removed DeletePatients". */
function roleChange(d: Record<string, unknown>): string {
  const parts = [
    text(d.renamedFrom) && `renamed from ${text(d.renamedFrom)}`,
    d.descriptionChanged === true && 'description changed',
    names(d.added) && `added ${names(d.added)}`,
    names(d.removed) && `removed ${names(d.removed)}`,
    d.archived === true && 'archived',
    d.archived === false && 'restored',
  ].filter(Boolean);
  return [text(d.role), parts.join('; ')].filter(Boolean).join(': ');
}

/**
 * What an entry's details say, in a line of words, e.g. "Changed first name, email", "By last name: 3 found",
 * "By NHS number (FHIR API): 1 found", "A file on the patient" or "User → Admin, User". Empty when the action says
 * it all.
 */
export function describeActivity(entry: Pick<ActivityEntry, 'action' | 'details' | 'pathwayId'>): string {
  const d = parse(entry.details);
  switch (entry.action) {
    case 'PatientUpdated': {
      const fields = fieldNames(d.fields);
      return fields ? `Changed ${fields}` : 'Nothing changed';
    }
    // The names of the fields changed, never their values.
    case 'StaffDetailsChanged': {
      const fields = fieldNames(d.fields);
      return fields ? `Changed ${fields}` : '';
    }
    case 'PatientsSearched': {
      const count = typeof d.resultCount === 'number' ? `${d.resultCount} found` : '';
      const by = Array.isArray(d.filters) && d.filters.includes('all') ? 'All patients' : `By ${fieldNames(d.filters)}`;
      // A search through the FHIR API (another system asking by NHS number), not the patients list.
      const via = d.via === 'fhir' ? ' (FHIR API)' : '';
      return [by + via, count].filter(Boolean).join(': ');
    }
    case 'DemographicsLookedUp':
      // Whether the national demographics service found anyone; the number looked up is never recorded.
      return d.found === true ? 'Found' : d.found === false ? 'Nobody found' : '';
    case 'SignInFailed':
      return SIGN_IN_FAILURES[String(d.reason)] ?? 'Wrong username or password';
    case 'SignedIn':
      return SECOND_STEPS[String(d.secondStep)] ?? '';
    case 'TwoFactorChallenged':
      return d.method === 'email' ? 'Code emailed' : '';
    case 'TwoFactorEnabled':
      return d.switchedFrom === 'email' ? 'Switched from codes by email' : '';
    case 'RecoveryCodeUsed':
      return typeof d.codesLeft === 'number' ? `${d.codesLeft} left` : '';
    case 'PasswordResetRequested':
      // Never what was typed: only whether an account with a confirmed address matched.
      return d.matched === true ? 'Link sent' : d.matched === false ? 'No account matched' : '';
    case 'TwoFactorRequirementChanged':
      return d.requireTwoFactorForAdmins === true ? 'Required for admins' : d.requireTwoFactorForAdmins === false ? 'Not required' : '';
    case 'RolesChanged':
      if (d.added) return `Added ${String(d.added)}`;
      if (d.removed) return `Removed ${String(d.removed)}`;
      return `${roles(d.from)} → ${roles(d.to)}`;
    // By name, as they were then (a specialty renamed since keeps its old name here).
    case 'StaffSpecialtiesChanged':
      return `${roles(d.from)} → ${roles(d.to)}`;
    // Role and permission names only: a description's words are never recorded.
    case 'RoleCreated':
    case 'RoleDeleted': {
      const permissions = Array.isArray(d.permissions) && d.permissions.length ? d.permissions.map(String).join(', ') : '';
      return [text(d.role), permissions].filter(Boolean).join(': ');
    }
    case 'RoleChanged':
      return roleChange(d);
    case 'RetentionKept':
      return [typeof d.keepUntil === 'string' ? `Until ${formatShortDate(d.keepUntil)}` : '', text(d.reason)].filter(Boolean).join(': ');
    // Recorded with the pathway when the files are a pathway's or a stage's; the file's name is never recorded.
    case 'AttachmentsViewed':
      return entry.pathwayId ? "A pathway's or stage's files" : "All the patient's files";
    case 'AttachmentDownloaded':
      return entry.pathwayId ? 'A file on a pathway or stage' : 'A file on the patient';
    // What changed, never the requester, a reason or anything withheld.
    case 'DataRequestChanged':
      return DATA_REQUEST_CHANGES[String(d.change)] ?? text(d.change);
    case 'PatientRestricted':
    case 'PatientRestrictionLifted':
    case 'PatientDisputed':
    case 'PatientDisputeCleared':
    case 'PatientAnonymised':
      return text(d.reason);
    default:
      return '';
  }
}

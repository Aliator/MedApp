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
  'ProcedureViewed',
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
  ProcedureViewed: 'Procedure viewed',
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
};

/** An action in words: "PatientViewed" → "Patient viewed". */
export const activityActionLabel = (action: string | null | undefined) =>
  ACTION_LABELS[action as ActivityAction] ?? action ?? '';

/** The actions that read a patient's record, for the "who has looked" quick filter. */
export const ACCESS_ACTIONS: ActivityAction[] = ['PatientViewed', 'PatientListed', 'ProcedureViewed', 'PatientExported'];

/** The sign-in actions, for their quick filter. */
export const SIGN_IN_ACTIONS: ActivityAction[] = ['SignedIn', 'SignInFailed', 'SignedOut'];

/** The account and role actions, for their quick filter. */
export const ACCOUNT_ACTIONS: ActivityAction[] = [
  'AccountCreated',
  'AccountDeleted',
  'PasswordReset',
  'PasswordChanged',
  'RolesChanged',
  'RoleCreated',
  'RoleDeleted',
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
};

const fieldNames = (value: unknown) =>
  Array.isArray(value) ? value.map((f) => FIELD_NAMES[String(f)] ?? String(f)).join(', ') : '';

const roles = (value: unknown) => (Array.isArray(value) && value.length ? value.map(String).join(', ') : 'none');

const text = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : '');

/**
 * What an entry's details say, in a line of words, e.g. "Changed first name, email", "By last name: 3 found" or
 * "User → Admin, User". Empty when the action says it all.
 */
export function describeActivity(entry: Pick<ActivityEntry, 'action' | 'details'>): string {
  const d = parse(entry.details);
  switch (entry.action) {
    case 'PatientUpdated': {
      const fields = fieldNames(d.fields);
      return fields ? `Changed ${fields}` : 'Nothing changed';
    }
    case 'PatientsSearched': {
      const count = typeof d.resultCount === 'number' ? `${d.resultCount} found` : '';
      const by = Array.isArray(d.filters) && d.filters.includes('all') ? 'All patients' : `By ${fieldNames(d.filters)}`;
      return [by, count].filter(Boolean).join(': ');
    }
    case 'SignInFailed':
      return d.reason === 'LockedOut' ? 'Account locked' : 'Wrong username or password';
    case 'RolesChanged':
      if (d.added) return `Added ${String(d.added)}`;
      if (d.removed) return `Removed ${String(d.removed)}`;
      return `${roles(d.from)} → ${roles(d.to)}`;
    case 'RoleCreated':
    case 'RoleDeleted':
      return text(d.role);
    case 'RetentionKept':
      return [typeof d.keepUntil === 'string' ? `Until ${formatShortDate(d.keepUntil)}` : '', text(d.reason)].filter(Boolean).join(': ');
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

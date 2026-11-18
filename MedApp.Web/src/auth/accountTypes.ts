/**
 * Account types (decision 182 of `docs/multi-clinic-plan.md`), worked out, never stored: **Admin** holds the built-in
 * Admin role (overview, Insights, read-only access to all data, accounts and setup; no clinical changes); **Clinic
 * admin** is staff who is a clinic admin of any of their teams; anyone else is **Staff**, in a team (a clinic or the
 * lab). Only staff do clinical work. The API sends it as `accountType` on whoami and each user.
 */
export const ACCOUNT_TYPES = ['Admin', 'ClinicAdmin', 'Staff'] as const;

export type AccountType = (typeof ACCOUNT_TYPES)[number];

/** A team's kind (the API's `TeamKind`): a clinic, or the lab (at most one that isn't archived). */
export type TeamKind = 'Clinic' | 'Lab';

/** Someone's role in one of their teams (the API's `TeamMemberRole`). */
export type TeamMemberRole = 'Member' | 'ClinicAdmin';

/** What each type is called on screen. */
export const ACCOUNT_TYPE_LABELS: Record<AccountType, string> = {
  Admin: 'Admin',
  ClinicAdmin: 'Clinic admin',
  Staff: 'Staff',
};

/** What each type may do, in a line, for Account Settings and the Users page. */
export const ACCOUNT_TYPE_DESCRIPTIONS: Record<AccountType, string> = {
  Admin: 'Overview, Insights, accounts and setup; reads every team’s records but makes no clinical changes.',
  ClinicAdmin: 'Staff who also look after their clinic: clinical work as their roles allow, in their teams.',
  Staff: 'Clinical work as their roles allow, in their teams.',
};

export const isAccountType = (value: unknown): value is AccountType =>
  typeof value === 'string' && (ACCOUNT_TYPES as readonly string[]).includes(value);

export const isTeamMemberRole = (value: unknown): value is TeamMemberRole => value === 'Member' || value === 'ClinicAdmin';

/** The type from someone's roles and their teams' roles (as the API works it out): Admin first, then clinic admin. */
export function accountTypeOf(roles: readonly string[], teamRoles: readonly (string | null | undefined)[]): AccountType {
  if (roles.includes('Admin')) return 'Admin';
  return teamRoles.includes('ClinicAdmin') ? 'ClinicAdmin' : 'Staff';
}

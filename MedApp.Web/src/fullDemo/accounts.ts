import type {DemoAccount} from './backend';

/**
 * The four accounts the walkthrough uses and the login page offers first, in this order (as the light demo's,
 * `src/demo/accounts.ts`): the admin, Dermatology's clinic admin, the lab, and the Eye clinic's clinic admin.
 */
export const DEMO_USERNAMES = ['admin', 'n.thornbury', 'r.aldous', 's.varma'] as const;

const CLINIC_ADMIN = ' (clinic admin)';

/** The four demo accounts first (those the database has), then everyone else by name. */
export function orderAccounts(accounts: readonly DemoAccount[]): {demo: DemoAccount[]; others: DemoAccount[]} {
  const demo = DEMO_USERNAMES.map((username) => accounts.find((a) => a.username === username)).filter((a): a is DemoAccount => !!a);
  const others = accounts.filter((a) => !demo.includes(a)).sort((a, b) => a.fullName.localeCompare(b.fullName));
  return {demo, others};
}

const listed = (names: string[]) => (names.length > 1 ? `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}` : (names[0] ?? ''));

/**
 * An account in a line, as the light demo's login page has it: "Clinic admin (Dermatology). Clinician. Consultant
 * dermatologist.", "Admin, read only. Centre administrator.", "Staff (Lab). Lab and Pathologist. Consultant
 * histopathologist."
 */
export function accountDescription(account: DemoAccount): string {
  const clinic = account.teams.find((team) => team.endsWith(CLINIC_ADMIN))?.slice(0, -CLINIC_ADMIN.length);
  const teams = account.teams.map((team) => (team.endsWith(CLINIC_ADMIN) ? team.slice(0, -CLINIC_ADMIN.length) : team));
  const label = account.roles.includes('Admin')
    ? 'Admin, read only'
    : clinic
      ? `Clinic admin (${clinic})`
      : teams.length
        ? `Staff (${listed(teams)})`
        : 'Staff';
  const roles = account.roles.filter((role) => role !== 'Admin');
  return `${label}.${roles.length ? ` ${listed(roles)}.` : ''} ${account.jobTitle}.`;
}

/** How the login page and the walkthrough name an account: "the admin", or the person's name. */
export const accountLabel = (account: Pick<DemoAccount, 'roles' | 'fullName'>) => (account.roles.includes('Admin') ? 'the admin' : account.fullName);

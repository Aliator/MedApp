import {accountTypeOf, type AccountType, type TeamMemberRole} from '../auth/accountTypes';
import {ADMIN_PERMISSIONS, type Permission} from '../auth/permissions';
import {TEAMS, USERS} from './ids';

/** A team someone belongs to, and their role in it. */
export interface DemoMembership {
  teamId: string;
  role: TeamMemberRole;
}

/** Someone in the demo clinic (names from the full demo data, `tools/MedApp.DevMcp/DemoData/DemoStaff.cs`). */
export interface DemoPerson {
  id: string;
  username: string;
  fullName: string;
  jobTitle: string;
  roles: string[];
  permissions: Permission[];
  /** The teams they start in (the state's `memberships` holds them as changed since). */
  teams: DemoMembership[];
}

const member = (teamId: string): DemoMembership => ({teamId, role: 'Member'});

/** What the Clinician role allows (the API's `StandardRoles.Clinician`). */
const CLINICIAN: Permission[] = [
  'UseApp',
  'ViewPatients',
  'EditPatients',
  'RegisterReferrals',
  'TriageReferrals',
  'CreatePathways',
  'WorkPathways',
  'UnlockStages',
  'FinishWithoutVisit',
  'RecordMdtOutcomes',
  'RecordConsent',
  'BookAppointments',
  'AttachFiles',
  'RecordSpecimens',
  'SignOffResults',
  'WriteLetters',
  'ApproveLetters',
  'SendLetters',
];

/** What the Lab role allows (the API's `StandardRoles.Lab`). */
const LAB_ROLE: Permission[] = ['UseApp', 'ViewPatients', 'WorkPathways', 'AttachFiles', 'RecordSpecimens', 'ProcessSpecimens'];

/** What the Pathologist role allows (MC15's new starting role): the Lab role's, and authorising lab reports. */
export const PATHOLOGIST_ROLE: Permission[] = [...LAB_ROLE, 'AuthoriseLabReports'];

/**
 * The admin account a visitor can be: Admin alone, read only (decision 182 of `docs/multi-clinic-plan.md`): the
 * overview, Insights, every team's records, accounts and setup, and no clinical changes. In no team.
 */
export const ADMIN: DemoPerson = {
  id: USERS.admin,
  username: 'admin',
  fullName: 'Centre administrator',
  jobTitle: 'Centre administrator',
  roles: ['Admin'],
  permissions: ADMIN_PERMISSIONS,
  teams: [],
};

/** The clinic admin a visitor can be: a Clinician, and Dermatology's clinic admin. */
export const STAFF: DemoPerson = {
  id: USERS.thornbury,
  username: 'n.thornbury',
  fullName: 'Nadia Thornbury',
  jobTitle: 'Consultant dermatologist',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  teams: [{teamId: TEAMS.dermatology, role: 'ClinicAdmin'}],
};

/** The Dermatology clinic admin, by its type's name. */
export const CLINIC_ADMIN = STAFF;

/**
 * The lab's member of staff a visitor can be: the Lab and Pathologist roles (the second so the demo's one lab account
 * can authorise a report, MC15), in the lab team.
 */
export const LAB: DemoPerson = {
  id: USERS.aldous,
  username: 'r.aldous',
  fullName: 'Ravi Aldous',
  jobTitle: 'Biomedical scientist, authorising reports (the lab)',
  roles: ['Lab', 'Pathologist'],
  permissions: PATHOLOGIST_ROLE,
  teams: [member(TEAMS.lab)],
};

/** The three accounts the login page offers (decision 188). */
export const DEMO_ACCOUNTS = [ADMIN, CLINIC_ADMIN, LAB] as const;

/** Someone's account type, from their roles and their teams (as they start, or as changed since). */
export const accountTypeOfPerson = (person: DemoPerson, teams: readonly DemoMembership[] = person.teams): AccountType =>
  accountTypeOf(person.roles, teams.map((t) => t.role));

/** An account in a line, for the login page and the Read Me: "Clinic admin (Dermatology). Clinician. Consultant dermatologist." */
export function accountDescription(person: DemoPerson): string {
  const type = accountTypeOfPerson(person);
  const label = {Admin: 'Admin, read only', ClinicAdmin: 'Clinic admin (Dermatology)', Staff: person.teams.length ? 'Staff (the lab)' : 'Staff'}[type];
  const roles = person.roles.filter((r) => r !== 'Admin');
  return `${label}.${roles.length ? ` ${roles.join(' and ')}.` : ''} ${person.jobTitle}.`;
}

/** How the Read Me and the login page name an account: "the admin", or the person's name. */
export const accountLabel = (person: DemoPerson) => (person.roles.includes('Admin') ? 'the admin' : person.fullName);

/** The rest of the clinic, who appear in lists and on pathways but can't be signed in as. */
const OTHERS: DemoPerson[] = [
  {
    id: USERS.whitlock,
    username: 'j.whitlock',
    fullName: 'Jo Whitlock',
    jobTitle: 'Service manager',
    roles: ['Manager'],
    permissions: ['UseApp', 'ViewPatients', 'ViewInsights', 'ArrangeMeetings'],
    teams: [],
  },
  {
    id: USERS.sandiford,
    username: 'b.sandiford',
    fullName: 'Bea Sandiford',
    jobTitle: 'Clinical nurse specialist: skin cancer',
    roles: ['Nurse'],
    permissions: ['UseApp', 'ViewPatients', 'WorkPathways', 'BookAppointments'],
    teams: [member(TEAMS.dermatology)],
  },
  {
    id: USERS.marsh,
    username: 'h.marsh',
    fullName: 'Hannah Marsh',
    jobTitle: 'Booking coordinator',
    roles: ['Booking'],
    permissions: ['UseApp', 'ViewPatients', 'BookAppointments', 'RegisterReferrals'],
    teams: [],
  },
];

/** A Dermatology clinician (not an account a visitor can be). */
export const HALLWORTH: DemoPerson = {
  id: USERS.hallworth,
  username: 't.hallworth',
  fullName: 'Tom Hallworth',
  jobTitle: 'GP with a special interest in skin',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  teams: [member(TEAMS.dermatology)],
};

/** Everyone in the demo clinic. */
export const PEOPLE: DemoPerson[] = [ADMIN, STAFF, LAB, ...OTHERS, HALLWORTH];

export const personById = (id: string | null | undefined) => PEOPLE.find((person) => person.id === id);
export const personByUsername = (username: string | null | undefined) =>
  PEOPLE.find((person) => person.username.toLowerCase() === (username ?? '').toLowerCase());
export const usernameOf = (id: string | null | undefined) => personById(id)?.username ?? null;

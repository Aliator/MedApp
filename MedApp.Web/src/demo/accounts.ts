import {accountTypeOf, type AccountType, type TeamMemberRole} from '../auth/accountTypes';
import {ADMIN_PERMISSIONS, type Permission} from '../auth/permissions';
import {TEAM_NAMES, TEAMS, USERS} from './ids';

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

/** The Eye clinic's clinic admin a visitor can be: a Clinician, as Nadia Thornbury is in Dermatology. */
export const EYE_CLINIC_ADMIN: DemoPerson = {
  id: USERS.varma,
  username: 's.varma',
  fullName: 'Saira Varma',
  jobTitle: 'Consultant ophthalmologist',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  teams: [{teamId: TEAMS.eyeClinic, role: 'ClinicAdmin'}],
};

export const LOWER_LIMB_ADMIN: DemoPerson = {
  id: USERS.castell,
  username: 'm.castell',
  fullName: 'Mia Castell',
  jobTitle: 'Clinical lead podiatrist',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  teams: [{teamId: TEAMS.lowerLimb, role: 'ClinicAdmin'}],
};

/**
 * The lab's member of staff a visitor can be: a histopathologist with the Lab and Pathologist roles (the first so the
 * demo's one lab account also does the bench steps, the second so it can authorise a report, MC15), in the lab team.
 */
export const LAB: DemoPerson = {
  id: USERS.aldous,
  username: 'r.aldous',
  fullName: 'Ravi Aldous',
  jobTitle: 'Consultant histopathologist',
  roles: ['Lab', 'Pathologist'],
  permissions: PATHOLOGIST_ROLE,
  teams: [member(TEAMS.lab)],
};

/** The four accounts the login page offers (decision 188; the Eye clinic's, the owner, 20 November 2026). */
export const DEMO_ACCOUNTS = [ADMIN, CLINIC_ADMIN, EYE_CLINIC_ADMIN, LOWER_LIMB_ADMIN, LAB] as const;

/** Someone's account type, from their roles and their teams (as they start, or as changed since). */
export const accountTypeOfPerson = (person: DemoPerson, teams: readonly DemoMembership[] = person.teams): AccountType =>
  accountTypeOf(person.roles, teams.map((t) => t.role));

/** An account in a line, for the login page and the walkthrough: "Clinic admin (Dermatology). Clinician. Consultant dermatologist." */
export function accountDescription(person: DemoPerson): string {
  const type = accountTypeOfPerson(person);
  const clinic = TEAM_NAMES.find(([id]) => person.teams.some((t) => t.teamId === id && t.role === 'ClinicAdmin'))?.[1];
  const label = {Admin: 'Admin, read only', ClinicAdmin: `Clinic admin (${clinic})`, Staff: person.teams.length ? 'Staff (the lab)' : 'Staff'}[type];
  const roles = person.roles.filter((r) => r !== 'Admin');
  return `${label}.${roles.length ? ` ${roles.join(' and ')}.` : ''} ${person.jobTitle}.`;
}

/** How the walkthrough and the login page name an account: "the admin", or the person's name. */
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
    id: USERS.ferrand,
    username: 'c.ferrand',
    fullName: 'Clara Ferrand',
    jobTitle: 'Ophthalmic nurse',
    roles: ['Nurse'],
    permissions: ['UseApp', 'ViewPatients', 'WorkPathways', 'BookAppointments'],
    teams: [member(TEAMS.eyeClinic)],
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
  jobTitle: 'GP with an extended role (GPwER) in dermatology',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  teams: [member(TEAMS.dermatology)],
};

/** Everyone in the demo clinic. */
export const PEOPLE: DemoPerson[] = [ADMIN, STAFF, LAB, ...OTHERS, HALLWORTH, EYE_CLINIC_ADMIN, LOWER_LIMB_ADMIN];

export const personById = (id: string | null | undefined) => PEOPLE.find((person) => person.id === id);
export const personByUsername = (username: string | null | undefined) =>
  PEOPLE.find((person) => person.username.toLowerCase() === (username ?? '').toLowerCase());
export const usernameOf = (id: string | null | undefined) => personById(id)?.username ?? null;

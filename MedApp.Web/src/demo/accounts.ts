import {PERMISSIONS, type Permission} from '../auth/permissions';
import {SPECIALTIES, USERS} from './ids';

/** Someone in the demo clinic (names from the full demo data, `tools/MedApp.DevMcp/DemoData/DemoStaff.cs`). */
export interface DemoPerson {
  id: string;
  username: string;
  fullName: string;
  jobTitle: string;
  roles: string[];
  permissions: Permission[];
  specialtyIds: string[];
}

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
  'BookAppointments',
  'AttachFiles',
  'RecordSpecimens',
  'WriteLetters',
  'ApproveLetters',
  'SendLetters',
];

/** The admin account a visitor can be: Admin and Clinician, so everything. */
export const ADMIN: DemoPerson = {
  id: USERS.admin,
  username: 'admin',
  fullName: 'Clinical lead',
  jobTitle: 'Clinical lead, consultant physician (diabetes)',
  roles: ['Admin', 'Clinician'],
  permissions: [...PERMISSIONS],
  specialtyIds: [SPECIALTIES.diabetes, SPECIALTIES.dermatology],
};

/** The staff account a visitor can be: a Clinician, not an admin. */
export const STAFF: DemoPerson = {
  id: USERS.thornbury,
  username: 'n.thornbury',
  fullName: 'Nadia Thornbury',
  jobTitle: 'Consultant dermatologist',
  roles: ['Clinician'],
  permissions: CLINICIAN,
  specialtyIds: [SPECIALTIES.dermatology],
};

/** The two accounts the login page offers. */
export const DEMO_ACCOUNTS = [ADMIN, STAFF] as const;

/** The rest of the clinic, who appear in lists and on pathways but can't be signed in as. */
const OTHERS: DemoPerson[] = [
  {
    id: USERS.whitlock,
    username: 'j.whitlock',
    fullName: 'Jo Whitlock',
    jobTitle: 'Service manager',
    roles: ['Manager'],
    permissions: ['UseApp', 'ViewPatients', 'ViewInsights', 'ArrangeMeetings'],
    specialtyIds: [],
  },
  {
    id: USERS.sandiford,
    username: 'b.sandiford',
    fullName: 'Bea Sandiford',
    jobTitle: 'Clinical nurse specialist: skin cancer',
    roles: ['Nurse'],
    permissions: ['UseApp', 'ViewPatients', 'WorkPathways', 'BookAppointments'],
    specialtyIds: [SPECIALTIES.dermatology],
  },
  {
    id: USERS.marsh,
    username: 'h.marsh',
    fullName: 'Hannah Marsh',
    jobTitle: 'Booking coordinator',
    roles: ['Booking'],
    permissions: ['UseApp', 'ViewPatients', 'BookAppointments', 'RegisterReferrals'],
    specialtyIds: [],
  },
  {
    id: USERS.hallworth,
    username: 't.hallworth',
    fullName: 'Tom Hallworth',
    jobTitle: 'GP with a special interest in skin',
    roles: ['Clinician'],
    permissions: CLINICIAN,
    specialtyIds: [SPECIALTIES.dermatology],
  },
];

/** Everyone in the demo clinic. */
export const PEOPLE: DemoPerson[] = [ADMIN, STAFF, ...OTHERS];

export const personById = (id: string | null | undefined) => PEOPLE.find((person) => person.id === id);
export const personByUsername = (username: string | null | undefined) =>
  PEOPLE.find((person) => person.username.toLowerCase() === (username ?? '').toLowerCase());
export const usernameOf = (id: string | null | undefined) => personById(id)?.username ?? null;

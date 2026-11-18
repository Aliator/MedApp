/**
 * What a member of staff may do: the API's permission catalogue (`Permissions.Catalogue`, in
 * `MedApp.Application/Common/Identity/Permissions.cs`), copied here so pages can say what each allows.
 * `permissions.test.ts` reads the C# catalogue and fails when the two differ, so a permission added or reworded there
 * must be copied here.
 *
 * The API stays the authority: it refuses anything the signed-in person's roles don't allow (a 403 with
 * `code: "permission_missing"`). The web app only hides or disables what it would refuse (`useCan`).
 */
export const PERMISSIONS = [
  'UseApp',
  'ViewPatients',
  'EditPatients',
  'DeletePatients',
  'RegisterReferrals',
  'TriageReferrals',
  'CreatePathways',
  'WorkPathways',
  'AssignPathways',
  'UnlockStages',
  'FinishWithoutVisit',
  'DeletePathways',
  'BookAppointments',
  'ManageAnyCalendar',
  'ArrangeMeetings',
  'ManageClinics',
  'AttachFiles',
  'RecordSpecimens',
  'ProcessSpecimens',
  'SignOffResults',
  'WriteLetters',
  'ApproveLetters',
  'SendLetters',
  'ViewInsights',
  'ManageMonitoring',
  'ManageSetup',
  'ManageUsers',
  'ManageRoles',
  'ManageSecuritySettings',
  'ViewAudit',
  'ExportPatientRecords',
  'ManagePrivacy',
  'CorrectRecords',
] as const;

export type Permission = (typeof PERMISSIONS)[number];

/** The groups the roles page shows permissions in (the API's `PermissionGroup`). */
export type PermissionGroup =
  | 'Base'
  | 'Patients'
  | 'Referrals'
  | 'Pathways'
  | 'AppointmentsAndCalendars'
  | 'Files'
  | 'Lab'
  | 'Results'
  | 'Letters'
  | 'Oversight'
  | 'Setup'
  | 'AdministratorOnly';

export interface PermissionDescription {
  group: PermissionGroup;
  /** What it allows, in plain words, as the API's 403 and the roles page say it. */
  description: string;
}

export const PERMISSION_CATALOGUE: Record<Permission, PermissionDescription> = {
  UseApp: {
    group: 'Base',
    description: 'Use the app: your own account, settings, notifications, notepad and calendar; read calendars, meetings and the setup lists.',
  },
  ViewPatients: {group: 'Patients', description: 'See patients, their timeline, pathways, appointments, files and referrals.'},
  EditPatients: {group: 'Patients', description: 'Add and edit patients, and look them up in the demographics service.'},
  DeletePatients: {group: 'Patients', description: 'Delete a patient who has nothing attached.'},
  RegisterReferrals: {group: 'Referrals', description: 'Register, edit and withdraw referrals, and mark them entered in error.'},
  TriageReferrals: {group: 'Referrals', description: 'Accept, reject or redirect referrals in your specialties.'},
  CreatePathways: {group: 'Pathways', description: 'Start a pathway for a patient.'},
  WorkPathways: {
    group: 'Pathways',
    description: 'Work on pathways: stages, notes, details, hold, discharge and cancel; ask for a note to be deleted.',
  },
  AssignPathways: {group: 'Pathways', description: 'Assign staff to pathways, and unassign them.'},
  UnlockStages: {group: 'Pathways', description: 'Unlock a finished stage, and reinstate a skipped one.'},
  FinishWithoutVisit: {group: 'Pathways', description: 'Finish, or skip, a stage that requires a visit without one, giving a reason.'},
  DeletePathways: {group: 'Pathways', description: 'Delete a cancelled or archived pathway.'},
  BookAppointments: {
    group: 'AppointmentsAndCalendars',
    description: 'Request, book, move and cancel appointments, record outcomes and notes, and record a visit after the event.',
  },
  ManageAnyCalendar: {
    group: 'AppointmentsAndCalendars',
    description: "Change other people's calendars: their blocks, day and week templates and default week.",
  },
  ArrangeMeetings: {
    group: 'AppointmentsAndCalendars',
    description: 'Arrange, change and cancel meetings; see the team view and suggested times.',
  },
  ManageClinics: {group: 'AppointmentsAndCalendars', description: 'Set staff working hours.'},
  AttachFiles: {group: 'Files', description: 'Attach files, and delete your own.'},
  RecordSpecimens: {group: 'Lab', description: "Record specimens taken, sent and lost, and edit them before they're sent."},
  ProcessSpecimens: {
    group: 'Lab',
    description: 'Receive and reject specimens in the lab, mark them lost, and attach their reports.',
  },
  SignOffResults: {
    group: 'Results',
    description: 'Mark results seen, sign them off, and take them over or reassign them; be a responsible clinician.',
  },
  WriteLetters: {group: 'Letters', description: 'Write letters about patients from templates, and submit them for approval.'},
  ApproveLetters: {group: 'Letters', description: 'Approve or return letters submitted to you, and approve your own.'},
  SendLetters: {
    group: 'Letters',
    description: 'Send approved letters by secure email, or record them as posted or handed over.',
  },
  ViewInsights: {group: 'Oversight', description: 'See Insights.'},
  ManageMonitoring: {group: 'Oversight', description: 'Set up your own monitoring rules.'},
  ManageSetup: {
    group: 'Setup',
    description: 'Manage the stage library, pathway templates, specialties, activity types and closed days.',
  },
  ManageUsers: {
    group: 'AdministratorOnly',
    description: 'Manage accounts: create and delete them, give roles, reset passwords and two-factor, sign people out everywhere.',
  },
  ManageRoles: {group: 'AdministratorOnly', description: 'Manage roles and their permissions.'},
  ManageSecuritySettings: {group: 'AdministratorOnly', description: 'Change the security settings.'},
  ViewAudit: {group: 'AdministratorOnly', description: "See the pathway audit log, the activity log and the timeline's full view."},
  ExportPatientRecords: {group: 'AdministratorOnly', description: 'Export everything held about a patient.'},
  ManagePrivacy: {
    group: 'AdministratorOnly',
    description: 'Restrict, dispute and anonymise records, and review and keep them past retention.',
  },
  CorrectRecords: {
    group: 'AdministratorOnly',
    description: "Delete notes and decide deletion requests, delete anyone's file, and withdraw anyone's visit recorded after the event.",
  },
};

/** Whether `name` is a permission this app knows (the API may know one this copy of the page doesn't yet). */
export const isPermission = (name: unknown): name is Permission =>
  typeof name === 'string' && (PERMISSIONS as readonly string[]).includes(name);

/** The permissions in a whoami's list that this app knows, each once, in the catalogue's order. */
export const knownPermissions = (names: readonly (string | null | undefined)[] | null | undefined): Permission[] =>
  PERMISSIONS.filter((permission) => names?.includes(permission));

/** Whether someone's roles grant `permission` (`user` is the signed-in person, from whoami). */
export const can = (user: {permissions: readonly Permission[]}, permission: Permission) => user.permissions.includes(permission);

/**
 * "It needs the permission to: See Insights." The words the API's 403 uses, for a page or an action someone's roles
 * don't allow.
 */
export const needsPermissionText = (permission: Permission) =>
  `It needs the permission to: ${PERMISSION_CATALOGUE[permission].description}`;

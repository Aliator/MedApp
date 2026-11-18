import type {Role} from '../api/roles';
import {PERMISSIONS, type Permission} from '../auth/permissions';

/** The clinical permissions the built-in Admin role doesn't get (the API's `Permissions.NotForAdmin`). */
const NOT_FOR_ADMIN: readonly Permission[] = ['TriageReferrals', 'RecordSpecimens', 'ProcessSpecimens'];

/**
 * The permissions of the API's standard roles (`StandardRoles`): the built-in Admin's fixed set (every one but the
 * clinical ones, NOT_FOR_ADMIN), and "All staff (before roles)", the old User role, with what every member of staff could do
 * before roles. The starting roles follow decision 68 of `docs/centre-phase2-plan.md`.
 */
export const ROLE_PERMISSIONS = {
  Admin: PERMISSIONS.filter((p) => !NOT_FOR_ADMIN.includes(p)),
  'All staff (before roles)': [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways',
    'BookAppointments', 'AttachFiles',
  ],
  Clinician: [
    'UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'TriageReferrals', 'CreatePathways', 'WorkPathways',
    'OverrideStages', 'BookAppointments', 'AttachFiles', 'RecordSpecimens',
  ],
  Nurse: [
    'UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways', 'BookAppointments', 'AttachFiles',
    'RecordSpecimens',
  ],
  Booking: [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'BookAppointments', 'ManageAnyCalendar',
    'ManageClinics', 'AttachFiles',
  ],
  Lab: ['UseApp', 'ViewPatients', 'WorkPathways', 'AttachFiles', 'RecordSpecimens', 'ProcessSpecimens'],
  Manager: ['UseApp', 'ViewPatients', 'AssignPathways', 'ArrangeMeetings', 'ManageClinics', 'ViewInsights', 'ManageMonitoring'],
} satisfies Record<string, Permission[]>;

/**
 * A role as `GET api/roles` sends it, for mocking: `name`'s standard permissions when it's a standard role (without
 * UseApp, which the API doesn't list), editable unless it's Admin, held by nobody, at version `v1`.
 */
export const roleResponse = (name: string, extra: Partial<Role> = {}): Role => ({
  id: `role-${name.toLowerCase().replace(/[^a-z]+/g, '-')}`,
  name,
  description: `Synthetic ${name} role.`,
  isBuiltIn: name === 'Admin',
  isArchived: false,
  permissions: ((ROLE_PERMISSIONS as Record<string, readonly string[]>)[name] ?? []).filter((p) => p !== 'UseApp'),
  holderCount: 0,
  version: 'v1',
  canEdit: name !== 'Admin',
  canDelete: name !== 'Admin',
  createdAt: '2026-11-02T09:00:00Z',
  lastUpdated: '2026-11-02T09:00:00Z',
  ...extra,
});

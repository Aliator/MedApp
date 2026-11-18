import type {Role} from '../api/roles';
import {PERMISSIONS, type Permission} from '../auth/permissions';

/**
 * The permissions of the API's standard roles (`StandardRoles`): the built-in Admin's fixed set (every one but
 * TriageReferrals, FinishWithoutVisit and RecordMdtOutcomes), and "All staff (before roles)", the old User role, with what every member of staff could do
 * before roles. The starting roles follow decision 68 of `docs/centre-phase2-plan.md`, and Senior clinician decision 138
 * of `docs/centre-phases-3-5-plan.md`.
 */
export const ROLE_PERMISSIONS = {
  Admin: PERMISSIONS.filter((p) => p !== 'TriageReferrals' && p !== 'FinishWithoutVisit' && p !== 'RecordMdtOutcomes'),
  'All staff (before roles)': [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways',
    'BookAppointments', 'AttachFiles',
  ],
  Clinician: [
    'UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'TriageReferrals', 'CreatePathways', 'WorkPathways',
    'UnlockStages', 'FinishWithoutVisit', 'RecordMdtOutcomes', 'BookAppointments', 'AttachFiles',
  ],
  'Senior clinician': [
    'UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'TriageReferrals', 'CreatePathways', 'WorkPathways',
    'UnlockStages', 'FinishWithoutVisit', 'RecordMdtOutcomes', 'BookAppointments', 'AttachFiles',
  ],
  Nurse: ['UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways', 'BookAppointments', 'AttachFiles'],
  Booking: [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'BookAppointments', 'ManageAnyCalendar',
    'ManageClinics', 'AttachFiles',
  ],
  Lab: ['UseApp', 'ViewPatients', 'WorkPathways', 'AttachFiles'],
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

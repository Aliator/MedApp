import {PERMISSIONS, type Permission} from '../auth/permissions';

/**
 * The permissions of the API's standard roles (`StandardRoles`): the built-in Admin's fixed set (every one but
 * TriageReferrals), and "All staff (before roles)", the old User role, with what every member of staff could do
 * before roles. The starting roles follow decision 68 of `docs/centre-phase2-plan.md`.
 */
export const ROLE_PERMISSIONS = {
  Admin: PERMISSIONS.filter((p) => p !== 'TriageReferrals'),
  'All staff (before roles)': [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways',
    'BookAppointments', 'AttachFiles',
  ],
  Clinician: [
    'UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'TriageReferrals', 'CreatePathways', 'WorkPathways',
    'OverrideStages', 'BookAppointments', 'AttachFiles',
  ],
  Nurse: ['UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways', 'BookAppointments', 'AttachFiles'],
  Booking: [
    'UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'BookAppointments', 'ManageAnyCalendar',
    'ManageClinics', 'AttachFiles',
  ],
  Lab: ['UseApp', 'ViewPatients', 'WorkPathways', 'AttachFiles'],
  Manager: ['UseApp', 'ViewPatients', 'AssignPathways', 'ArrangeMeetings', 'ManageClinics', 'ViewInsights', 'ManageMonitoring'],
} satisfies Record<string, Permission[]>;

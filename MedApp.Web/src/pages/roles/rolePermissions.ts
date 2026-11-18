import {PERMISSION_CATALOGUE, PERMISSIONS, type Permission, type PermissionGroup} from '../../auth/permissions';

/**
 * The groups the role editor shows permissions in, in order, with their titles (decision 62). `Base` (UseApp alone,
 * which every role has) isn't a tick, so it isn't here.
 */
export const PERMISSION_GROUPS: {
  group: Exclude<PermissionGroup, 'Base'>;
  title: string;
}[] = [
  {group: 'Patients', title: 'Patients'},
  {group: 'Referrals', title: 'Referrals'},
  {group: 'Pathways', title: 'Pathways'},
  {group: 'AppointmentsAndCalendars', title: 'Appointments and calendars'},
  {group: 'Files', title: 'Files'},
  {group: 'Lab', title: 'Lab'},
  {group: 'Results', title: 'Results'},
  {group: 'Letters', title: 'Letters'},
  {group: 'Oversight', title: 'Oversight'},
  {group: 'Setup', title: 'Setup'},
  {group: 'AdministratorOnly', title: 'Administrator only'},
];

/** A permission's name in words, from its name: "ManageAnyCalendar" → "Manage any calendar". */
export function permissionLabel(permission: string): string {
  const words = permission.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** Whether only the built-in Admin role may have it: the API refuses it on any other role. */
export const isAdminOnly = (permission: Permission) => PERMISSION_CATALOGUE[permission].group === 'AdministratorOnly';

/**
 * Each group's permissions (in the catalogue's order) whose name, words or description contain `search` (ignoring
 * case), leaving out groups with none. With no search, every group.
 */
export function permissionGroups(search: string) {
  const term = search.trim().toLowerCase();
  const matches = (permission: Permission) =>
    !term ||
    permission.toLowerCase().includes(term) ||
    permissionLabel(permission).toLowerCase().includes(term) ||
    PERMISSION_CATALOGUE[permission].description.toLowerCase().includes(term);
  return PERMISSION_GROUPS.map(({group, title}) => ({
    group,
    title,
    permissions: PERMISSIONS.filter((p) => PERMISSION_CATALOGUE[p].group === group && matches(p)),
  })).filter((g) => g.permissions.length > 0);
}

/** What a save adds and removes, each in the catalogue's order. */
export function permissionChanges(before: readonly string[], after: readonly string[]) {
  return {
    added: PERMISSIONS.filter((p) => after.includes(p) && !before.includes(p)),
    removed: PERMISSIONS.filter((p) => before.includes(p) && !after.includes(p)),
  };
}

/** The permissions ticked, in the catalogue's order, each once (what the editor sends). */
export const inCatalogueOrder = (permissions: readonly string[]) => PERMISSIONS.filter((p) => permissions.includes(p));

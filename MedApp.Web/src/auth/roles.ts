/** The built-in role that runs MedApp (its permissions are fixed in the API's code). */
export const ADMIN_ROLE = 'Admin';

/** Someone's roles in the order to show them: Admin first, then the rest by name. */
export const sortRoles = (roles: readonly string[]) =>
  [...roles].sort((a, b) => Number(b === ADMIN_ROLE) - Number(a === ADMIN_ROLE) || a.localeCompare(b));

/**
 * Someone's roles in a few words, for where there's room for little (the side nav's account row): the first, Admin
 * if they have it, and how many more ("Admin +1"); "No role" when they have none.
 */
export function rolesSummary(roles: readonly string[]): string {
  const sorted = sortRoles(roles);
  if (sorted.length === 0) return 'No role';
  return sorted.length === 1 ? sorted[0] : `${sorted[0]} +${sorted.length - 1}`;
}

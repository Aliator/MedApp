import {redirect} from '@tanstack/react-router';

/**
 * Pathways were called procedures, and pathway templates plain templates; teams were called specialties (MC2).
 * Addresses, searches and settings from then still arrive: a bookmark, a link someone kept, a half view saved in a
 * tab, a saved search or pins in someone's settings. This module reads them as what they are now, and is the one place in the web app that knows
 * the old names. (docs/glossary.md has both vocabularies.)
 */

/** Each old address, and the one its page has now. Calendar templates were never at /admin/templates. */
const OLD_ADDRESSES: readonly (readonly [RegExp, string])[] = [
  [/^\/procedures(?=\/|$)/, '/pathways'],
  [/^(\/patients\/[^/]+)\/procedures(?=\/|$)/, '$1/pathways'],
  [/^\/admin\/templates(?=\/|$)/, '/admin/pathway-templates'],
  [/^\/admin\/specialties(?=\/|$)/, '/admin/teams'],
];

/** The address a page from before the rename has now, or undefined for any other address. */
export function addressNow(pathname: string): string | undefined {
  for (const [old, now] of OLD_ADDRESSES) if (old.test(pathname)) return pathname.replace(old, now);
  return undefined;
}

/**
 * The root route's `beforeLoad`: sends an old address on to the page's address now, with the same search, replacing
 * it in the history (so Back doesn't return to it). It runs before the route guards, on the server and in the browser.
 */
export function leaveOldAddress({location}: {location: {pathname: string; searchStr: string; hash: string}}) {
  const now = addressNow(location.pathname);
  if (now) throw redirect({href: `${now}${location.searchStr}${location.hash ? `#${location.hash}` : ''}`, replace: true});
}

/**
 * A list's search as it was written before the renames, by the names it has now: the audit log's `procedureId`, a sort
 * by the Procedure column, and the activity log's ProcedureViewed action; and a `specialty` filter, a sort by the
 * Specialty column and the StaffSpecialtiesChanged action (saved searches in people's settings keep these). Only
 * those: a search for the word "procedure" in a title stays as typed.
 */
export function searchNow(raw: Record<string, unknown>): Record<string, unknown> {
  const now: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(raw)) {
    if (key === 'procedureId') now.pathwayId ??= value;
    else if (key === 'specialty') now.team ??= value;
    else if (key === 'sort' && value === 'Procedure') now.sort = 'Pathway';
    else if (key === 'sort' && value === 'Specialty') now.sort = 'Team';
    else if (key === 'action' && typeof value === 'string')
      now.action = value.replace(/\bProcedureViewed\b/g, 'PathwayViewed').replace(/\bStaffSpecialtiesChanged\b/g, 'TeamMembershipChanged');
    else now[key] = value;
  }
  return now;
}

/**
 * The side nav's pins as they're stored (`medapp.pins`), by the names they have now. Settings saved before the rename
 * keep the pinned pathways under `procedures`: they're read from there until the pins are next saved, which writes
 * them under `pathways`.
 */
export function pinsNow(stored: unknown): {patients?: unknown; pathways?: unknown} | null {
  if (typeof stored !== 'object' || stored === null) return null;
  const {patients, pathways, procedures} = stored as {patients?: unknown; pathways?: unknown; procedures?: unknown};
  return {patients, pathways: pathways ?? procedures};
}

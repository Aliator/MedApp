import {PAGE_SIZE} from '../../api/search';
import type {SetupListSearch} from '../listSearch';

/**
 * One page of a setup list (stage definitions or templates). These lists are short and come whole from the API,
 * so they're filtered, sorted (by name) and paged here: archived ones only when asked for.
 */
export function setupPage<T extends {name?: string | null; isArchived?: boolean}>(items: readonly T[], search: SetupListSearch) {
  const shown = items
    .filter((item) => search.archived || !item.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const pages = Math.max(1, Math.ceil(shown.length / PAGE_SIZE));
  const page = Math.min(search.page ?? 1, pages);
  return {
    rows: shown.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    page,
    total: shown.length,
    archivedCount: items.filter((item) => item.isArchived).length,
  };
}

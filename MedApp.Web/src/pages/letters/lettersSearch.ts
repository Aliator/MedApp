import {LETTER_VIEWS, letterSearchQuery, type LetterView} from '../../api/letters';
import type {Permission} from '../../auth/permissions';
import {parseClinic} from '../listSearch';

/** A view of the Letters page: one of its views, or All (its view filter removed), newest started first. */
export type LettersView = LetterView | 'All';

/** The columns the Letters page sorts by, as the API names them (`letter` is the kind, then the template). */
export const LETTER_SORTS = ['patient', 'letter', 'status', 'latest'] as const;
export type LetterSortColumn = (typeof LETTER_SORTS)[number];

/**
 * The Letters page's address: its view (`view=ToSend`, as the API names it), the patient's name searched for, the page
 * and the sort. Without a view, the page shows the one the person most likely wants (`defaultLetterView`), so the side
 * nav's link has no search at all.
 */
export interface LettersListSearch {
  view?: LettersView;
  /** The patient's name contains this. */
  patient?: string;
  page?: number;
  sort?: LetterSortColumn;
  asc?: boolean;
  /** The clinic picker's team, or `all` (see `parseClinic`). */
  clinic?: string;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseLettersSearch(raw: Record<string, unknown>): LettersListSearch {
  const page = Number(text(raw.page));
  const view = text(raw.view)?.toLowerCase();
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: LettersListSearch = {
    view: view === 'all' ? 'All' : LETTER_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    patient: text(raw.patient),
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: LETTER_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
    clinic: parseClinic(raw.clinic),
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as LettersListSearch;
}

/**
 * The view the page opens on: My drafts for someone who writes letters, Awaiting my approval for an approver who
 * doesn't, otherwise To send.
 */
export function defaultLetterView(permissions: readonly Permission[]): LetterView {
  if (permissions.includes('WriteLetters')) return 'MyDrafts';
  if (permissions.includes('ApproveLetters')) return 'AwaitingMyApproval';
  return 'ToSend';
}

/**
 * The order the list shows: the address's (a sort named without a direction is ascending), or the view's own, by the
 * latest step where it orders by that (drafts and sent newest first, those to approve and to send oldest first). None
 * is marked for Returned to me (by when returned) or All (by when started), which no column shows.
 */
export function lettersSortOf(search: LettersListSearch, view: LettersView): {sort: LetterSortColumn; asc: boolean} | null {
  if (search.sort) return {sort: search.sort, asc: search.asc ?? true};
  if (view === 'MyDrafts' || view === 'Sent') return {sort: 'latest', asc: false};
  if (view === 'AwaitingMyApproval' || view === 'ToSend') return {sort: 'latest', asc: true};
  return null;
}

/**
 * The list's page of letters, for its view, search, page, page size and the clinic picker's team (the route's loader
 * fetches the same).
 */
export const lettersListQuery = (view: LettersView, search: LettersListSearch, pageSize: number, teamId?: string) =>
  letterSearchQuery({
    View: view,
    Search: search.patient,
    Sort: search.sort,
    Ascending: search.sort ? (search.asc ?? true) : undefined,
    Page: search.page ?? 1,
    PageSize: pageSize,
    TeamId: teamId,
  });

import {LETTER_VIEWS, letterSearchQuery, type LetterView} from '../../api/letters';
import type {Permission} from '../../auth/permissions';

/**
 * The Letters page's address: its view (`view=ToSend`, as the API names it) and page. Without a view, the page shows
 * the one the person most likely wants (`defaultLetterView`), so the side nav's link has no search at all.
 */
export interface LettersListSearch {
  view?: LetterView;
  page?: number;
}

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseLettersSearch(raw: Record<string, unknown>): LettersListSearch {
  const page = Number(text(raw.page));
  const view = text(raw.view)?.toLowerCase();
  const search: LettersListSearch = {
    view: LETTER_VIEWS.find((v) => v.value.toLowerCase() === view)?.value,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
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

/** The list's page of letters, for its view, page and page size (the route's loader fetches the same). */
export const lettersListQuery = (view: LetterView, page: number, pageSize: number) =>
  letterSearchQuery({View: view, Page: page, PageSize: pageSize});

/**
 * The reader's place on the Read Me, kept while a tour button moves it: opening a page beside it from the Read Me on
 * its own (it moves to the right half, a new page there), switching account (a full reload), and going back to it on
 * its own (`openFull`, or closing the half beside it). Each would otherwise start it again at the top. The button's
 * part of the page (a tour step, or the Two accounts card's footer, marked `data-read-me-place`) is kept with how far
 * down the scrolling area it was, and once the Read Me shows again where it was going, that part is put back there.
 *
 * UI state only, in this tab's `sessionStorage` under a `medapp.` key (so sign-out clears it), never anything about
 * a patient.
 */

const KEY = 'medapp.readMePlace';

/** A place older than this is stale (the Read Me never showed where it was going): it's dropped, not used. */
const MAX_AGE_MS = 30_000;

/** Where the Read Me shows next: in the right half of a half view, or on its own (the whole view). */
export type ReadMePosition = 'right' | 'whole';

/** The attribute that marks a part of the Read Me a place can be kept for. */
export const PLACE_ATTRIBUTE = 'data-read-me-place';

interface StoredPlace {
  place: string;
  /** The part's top, in pixels below the top of the scrolling area's visible part (negative when above it). */
  offset: number;
  position: ReadMePosition;
  at: number;
}

/** The part in this Read Me (`root`): the one leaving the left half may still be there as the new one shows. */
const partOf = (root: HTMLElement, place: string): HTMLElement | null =>
  Array.from(root.querySelectorAll<HTMLElement>(`[${PLACE_ATTRIBUTE}]`)).find((part) => part.getAttribute(PLACE_ATTRIBUTE) === place) ?? null;

/** The element that scrolls the Read Me: its half's scroller in a half view, or the main area; the page's otherwise (the nearest one around it that can scroll and has more than it shows). */
export function scrollerOf(element: HTMLElement): HTMLElement {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const {overflowY} = getComputedStyle(parent);
    // Only one that does scroll: a card on the way may allow it without having anything to scroll.
    if ((overflowY === 'auto' || overflowY === 'scroll') && parent.scrollHeight > parent.clientHeight) return parent;
  }
  return (document.scrollingElement as HTMLElement | null) ?? document.documentElement;
}

/** The top of the scroller's visible part, in the window. */
const topOf = (scroller: HTMLElement) =>
  scroller === document.scrollingElement || scroller === document.documentElement ? 0 : scroller.getBoundingClientRect().top;

/** Keeps where the reader is (`place`, a marked part of this Read Me, `root`) before the Read Me moves to `position`. */
export function rememberReadMePlace(root: HTMLElement | null, place: string, position: ReadMePosition): void {
  const part = root && partOf(root, place);
  if (!part) return;
  const stored: StoredPlace = {place, offset: part.getBoundingClientRect().top - topOf(scrollerOf(part)), position, at: Date.now()};
  try {
    sessionStorage.setItem(KEY, JSON.stringify(stored));
  } catch {
    // Without storage, the Read Me starts at the top.
  }
}

function readStored(): StoredPlace | null {
  try {
    const stored: unknown = JSON.parse(sessionStorage.getItem(KEY) ?? 'null');
    if (!stored || typeof stored !== 'object') return null;
    const {place, offset, position, at} = stored as Partial<StoredPlace>;
    return typeof place === 'string' && typeof offset === 'number' && (position === 'right' || position === 'whole') && typeof at === 'number'
      ? {place, offset, position, at}
      : null;
  } catch {
    return null;
  }
}

function forget() {
  try {
    sessionStorage.removeItem(KEY);
  } catch {
    // Without storage there's nothing kept.
  }
}

/** Whether the page itself scrolls (the last resort for a scroller). */
const pageScrolls = () => {
  const page = document.scrollingElement ?? document.documentElement;
  return page.scrollHeight > page.clientHeight;
};

/**
 * Puts the reader back where they were in this Read Me (`root`), if a place was kept for the Read Me showing at `position` (a Read Me shown
 * anywhere else, such as the one briefly in the left half as the page beside it opens, leaves it for the right one),
 * then forgets it: `restored`, `none` (nothing kept for it), or `not yet` while the Read Me isn't laid out in
 * something that scrolls (a half pane's page settling), for the caller to try again on a later frame.
 */
export function restoreReadMePlace(root: HTMLElement | null, position: ReadMePosition): 'restored' | 'none' | 'not yet' {
  const stored = readStored();
  if (!stored) return 'none';
  if (Date.now() - stored.at > MAX_AGE_MS) {
    forget();
    return 'none';
  }
  if (stored.position !== position) return 'none';
  const part = root && partOf(root, stored.place);
  if (!part) return 'not yet';
  const scroller = scrollerOf(part);
  const isPage = scroller === (document.scrollingElement ?? document.documentElement);
  if (isPage && !pageScrolls()) return 'not yet';
  forget();
  scroller.scrollTop += part.getBoundingClientRect().top - topOf(scroller) - stored.offset;
  return 'restored';
}

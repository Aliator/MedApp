import {useAppShellMobile} from '@astryxdesign/core/AppShell';
import {useRouter} from '@tanstack/react-router';
import {useEffect, useRef, useSyncExternalStore} from 'react';
import {useHalfViews} from '../pages/halfView';

/**
 * Marks an element that isn't a link (the side nav's Notepad toggle) with the page a Shift-click or Ctrl-click on it
 * opens in the half view. Such a page only ever opens on the right (the notepad's page has no main view of its own).
 */
export const HALF_VIEW_HREF = 'data-half-view-href';

/** `basepath` (the light demo's `/MedApp/`) taken off an address, so it's one the routers know. */
function withoutBase(href: string, basepath: string | undefined): string {
  const base = (basepath ?? '/').replace(/\/$/, '');
  return base && (href === base || href.startsWith(`${base}/`)) ? href.slice(base.length) || '/' : href;
}

/**
 * The in-app page a Shift-click or Ctrl-click (or the same with Enter) on this element opens, or null where it's left
 * to the browser: a link (`a[href]`, which every Astryx link, `href` button and nav item renders) to an address in the
 * app (one starting with `/`, not `//`), not a download, not opening in another tab (`target`), and not disabled. Or
 * an element marked with `HALF_VIEW_HREF`.
 */
export function halfViewHref(target: EventTarget | null, basepath?: string): string | null {
  if (!(target instanceof Element)) return null;
  const marked = target.closest(`[${HALF_VIEW_HREF}]`);
  if (marked) return marked.getAttribute(HALF_VIEW_HREF);
  const link = target.closest('a[href]');
  if (!link || link.hasAttribute('download') || link.getAttribute('aria-disabled') === 'true') return null;
  // A control inside the link (a button, a field) keeps its own Shift- or Ctrl-click.
  const control = target.closest('button, input, textarea, select, [contenteditable]');
  if (control && control !== link && link.contains(control)) return null;
  const opensIn = link.getAttribute('target');
  if (opensIn && opensIn !== '_self') return null;
  const href = link.getAttribute('href')!;
  // Same-app paths only: `//host` and `/\host` (which browsers read as `//host`) leave the site, so they're refused.
  if (!href.startsWith('/') || href.startsWith('//') || href.startsWith('/\\')) return null;
  if (typeof window !== 'undefined' && new URL(href, window.location.origin).origin !== window.location.origin) return null;
  return withoutBase(href, basepath);
}

/**
 * Which side a click opens its page on: Shift on the right (the half view), Ctrl (⌘ on a Mac, where Ctrl-click is a
 * right-click) on the left, or null for any other click (no modifier, Alt, or Shift with Ctrl).
 */
export function halfViewSide(event: {shiftKey: boolean; ctrlKey: boolean; metaKey: boolean; altKey: boolean}): 'left' | 'right' | null {
  if (event.altKey) return null;
  const mod = event.ctrlKey || event.metaKey;
  if (event.shiftKey && !mod) return 'right';
  if (mod && !event.shiftKey) return 'left';
  return null;
}

const pathOf = (href: string) => href.split(/[?#]/)[0];

/**
 * Shift-click and Ctrl-click (⌘-click on a Mac) on any in-app link or nav item open its page in a half view:
 * - **Shift-click:** on the right, beside the current page, or in place of the half view's page when one is open
 *   (`halfViews.open`).
 * - **Ctrl-click:** on the left, the page that was there moving to the right (beside it, or in place of the half view's
 *   page): `halfViews.openOnLeft`.
 * - **The keyboard:** Shift+Enter and Ctrl+Enter on a focused link do the same.
 *
 * One listener on the document, in the capture phase, so it sees the click before any link, button or nav item does,
 * and every one of them works the same way without code of its own. The click stops here: nothing else (the link's own
 * navigation, the browser's new window or tab, the nav item's toggle) acts on it as well.
 *
 * Left alone, to do what they always do: plain, Alt and middle clicks (the browser's new tab), and the right-click menu;
 * external links, downloads and links opening in another tab; anything that isn't a link (a dialog's buttons); and
 * everything on a phone, where AppShell has no room for a half view. A link to the page already on the left opens as a
 * plain click would, as a page is only open once side by side (the nav menu greys that out too). The notepad (marked
 * with `HALF_VIEW_HREF`) opens on the right either way, as its page is only ever shown in the half view.
 */
export function HalfViewClicks() {
  const halfViews = useHalfViews();
  const router = useRouter();
  const {isMobile} = useAppShellMobile();
  const latest = useRef({halfViews, isMobile});
  useEffect(() => {
    latest.current = {halfViews, isMobile};
  });

  useEffect(() => {
    const doc = document;
    const take = (event: MouseEvent | KeyboardEvent): void => {
      const {halfViews: views, isMobile: isPhone} = latest.current;
      if (!views || isPhone || event.defaultPrevented) return;
      const side = halfViewSide(event);
      if (!side) return;
      const basepath = router.options.basepath;
      const href = halfViewHref(event.target, basepath);
      if (!href) return;
      event.preventDefault();
      event.stopPropagation();
      const element = (event.target as Element).closest<HTMLElement>(`[${HALF_VIEW_HREF}], a[href]`);
      if (element?.hasAttribute(HALF_VIEW_HREF)) {
        views.open(href);
        return;
      }
      if (pathOf(href) === pathOf(withoutBase(router.state.location.pathname, basepath))) {
        // Already on the left: what a plain click does (the link's own handlers, or the browser's).
        element?.dispatchEvent(new MouseEvent('click', {bubbles: true, cancelable: true, button: 0}));
        return;
      }
      if (side === 'right') views.open(href);
      else views.openOnLeft(href);
    };
    const onClick = (event: MouseEvent) => {
      if (event.button === 0) take(event);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      // Only on a link: Shift+Enter or Ctrl+Enter elsewhere (a field, a button) is that element's own.
      if (event.key === 'Enter' && event.target instanceof Element && event.target.closest('a[href]')) take(event);
    };
    doc.addEventListener('click', onClick, true);
    doc.addEventListener('keydown', onKeyDown, true);
    return () => {
      doc.removeEventListener('click', onClick, true);
      doc.removeEventListener('keydown', onKeyDown, true);
    };
  }, [router]);

  return null;
}

const isApple = () => /Mac|iPhone|iPad|iPod/i.test(navigator.platform || navigator.userAgent);
const subscribeNever = () => () => {};

/**
 * The words for a Ctrl-click: "⌘-click" on a Mac (where Ctrl-click is a right-click), "Ctrl-click" elsewhere. "Ctrl-click"
 * on the server and while hydrating, so the two match, then the browser's.
 */
export function useCtrlClickWords(): string {
  return useSyncExternalStore(subscribeNever, () => (isApple() ? '⌘-click' : 'Ctrl-click'), () => 'Ctrl-click');
}

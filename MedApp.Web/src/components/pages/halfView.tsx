import {createMemoryHistory, createRouter, RouterProvider, useRouter, type AnyRouter} from '@tanstack/react-router';
import {createContext, useContext, useEffect, useState} from 'react';

/**
 * Half views: two pages side by side, each in its narrow ("half view") layout. A side nav item's half-view button
 * opens its page beside the current one and folds the side nav away; closing either half goes back to one page,
 * the other one, with the side nav open again.
 */

/** Where a page is being shown: in a half view (with a way to close that half), or on its own. */
export interface HalfView {
  isHalf: boolean;
  /** Which half: the main page is on the left, the half view's on the right. */
  side?: 'left' | 'right';
  /** Closes this half, leaving the other page on its own. */
  onClose?: () => void;
}

export const HalfViewContext = createContext<HalfView>({isHalf: false});

/** This page's half view: whether it's in one, and how to close it. */
export const useHalfView = () => useContext(HalfViewContext);

/** Opening half views, from the side nav. */
export interface HalfViews {
  /** Opens this page (a path) beside the current one, replacing any page already there. */
  open: (path: string) => void;
  /** The page the half view shows (its path, without a query), or null when there's none. */
  openPath: string | null;
  /** Closes the half view and opens this page in full view (the main view, with the nav open again). */
  openFull: (path: string) => void;
  /**
   * Opens this page on the left (the main view), moving the page that was there to the right: beside it, or in place
   * of the half view's page (Ctrl-click, `HalfViewClicks`).
   */
  openOnLeft: (path: string) => void;
  /**
   * Opens this page on the left (the main view), keeping any half view as it is: for a page in the half view to open
   * one beside itself (a page followed on the right).
   */
  showOnLeft: (path: string) => void;
  /** Closes the half view, leaving the main page on its own (with the nav open again). */
  close: () => void;
}

export const HalfViewsContext = createContext<HalfViews | null>(null);

/** The way to open half views, or null where there is none (in a half view itself, and in tests of one part). */
export const useHalfViews = () => useContext(HalfViewsContext);

/**
 * Inside a dialog that sits over the whole window (a page on its own, a phone): what's in it lays out by the window
 * alone (`DialogFrame` sets it; `useIsNarrow` reads it). A dialog over its own region (`DialogRegionContext`) doesn't
 * set it: what's in it lays out by that region, as the page under it does.
 */
export const OverWholeWindowContext = createContext(false);

/**
 * The part of the window a page is shown in, for its dialogs: a half of a half view, or the page beside the docked
 * notepad. While something sits beside the page (`isBeside`), a dialog it opens covers only this region, so what's
 * beside it (the other half, the notepad, the side nav) stays usable, to copy from (`RegionDialog`). Otherwise
 * dialogs sit over the whole window. `AppLayout` provides it; there's none outside the signed-in layout.
 */
export interface DialogRegion {
  /** The region's element (what the page scrolls in), made inert while a dialog covers it; null before it mounts. */
  element: HTMLElement | null;
  /** Where dialogs over a region are rendered: after the app shell, so they're above the page and its sticky header. */
  host: HTMLElement | null;
  /** Whether something is beside the page: the other half of a half view, or the docked notepad. */
  isBeside: boolean;
}

export const DialogRegionContext = createContext<DialogRegion | null>(null);

/** The region a dialog opened here covers, or null when it would sit over the whole window. */
export function useDialogRegion(): DialogRegion | null {
  const region = useContext(DialogRegionContext);
  return region !== null && region.isBeside && region.element !== null && region.host !== null ? region : null;
}

/**
 * Inside a half view's own router. Its pages render without the app shell (the side nav is the main page's).
 */
export const InHalfPaneContext = createContext(false);

export interface HalfPaneProps {
  /** The page it opens on. */
  path: string;
  /** Called with the page's address each time it changes, so closing the other half can open it full size. */
  onPathChange: (path: string) => void;
}

/**
 * The second page of a half view: the app's own routes, in a router of its own whose history is in memory, so
 * links and Back inside it change only this half. It shares the main router's routes and context (and so the
 * query cache), and opens on `path`.
 */
export function HalfPane({path, onPathChange}: HalfPaneProps) {
  const main = useRouter();
  const [router] = useState<AnyRouter>(() =>
    createRouter({
      routeTree: main.routeTree,
      context: main.options.context,
      history: createMemoryHistory({initialEntries: [path]}),
      defaultPreload: 'intent',
    }),
  );

  useEffect(
    () => router.subscribe('onResolved', ({toLocation}) => onPathChange(toLocation.href)),
    [router, onPathChange],
  );

  return (
    <InHalfPaneContext.Provider value={true}>
      <RouterProvider router={router} />
    </InHalfPaneContext.Provider>
  );
}

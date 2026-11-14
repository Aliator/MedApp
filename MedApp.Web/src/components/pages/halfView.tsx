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
}

export const HalfViewsContext = createContext<HalfViews | null>(null);

/** The way to open half views, or null where there is none (in a half view itself, and in tests of one part). */
export const useHalfViews = () => useContext(HalfViewsContext);

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

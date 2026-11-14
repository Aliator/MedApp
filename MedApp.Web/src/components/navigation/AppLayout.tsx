import {AppShell} from '@astryxdesign/core/AppShell';
import {Divider} from '@astryxdesign/core/Divider';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {Theme} from '@astryxdesign/core/theme';
import {Outlet, useRouter} from '@tanstack/react-router';
import {useCallback, useContext, useEffect, useMemo, useRef, useState} from 'react';
import {defaultThemeSlug, themes} from '../../astryx-themes';
import {HalfPane, HalfViewContext, HalfViewsContext, InHalfPaneContext, type HalfView} from '../pages/halfView';
import {AppSideNav} from './AppSideNav';

const STORAGE_KEY = 'medapp.halfView';

/** A half of a half view scrolls up and down only. */
const HALF = {overflowX: 'hidden'} as const;

/** The half view's page, kept for the browser tab so a reload keeps it (storage may be unavailable). */
function storedPath(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function storePath(path: string | null) {
  try {
    if (path) sessionStorage.setItem(STORAGE_KEY, path);
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Without storage, a reload just closes the half view.
  }
}

/**
 * The signed-in frame: side navigation plus the current page (replaces Blazor's MainLayout).
 *
 * With a half view open, the current page and the half view's page sit side by side, each in its narrow layout,
 * and the side nav is folded to its icons (its button opens it again). Closing either half goes back to the
 * other page on its own, with the side nav open.
 *
 * Inside a half view's own router, this is just the page: the shell is the main page's.
 */
export function AppLayout() {
  return useContext(InHalfPaneContext) ? <Outlet /> : <MainLayout />;
}

function MainLayout() {
  const router = useRouter();
  // The half view's page; `key` starts it afresh when another page is opened in it.
  const [half, setHalf] = useState<{path: string; key: number} | null>(() => {
    const path = storedPath();
    return path ? {path, key: 0} : null;
  });
  const [isNavCollapsed, setIsNavCollapsed] = useState(half !== null);
  // Where the half view has got to: closing the main half opens it full size, and the nav won't open it twice.
  const [halfPath, setHalfPath] = useState(half?.path ?? null);

  const trackHalf = useCallback((path: string) => {
    setHalfPath(path);
    storePath(path);
  }, []);

  const open = useCallback((path: string) => {
    setHalfPath(path);
    storePath(path);
    setHalf({path, key: Date.now()});
    setIsNavCollapsed(true);
  }, []);
  // Back to one page: the half view closes and the nav opens again.
  const closeHalf = useCallback(() => {
    setHalfPath(null);
    storePath(null);
    setHalf(null);
    setIsNavCollapsed(false);
  }, []);
  const openFull = useCallback(
    (path: string) => {
      closeHalf();
      void router.navigate({href: path});
    },
    [closeHalf, router],
  );
  const halfViews = useMemo(
    () => ({
      open,
      openFull,
      openPath: half && halfPath ? halfPath.split('?')[0] : null,
    }),
    [open, openFull, half, halfPath],
  );

  const close = (showHalfPage: boolean) => {
    const path = halfPath;
    closeHalf();
    if (showHalfPage && path) void router.navigate({href: path});
  };

  // The same page can't be open twice: when the main view goes to the half view's page, the half view closes
  // (the main view has it now).
  const halfPage = useRef<string | null>(null);
  useEffect(() => {
    halfPage.current = half && halfPath ? halfPath.split('?')[0] : null;
  }, [half, halfPath]);
  useEffect(
    () =>
      router.subscribe('onResolved', ({toLocation}) => {
        if (halfPage.current === null || toLocation.pathname !== halfPage.current) return;
        halfPage.current = null;
        closeHalf();
      }),
    [closeHalf, router],
  );
  const mainHalf: HalfView = {isHalf: true, onClose: () => close(true)};
  const otherHalf: HalfView = {isHalf: true, onClose: () => close(false)};

  return (
    <HalfViewsContext.Provider value={halfViews}>
      <AppShell
        sideNav={
          // The side nav is dark in both modes: in light mode it keeps the dark mode's colours.
          <Theme theme={themes[defaultThemeSlug]} mode="dark">
            <AppSideNav
              collapsible={{
                isCollapsed: isNavCollapsed,
                onCollapsedChange: setIsNavCollapsed,
                hasButton: half !== null,
              }}
            />
          </Theme>
        }
        // In a half view, each half's page frame insets it instead (see below).
        contentPadding={half ? 0 : 6}>
        {half ? (
          // Each half scrolls on its own: a long page scrolls in its half, not the whole window, and only up and
          // down (a page that's too wide for its half shrinks or wraps its parts instead). The shell has no padding
          // here, so each page's frame insets it from the window's edges and the divider by the usual 24px, and
          // fits its half exactly (it would otherwise reach out over the shell's padding, and overflow its half).
          <HStack gap={0} height="100%">
            <StackItem size="fill" isScrollable aria-label="Left half" style={HALF}>
              <HalfViewContext.Provider value={mainHalf}>
                <Outlet />
              </HalfViewContext.Provider>
            </StackItem>
            <Divider orientation="vertical" style={{marginBlock: 'var(--spacing-6)', blockSize: 'auto', alignSelf: 'stretch'}} />
            <StackItem size="fill" isScrollable aria-label="Right half" style={HALF}>
              <HalfViewContext.Provider value={otherHalf}>
                <HalfPane key={half.key} path={half.path} onPathChange={trackHalf} />
              </HalfViewContext.Provider>
            </StackItem>
          </HStack>
        ) : (
          <Outlet />
        )}
      </AppShell>
    </HalfViewsContext.Provider>
  );
}

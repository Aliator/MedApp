import {AppShell} from '@astryxdesign/core/AppShell';
import {Divider} from '@astryxdesign/core/Divider';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {Outlet, useRouter} from '@tanstack/react-router';
import {useCallback, useContext, useMemo, useState} from 'react';
import {HalfPane, HalfViewContext, HalfViewsContext, InHalfPaneContext, type HalfView} from '../pages/halfView';
import {AppSideNav} from './AppSideNav';

const STORAGE_KEY = 'medapp.halfView';

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
  const halfViews = useMemo(() => ({open, openPath: half && halfPath ? halfPath.split('?')[0] : null}), [open, half, halfPath]);

  const close = (showHalfPage: boolean) => {
    const path = halfPath;
    setHalfPath(null);
    storePath(null);
    setHalf(null);
    setIsNavCollapsed(false);
    if (showHalfPage && path) void router.navigate({href: path});
  };
  const mainHalf: HalfView = {isHalf: true, onClose: () => close(true)};
  const otherHalf: HalfView = {isHalf: true, onClose: () => close(false)};

  return (
    <HalfViewsContext.Provider value={halfViews}>
      <AppShell
        sideNav={
          <AppSideNav
            collapsible={{isCollapsed: isNavCollapsed, onCollapsedChange: setIsNavCollapsed, hasButton: half !== null}}
          />
        }
        contentPadding={6}>
        {half ? (
          // The gap either side of the divider matches the content's inset from the window's edges.
          <HStack gap={6} height="100%">
            <StackItem size="fill">
              <HalfViewContext.Provider value={mainHalf}>
                <Outlet />
              </HalfViewContext.Provider>
            </StackItem>
            <Divider orientation="vertical" />
            <StackItem size="fill">
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

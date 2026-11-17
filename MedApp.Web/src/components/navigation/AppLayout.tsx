import {AppShell} from '@astryxdesign/core/AppShell';
import {Divider} from '@astryxdesign/core/Divider';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {useSideNavRenderMode} from '@astryxdesign/core/SideNav';
import {Theme} from '@astryxdesign/core/theme';
import {Outlet, useRouter} from '@tanstack/react-router';
import {useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode} from 'react';
import {LiveNotifications} from '../../app/LiveNotifications';
import {SettingsSync} from '../../app/SettingsSync';
import {defaultThemeSlug, themes} from '../../astryx-themes';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {NOTEPAD_WIDTH, NotepadProvider, useNotepadDocked} from '../notepad/NotepadContext';
import {NotepadPane} from '../notepad/NotepadPane';
import {NotepadSheet} from '../notepad/NotepadSheet';
import {HalfPane, HalfViewContext, HalfViewsContext, InHalfPaneContext, type HalfView} from '../pages/halfView';
import {AppSideNav} from './AppSideNav';
import {KeyboardShortcuts} from './KeyboardShortcuts';
import {SessionTimeout} from './SessionTimeout';

const STORAGE_KEY = 'medapp.halfView';

/**
 * A half of a half view scrolls up and down only, and keeps room for a scrollbar on both sides whether or not its page
 * scrolls (see `STABLE_SCROLLBAR`), so the divider between the halves stays put, in the middle, when either starts to.
 */
const HALF = {overflowX: 'hidden', scrollbarGutter: 'stable both-edges'} as const;

/** A wrapper that takes no part in the layout: its children are laid out as if it weren't there. */
const CONTENTS = {display: 'contents'} as const;

/** The half view's two pages beside the docked notepad: they take the room left, and each half scrolls on its own. */
const FILL = {minInlineSize: 0} as const;

/** The docked notepad's column, the full height beside the page. */
const NOTEPAD_COLUMN = {inlineSize: NOTEPAD_WIDTH, flexShrink: 0, blockSize: '100%'} as const;

/** The line between the two halves of a half view, inset from the top and bottom like the pages. */
const DIVIDER = {marginBlock: 'var(--spacing-6)', blockSize: 'auto', alignSelf: 'stretch'} as const;

/**
 * The window's main area keeps room for a scrollbar whether or not its page scrolls. Where scrollbars take up space
 * (Windows, and most Linux desktops), a page is centred in what's left, so without this a page that scrolls (Account
 * Settings, the accessibility statement) sat a few pixels away from one that doesn't. The room is kept on both sides,
 * so the margins stay even. AppShell has no prop for it, so it's a rule on its main area (`role="main"`). In a half
 * view the halves scroll instead, and keep their own room (`HALF`).
 */
export const STABLE_SCROLLBAR =
  '[role="main"]:not(:has([data-half-views])):not(:has([data-notepad-docked])) { scrollbar-gutter: stable both-edges; }';

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
  return useContext(InHalfPaneContext) ? (
    <Outlet />
  ) : (
    // The notepad, docked beside the page or a sheet over it, stays open while moving around the app.
    <NotepadProvider>
      <MainLayout />
    </NotepadProvider>
  );
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
  const mainHalf: HalfView = {isHalf: true, side: 'left', onClose: () => close(true)};
  const otherHalf: HalfView = {isHalf: true, side: 'right', onClose: () => close(false)};
  const isNotepadDocked = useNotepadDocked();

  const pages = half ? (
    // Each half scrolls on its own: a long page scrolls in its half, not the whole window, and only up and
    // down (a page that's too wide for its half shrinks or wraps its parts instead). The shell has no padding
    // here, so each page's frame insets it from the window's edges and the divider by the usual 24px, and
    // fits its half exactly (it would otherwise reach out over the shell's padding, and overflow its half).
    <HStack gap={0} height="100%" data-half-views="">
      <StackItem size="fill" isScrollable aria-label="Left half" style={HALF}>
        <HalfViewContext.Provider value={mainHalf}>
          <Outlet />
        </HalfViewContext.Provider>
      </StackItem>
      <Divider orientation="vertical" style={DIVIDER} />
      <StackItem size="fill" isScrollable aria-label="Right half" style={HALF}>
        <HalfViewContext.Provider value={otherHalf}>
          {/* Should it ever be built again, it opens where it had got to, not where it started. */}
          <HalfPane key={half.key} path={halfPath ?? half.path} onPathChange={trackHalf} />
        </HalfViewContext.Provider>
      </StackItem>
    </HStack>
  ) : (
    <Outlet />
  );

  return (
    // Ctrl+S saves the form that's open, in either half.
    <SaveShortcutProvider>
      <HalfViewsContext.Provider value={halfViews}>
        {/* The settings, kept with the account while signed in. */}
        <SettingsSync />
        {/* The keyboard shortcuts (Account Settings, Accessibility), and their list (?). */}
        <KeyboardShortcuts />
        {/* New notifications, as they happen (the bell is in the side nav). */}
        <LiveNotifications />
        {/* The warning before the session times out, and the way back to the login page when it has. */}
        <SessionTimeout />
        <style data-medapp-layout="">{STABLE_SCROLLBAR}</style>
        <AppShell
          sideNav={
            // The side nav is dark in both modes: in light mode it keeps the dark mode's colours.
            <SideNavTheme>
              <AppSideNav
                collapsible={{
                  isCollapsed: isNavCollapsed,
                  onCollapsedChange: setIsNavCollapsed,
                  hasButton: half !== null,
                }}
              />
            </SideNavTheme>
          }
          // In a half view, or beside the docked notepad, each page's frame insets it instead (see below).
          contentPadding={half || isNotepadDocked ? 0 : 6}>
          {/*
            The page always sits in the same two wrappers, whether or not the notepad is docked beside it, so opening
            or closing the notepad never moves the page in the tree: React would otherwise rebuild it from scratch,
            losing anything typed on it. Without the notepad the wrappers take no part in the layout (display:
            contents); docked, they're a row, the page scrolling on its own (as a half does) beside the notepad.
          */}
          <HStack
            gap={0}
            height="100%"
            data-notepad-docked={isNotepadDocked ? '' : undefined}
            style={isNotepadDocked ? undefined : CONTENTS}>
            <StackItem size="fill" isScrollable aria-label="Page" style={!isNotepadDocked ? CONTENTS : half ? FILL : HALF}>
              {pages}
            </StackItem>
            {isNotepadDocked && (
              <>
                <Divider orientation="vertical" />
                <StackItem style={NOTEPAD_COLUMN}>
                  <NotepadPane />
                </StackItem>
              </>
            )}
          </HStack>
        </AppShell>
        {/* On a narrow window, the notepad is a full-screen sheet instead. */}
        <NotepadSheet />
      </HalfViewsContext.Provider>
    </SaveShortcutProvider>
  );
}

/**
 * The side nav's theme: always dark, in its column and its drawer, whatever the app's mode. Except on a phone's top bar
 * (Astryx renders the side nav's heading there), which sits on the page's own background: there it takes the app's
 * mode, or its name and bell would be white on a light bar.
 */
export function SideNavTheme({children}: {children: ReactNode}) {
  if (useSideNavRenderMode() === 'topbar') return <>{children}</>;
  return (
    <Theme theme={themes[defaultThemeSlug]} mode="dark">
      {children}
    </Theme>
  );
}

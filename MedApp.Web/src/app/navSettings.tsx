import {createContext, useCallback, useContext, useMemo, type ReactNode} from 'react';
import {appSettings, defineSetting, useAppSetting} from './settingsStore';

/**
 * How long the pointer rests on a folded side nav icon (a half view) before its row comes out (Account Settings,
 * Accessibility): longer for people who move the pointer slowly, none for those who want it at once.
 */
export const FLYOUT_DELAYS = [
  {value: 'none', label: 'None', time: 'None', ms: 0},
  {value: 'short', label: 'Short', time: '0.25s', ms: 250},
  {value: 'standard', label: 'Standard', time: '0.5s', ms: 500},
  {value: 'long', label: 'Long', time: '1s', ms: 1000},
  {value: 'longer', label: 'Longer', time: '2s', ms: 2000},
] as const;

export type FlyoutDelay = (typeof FLYOUT_DELAYS)[number]['value'];

/** The pages sign-in can open on (Account Settings, Navigation): Home unless chosen otherwise. */
export const START_PAGES = [
  {value: 'home', label: 'Home', path: '/'},
  {value: 'tasks', label: 'My Worklist', path: '/tasks'},
  {value: 'patients', label: 'Patients', path: '/patients'},
  {value: 'procedures', label: 'Pathways', path: '/pathways'},
] as const;

export type StartPage = (typeof START_PAGES)[number]['value'];
type StartPath = (typeof START_PAGES)[number]['path'];

const FLYOUT_DELAY_KEY = defineSetting('medapp.navFlyoutDelay');
const START_PAGE_KEY = defineSetting('medapp.startPage');
const FOLDS_IN_HALF_VIEW_KEY = defineSetting('medapp.navFoldsInHalfView');
const ALWAYS_SHOW_ROW_MENUS_KEY = defineSetting('medapp.navAlwaysShowRowMenus');

interface NavSettingsContextValue {
  flyoutDelay: FlyoutDelay;
  /** The flyout delay in milliseconds. */
  flyoutDelayMs: number;
  setFlyoutDelay: (delay: FlyoutDelay) => void;
  /** Where sign-in opens. */
  startPage: StartPage;
  startPagePath: StartPath;
  setStartPage: (page: StartPage) => void;
  /** Whether opening a half view folds the side nav to its icon rail (on unless turned off). */
  foldsInHalfView: boolean;
  setFoldsInHalfView: (folds: boolean) => void;
  /** Whether each side nav row's menu button always shows, rather than only while the row is hovered (off unless turned on). */
  alwaysShowRowMenus: boolean;
  setAlwaysShowRowMenus: (always: boolean) => void;
}

const NavSettingsContext = createContext<NavSettingsContextValue | null>(null);

const delayMs = (delay: FlyoutDelay) => FLYOUT_DELAYS.find((option) => option.value === delay)!.ms;
const startPath = (page: StartPage) => START_PAGES.find((option) => option.value === page)!.path;

/**
 * Where sign-in opens, from the settings as they are now (outside React): for signing in, once the account's
 * settings are loaded, since signing out clears them from the browser.
 */
export const currentStartPagePath = (): StartPath =>
  START_PAGES.find((option) => option.value === appSettings.get(START_PAGE_KEY))?.path ?? '/';

/**
 * How the side nav behaves (Account Settings), kept in this browser: how long its folded icons wait before bringing
 * their rows out (half a second), the page sign-in opens on (Home), and whether opening a half view folds it to its
 * icons (yes; off, it stays as it was and the two halves share what's left). (A nav item always opens its page in the main
 * view, the left of a half view, and Shift-click opens it in the half view: the owner removed the setting that made it
 * close the half view instead, 18 November 2026. Its old key, `medapp.navClosesHalfView`, is no longer read.)
 */
export function NavSettingsProvider({children}: {children: ReactNode}) {
  const [storedDelay, storeDelay] = useAppSetting(FLYOUT_DELAY_KEY);
  const [storedStart, storeStart] = useAppSetting(START_PAGE_KEY);
  const [storedFolds, storeFolds] = useAppSetting(FOLDS_IN_HALF_VIEW_KEY);
  const [storedAlways, storeAlways] = useAppSetting(ALWAYS_SHOW_ROW_MENUS_KEY);
  const flyoutDelay: FlyoutDelay = FLYOUT_DELAYS.find((option) => option.value === storedDelay)?.value ?? 'standard';
  const startPage: StartPage = START_PAGES.find((option) => option.value === storedStart)?.value ?? 'home';
  const foldsInHalfView = storedFolds !== 'false';
  const alwaysShowRowMenus = storedAlways === 'true';

  const setFlyoutDelay = useCallback((delay: FlyoutDelay) => storeDelay(delay), [storeDelay]);
  const setStartPage = useCallback((page: StartPage) => storeStart(page), [storeStart]);
  const setFoldsInHalfView = useCallback((folds: boolean) => storeFolds(String(folds)), [storeFolds]);
  const setAlwaysShowRowMenus = useCallback((always: boolean) => storeAlways(String(always)), [storeAlways]);

  const value = useMemo(
    () => ({
      flyoutDelay,
      flyoutDelayMs: delayMs(flyoutDelay),
      setFlyoutDelay,
      startPage,
      startPagePath: startPath(startPage),
      setStartPage,
      foldsInHalfView,
      setFoldsInHalfView,
      alwaysShowRowMenus,
      setAlwaysShowRowMenus,
    }),
    [flyoutDelay, setFlyoutDelay, startPage, setStartPage, foldsInHalfView, setFoldsInHalfView, alwaysShowRowMenus, setAlwaysShowRowMenus],
  );
  return <NavSettingsContext.Provider value={value}>{children}</NavSettingsContext.Provider>;
}

const DEFAULTS: NavSettingsContextValue = {
  flyoutDelay: 'standard',
  flyoutDelayMs: delayMs('standard'),
  setFlyoutDelay: () => {},
  startPage: 'home',
  startPagePath: '/',
  setStartPage: () => {},
  foldsInHalfView: true,
  setFoldsInHalfView: () => {},
  alwaysShowRowMenus: false,
  setAlwaysShowRowMenus: () => {},
};

/** The side nav settings; outside the provider (tests of one part), the defaults. */
export function useNavSettings(): NavSettingsContextValue {
  return useContext(NavSettingsContext) ?? DEFAULTS;
}

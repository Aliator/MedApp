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

const CLOSES_HALF_VIEW_KEY = defineSetting('medapp.navClosesHalfView');
const FLYOUT_DELAY_KEY = defineSetting('medapp.navFlyoutDelay');
const START_PAGE_KEY = defineSetting('medapp.startPage');

interface NavSettingsContextValue {
  /**
   * What clicking a side nav item does while a half view is open: true closes the half view and shows the page on
   * its own; false opens it on the left (the main view), keeping the half view.
   */
  closesHalfView: boolean;
  setClosesHalfView: (closes: boolean) => void;
  flyoutDelay: FlyoutDelay;
  /** The flyout delay in milliseconds. */
  flyoutDelayMs: number;
  setFlyoutDelay: (delay: FlyoutDelay) => void;
  /** Where sign-in opens. */
  startPage: StartPage;
  startPagePath: StartPath;
  setStartPage: (page: StartPage) => void;
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
 * How the side nav behaves (Account Settings), kept in this browser: whether its clicks close an open half view
 * (by default they do), how long its folded icons wait before bringing their rows out (half a second), and the page
 * sign-in opens on (Home).
 */
export function NavSettingsProvider({children}: {children: ReactNode}) {
  const [storedCloses, storeCloses] = useAppSetting(CLOSES_HALF_VIEW_KEY);
  const [storedDelay, storeDelay] = useAppSetting(FLYOUT_DELAY_KEY);
  const [storedStart, storeStart] = useAppSetting(START_PAGE_KEY);
  // Off unless chosen (the owner's default, 18 November 2026): a nav item opens on the left, keeping the half view.
  const closesHalfView = storedCloses === 'true';
  const flyoutDelay: FlyoutDelay = FLYOUT_DELAYS.find((option) => option.value === storedDelay)?.value ?? 'standard';
  const startPage: StartPage = START_PAGES.find((option) => option.value === storedStart)?.value ?? 'home';

  const setClosesHalfView = useCallback((closes: boolean) => storeCloses(String(closes)), [storeCloses]);
  const setFlyoutDelay = useCallback((delay: FlyoutDelay) => storeDelay(delay), [storeDelay]);
  const setStartPage = useCallback((page: StartPage) => storeStart(page), [storeStart]);

  const value = useMemo(
    () => ({
      closesHalfView,
      setClosesHalfView,
      flyoutDelay,
      flyoutDelayMs: delayMs(flyoutDelay),
      setFlyoutDelay,
      startPage,
      startPagePath: startPath(startPage),
      setStartPage,
    }),
    [closesHalfView, setClosesHalfView, flyoutDelay, setFlyoutDelay, startPage, setStartPage],
  );
  return <NavSettingsContext.Provider value={value}>{children}</NavSettingsContext.Provider>;
}

const DEFAULTS: NavSettingsContextValue = {
  closesHalfView: false,
  setClosesHalfView: () => {},
  flyoutDelay: 'standard',
  flyoutDelayMs: delayMs('standard'),
  setFlyoutDelay: () => {},
  startPage: 'home',
  startPagePath: '/',
  setStartPage: () => {},
};

/** The side nav settings; outside the provider (tests of one part), the defaults. */
export function useNavSettings(): NavSettingsContextValue {
  return useContext(NavSettingsContext) ?? DEFAULTS;
}

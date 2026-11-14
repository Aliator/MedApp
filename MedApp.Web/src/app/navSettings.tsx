import {createContext, useCallback, useContext, useMemo, useState, type ReactNode} from 'react';

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

const CLOSES_HALF_VIEW_KEY = 'medapp.navClosesHalfView';
const FLYOUT_DELAY_KEY = 'medapp.navFlyoutDelay';

function read<T>(key: string, parse: (stored: string | null) => T, fallback: T): T {
  try {
    return parse(localStorage.getItem(key));
  } catch {
    return fallback;
  }
}

function write(key: string, value: string) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage blocked: the choice lasts until the page reloads.
  }
}

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
}

const NavSettingsContext = createContext<NavSettingsContextValue | null>(null);

const delayMs = (delay: FlyoutDelay) => FLYOUT_DELAYS.find((option) => option.value === delay)!.ms;

/**
 * How the side nav behaves (Account Settings), kept in this browser: whether its clicks close an open half view
 * (by default they do), and how long its folded icons wait before bringing their rows out (half a second).
 */
export function NavSettingsProvider({children}: {children: ReactNode}) {
  const [closesHalfView, setCloses] = useState(() => read(CLOSES_HALF_VIEW_KEY, (s) => s !== 'false', true));
  const [flyoutDelay, setDelay] = useState<FlyoutDelay>(() =>
    read(FLYOUT_DELAY_KEY, (s) => FLYOUT_DELAYS.find((option) => option.value === s)?.value ?? 'standard', 'standard'),
  );

  const setClosesHalfView = useCallback((closes: boolean) => {
    setCloses(closes);
    write(CLOSES_HALF_VIEW_KEY, String(closes));
  }, []);
  const setFlyoutDelay = useCallback((delay: FlyoutDelay) => {
    setDelay(delay);
    write(FLYOUT_DELAY_KEY, delay);
  }, []);

  const value = useMemo(
    () => ({closesHalfView, setClosesHalfView, flyoutDelay, flyoutDelayMs: delayMs(flyoutDelay), setFlyoutDelay}),
    [closesHalfView, setClosesHalfView, flyoutDelay, setFlyoutDelay],
  );
  return <NavSettingsContext.Provider value={value}>{children}</NavSettingsContext.Provider>;
}

const DEFAULTS: NavSettingsContextValue = {
  closesHalfView: true,
  setClosesHalfView: () => {},
  flyoutDelay: 'standard',
  flyoutDelayMs: delayMs('standard'),
  setFlyoutDelay: () => {},
};

/** The side nav settings; outside the provider (tests of one part), the defaults. */
export function useNavSettings(): NavSettingsContextValue {
  return useContext(NavSettingsContext) ?? DEFAULTS;
}

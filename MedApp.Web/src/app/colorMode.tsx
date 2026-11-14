import {createContext, useCallback, useContext, useMemo, useState, type ReactNode} from 'react';

export type ColorMode = 'light' | 'dark';

const STORAGE_KEY = 'medapp.colorMode';

/** The remembered choice, or dark when there is none (plan Q11: dark by default, not following the OS). */
function readStoredMode(): ColorMode {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}

interface ColorModeContextValue {
  mode: ColorMode;
  toggle: () => void;
}

const ColorModeContext = createContext<ColorModeContextValue | null>(null);

export function ColorModeProvider({children}: {children: (mode: ColorMode) => ReactNode}) {
  const [mode, setMode] = useState<ColorMode>(readStoredMode);

  const toggle = useCallback(() => {
    setMode((current) => {
      const next = current === 'dark' ? 'light' : 'dark';
      try {
        localStorage.setItem(STORAGE_KEY, next);
      } catch {
        // Storage blocked (e.g. private mode): the choice lasts until the page reloads.
      }
      return next;
    });
  }, []);

  const value = useMemo(() => ({mode, toggle}), [mode, toggle]);
  return <ColorModeContext.Provider value={value}>{children(mode)}</ColorModeContext.Provider>;
}

export function useColorMode(): ColorModeContextValue {
  const value = useContext(ColorModeContext);
  if (!value) throw new Error('useColorMode must be used inside ColorModeProvider.');
  return value;
}

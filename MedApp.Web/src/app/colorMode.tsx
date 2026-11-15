import {createContext, useCallback, useContext, useMemo, type ReactNode} from 'react';
import {defineSetting, useAppSetting} from './settingsStore';

export type ColorMode = 'light' | 'dark';

const STORAGE_KEY = defineSetting('medapp.colorMode');

interface ColorModeContextValue {
  mode: ColorMode;
  setMode: (mode: ColorMode) => void;
  toggle: () => void;
}

const ColorModeContext = createContext<ColorModeContextValue | null>(null);

/** Light or dark mode: the remembered choice, or dark when there is none (plan Q11: dark by default, not following the OS). */
export function ColorModeProvider({children}: {children: (mode: ColorMode) => ReactNode}) {
  const [stored, store] = useAppSetting(STORAGE_KEY);
  const mode: ColorMode = stored === 'light' ? 'light' : 'dark';

  const choose = useCallback((next: ColorMode) => store(next), [store]);
  const toggle = useCallback(() => choose(mode === 'dark' ? 'light' : 'dark'), [choose, mode]);

  const value = useMemo(() => ({mode, setMode: choose, toggle}), [mode, choose, toggle]);
  return <ColorModeContext.Provider value={value}>{children(mode)}</ColorModeContext.Provider>;
}

export function useColorMode(): ColorModeContextValue {
  const value = useContext(ColorModeContext);
  if (!value) throw new Error('useColorMode must be used inside ColorModeProvider.');
  return value;
}

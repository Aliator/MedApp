import {createContext, useCallback, useContext, useLayoutEffect, useMemo, useState, type ReactNode} from 'react';

/** How big the app's text is. Astryx sizes text and spacing in rem, so this scales the page's root font size. */
export const TEXT_SIZES = [
  {value: 'small', label: 'Small', scale: '87.5%'},
  {value: 'standard', label: 'Standard', scale: '100%'},
  {value: 'large', label: 'Large', scale: '112.5%'},
  {value: 'larger', label: 'Larger', scale: '125%'},
] as const;

export type TextSize = (typeof TEXT_SIZES)[number]['value'];

const STORAGE_KEY = 'medapp.textSize';

function readStored(): TextSize {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return TEXT_SIZES.find((size) => size.value === stored)?.value ?? 'standard';
  } catch {
    return 'standard';
  }
}

interface TextSizeContextValue {
  textSize: TextSize;
  setTextSize: (size: TextSize) => void;
}

const TextSizeContext = createContext<TextSizeContextValue | null>(null);

/**
 * The text size people chose (Account Settings), kept in this browser. Standard leaves the browser's own size
 * alone; the others scale it, so a reader's browser setting still counts.
 */
export function TextSizeProvider({children}: {children: ReactNode}) {
  const [textSize, setSize] = useState<TextSize>(readStored);

  useLayoutEffect(() => {
    const root = document.documentElement;
    const scale = TEXT_SIZES.find((size) => size.value === textSize)!.scale;
    root.style.fontSize = textSize === 'standard' ? '' : scale;
    return () => {
      root.style.fontSize = '';
    };
  }, [textSize]);

  const setTextSize = useCallback((size: TextSize) => {
    setSize(size);
    try {
      localStorage.setItem(STORAGE_KEY, size);
    } catch {
      // Storage blocked: the size lasts until the page reloads.
    }
  }, []);

  const value = useMemo(() => ({textSize, setTextSize}), [textSize, setTextSize]);
  return <TextSizeContext.Provider value={value}>{children}</TextSizeContext.Provider>;
}

export function useTextSize(): TextSizeContextValue {
  const value = useContext(TextSizeContext);
  if (!value) throw new Error('useTextSize must be used inside TextSizeProvider.');
  return value;
}

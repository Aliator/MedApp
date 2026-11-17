import {useCallback, useState} from 'react';

/**
 * Whether an element is narrower than a width in rem (so it grows with the text size, as rem-sized content does),
 * kept up to date as it resizes. Attach the returned ref to the element. Use it where a component's own width, not the
 * window's, decides its layout: the Calendar page's week goes compact only when the card it's in can't fit it.
 *
 * False until it's measured (on the server, and in the browser's first render, so that matches the server's), and
 * while the element has no width (not laid out, or hidden).
 */
export function useNarrowerThan(rem: number): [(element: HTMLElement | null) => (() => void) | undefined, boolean] {
  const [isNarrower, setIsNarrower] = useState(false);
  const ref = useCallback(
    (element: HTMLElement | null) => {
      if (!element || typeof ResizeObserver === 'undefined') return undefined;
      const observer = new ResizeObserver((entries) => {
        const width = entries[entries.length - 1]?.contentRect.width ?? 0;
        if (width <= 0) return;
        const remPx = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
        setIsNarrower(width < rem * remPx);
      });
      observer.observe(element);
      return () => observer.disconnect();
    },
    [rem],
  );
  return [ref, isNarrower];
}

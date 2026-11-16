import {useCallback, useState} from 'react';

/**
 * Whether an element's content is too big for it (so it scrolls), kept up to date as the element or its content
 * changes size. Attach the returned ref to the scrolling element. It watches the element and its direct children, so
 * content swapped inside it (a chart for a table) is noticed too.
 *
 * False until it's measured: on the server, and in the first render in the browser, so that matches the server's.
 */
export function useOverflows(): [(element: HTMLElement | null) => (() => void) | undefined, boolean] {
  const [overflows, setOverflows] = useState(false);
  const ref = useCallback((element: HTMLElement | null) => {
    if (!element || typeof ResizeObserver === 'undefined') return undefined;
    const measure = () =>
      setOverflows(element.scrollHeight > element.clientHeight + 1 || element.scrollWidth > element.clientWidth + 1);
    const resizes = new ResizeObserver(measure);
    const watch = () => {
      resizes.disconnect();
      resizes.observe(element);
      for (const child of element.children) resizes.observe(child);
      measure();
    };
    // New children (content swapped in) are watched as well.
    const mutations = typeof MutationObserver === 'undefined' ? undefined : new MutationObserver(watch);
    mutations?.observe(element, {childList: true});
    watch();
    return () => {
      resizes.disconnect();
      mutations?.disconnect();
    };
  }, []);
  return [ref, overflows];
}

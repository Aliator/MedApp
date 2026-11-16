/**
 * jsdom has no layout, and no ResizeObserver: this one reports every element it observes as `width` pixels wide,
 * at once, so components that size themselves to their container (charts) draw in tests.
 */
export function givenWidth(width = 600) {
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(private readonly callback: ResizeObserverCallback) {}
      observe(target: Element) {
        this.callback([{target, contentRect: {width} as DOMRectReadOnly} as ResizeObserverEntry], this as unknown as ResizeObserver);
      }
      unobserve() {}
      disconnect() {}
    },
  );
}

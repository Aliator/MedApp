import {TextArea, type TextAreaProps} from '@astryxdesign/core/TextArea';
import {useLayoutEffect, useRef, useState} from 'react';

export interface FillingTextAreaProps extends Omit<TextAreaProps, 'rows' | 'ref'> {
  /** The fewest rows it shrinks to when there's no room; below that, its container scrolls. */
  minRows?: number;
}

/** The nearest ancestor that scrolls vertically: the space the text area has to fit in. */
function scrollingParent(element: HTMLElement): HTMLElement | undefined {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const {overflowY} = getComputedStyle(parent);
    if (overflowY === 'auto' || overflowY === 'scroll') return parent;
  }
  return undefined;
}

/** How tall the scroller's content is, whether or not it overflows. */
function contentHeight(scroller: HTMLElement) {
  const style = getComputedStyle(scroller);
  let height = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
  for (const child of scroller.children) height += (child as HTMLElement).offsetHeight;
  return height;
}

/**
 * A TextArea that grows to fill the free space in the area that scrolls around it (e.g. a `FoldingCard` section
 * or a `PanelCard` body), so the box is as big as it can be without making that area scroll. It never goes below
 * `minRows`; if even that doesn't fit, the area scrolls as usual. It resizes as the space changes.
 *
 * Use it where a box is the main thing to fill in on a card, and one per scrolling area: two would compete for the
 * same space.
 */
export function FillingTextArea({minRows = 2, ...props}: FillingTextAreaProps) {
  const ref = useRef<HTMLTextAreaElement>(null);
  const [rows, setRows] = useState(minRows);

  useLayoutEffect(() => {
    const textArea = ref.current;
    const scroller = textArea && scrollingParent(textArea);
    if (!textArea || !scroller) return;

    const fit = () => {
      const lineHeight = parseFloat(getComputedStyle(textArea).lineHeight);
      // Nothing laid out (e.g. hidden, or in tests): keep the rows it has.
      if (!lineHeight || !scroller.clientHeight) return;
      const spare = scroller.clientHeight - contentHeight(scroller);
      // Spare space (or the overflow, when negative) in whole rows, from the rows the box has now.
      const fitted = Math.max(minRows, textArea.rows + Math.floor(spare / lineHeight));
      if (fitted !== textArea.rows) setRows(fitted);
    };

    fit();
    if (typeof ResizeObserver === 'undefined') return;
    // Refit when the area changes size (the window, or sections opening and folding) or its content does.
    const observer = new ResizeObserver(fit);
    observer.observe(scroller);
    for (const child of scroller.children) observer.observe(child);
    return () => observer.disconnect();
  }, [minRows]);

  return <TextArea {...props} ref={ref} rows={rows} />;
}

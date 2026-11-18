import {MultiSelector, type MultiSelectorOptionType, type MultiSelectorProps} from '@astryxdesign/core/MultiSelector';
import {Selector, type SelectorOptionType, type SelectorProps} from '@astryxdesign/core/Selector';
import {useCallback, type Ref} from 'react';

/**
 * The element that scrolls the list: the listbox, or the first ancestor that scrolls up to its popover (or sheet).
 * Never anything outside it, such as the dialog or page behind.
 */
function scrollerOf(listbox: HTMLElement): HTMLElement {
  const surface = listbox.closest('[popover], dialog');
  for (let element: HTMLElement | null = listbox; element; element = element.parentElement) {
    if (element.scrollHeight > element.clientHeight) return element;
    if (element === surface || !surface) break;
  }
  return listbox;
}

/**
 * Scrolls an open list so its selected option (or, with nothing selected, its first) sits in the middle of the list,
 * as far as the list can scroll: the same place on every open, whatever it was scrolled to when it last closed.
 */
export function scrollToSelectedOption(listbox: HTMLElement) {
  const option = listbox.querySelector<HTMLElement>('[role="option"][aria-selected="true"]') ?? listbox.querySelector<HTMLElement>('[role="option"]');
  const scroller = scrollerOf(listbox);
  if (!option) {
    scroller.scrollTop = 0;
    return;
  }
  const optionTop = option.getBoundingClientRect().top - scroller.getBoundingClientRect().top + scroller.scrollTop;
  scroller.scrollTop = Math.max(0, optionTop - (scroller.clientHeight - option.offsetHeight) / 2);
}

/**
 * Astryx's `Selector`, opening in a fixed place: its list always opens below its field (above only when it can't fit
 * below), scrolled to the selected option.
 *
 * Astryx's own default lays the list over the field so the selected option sits on it, measured from where the list
 * was last scrolled. A list scrolled and closed then reopens somewhere else (high above the field, below it, or over
 * it), and still scrolled where it was left. Use this wherever a form would use `Selector`; ESLint refuses the plain one.
 */
export function AnchoredSelector<T extends SelectorOptionType>(props: SelectorProps<T>) {
  const {ref, ...rest} = props;
  return <Selector placement="below" {...rest} ref={useScrollToSelectedOnOpen(ref)} />;
}

/**
 * Astryx's `MultiSelector`, opening scrolled to its first selected option (or its first option) every time, not
 * wherever its list was left. Its list already opens below its field. ESLint refuses the plain one.
 */
export function AnchoredMultiSelector<T extends MultiSelectorOptionType>(props: MultiSelectorProps<T>) {
  const {ref, ...rest} = props;
  return <MultiSelector {...rest} ref={useScrollToSelectedOnOpen(ref)} />;
}

/**
 * A ref for a picker's trigger that watches it while it's mounted (a React 19 ref returns its clean-up), scrolling
 * its list to the selected option on each open, and passes it on to the caller's ref, if any.
 */
function useScrollToSelectedOnOpen<E extends HTMLElement>(callerRef: Ref<E> | undefined) {
  return useCallback(
    (trigger: E | null) => {
      assignRef(callerRef, trigger);
      if (!trigger) return;
      const stopWatching = scrollToSelectedOnOpen(trigger);
      return () => {
        stopWatching();
        assignRef(callerRef, null);
      };
    },
    [callerRef],
  );
}

/**
 * On each open (the trigger's `aria-expanded` turning true), scrolls its list to the selected option: straight after
 * Astryx's own open, which only scrolls the option into view when it isn't already, and once more before the next
 * paint, in case the list wasn't shown yet. Returns the clean-up.
 */
function scrollToSelectedOnOpen(trigger: HTMLElement) {
  let frame = 0;
  const scroll = () => {
    const listboxId = trigger.getAttribute('aria-controls');
    const listbox = listboxId ? document.getElementById(listboxId) : null;
    if (listbox) scrollToSelectedOption(listbox);
  };
  const observer = new MutationObserver(() => {
    if (trigger.getAttribute('aria-expanded') !== 'true') return;
    scroll();
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(scroll);
  });
  observer.observe(trigger, {attributes: true, attributeFilter: ['aria-expanded']});
  return () => {
    observer.disconnect();
    cancelAnimationFrame(frame);
  };
}

function assignRef<T>(ref: Ref<T> | undefined, value: T | null) {
  if (typeof ref === 'function') ref(value);
  else if (ref) ref.current = value;
}

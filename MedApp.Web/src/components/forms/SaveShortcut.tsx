import {useHotkeys} from '@astryxdesign/core/hooks';
import {createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode, type RefObject} from 'react';

/** The save shortcut, as `Kbd` shows it: Ctrl+S, or ⌘S on a Mac. */
export const SAVE_KEYS = 'mod+s';

/** Something Ctrl+S can save: a form's element (where focus counts as "in it"), and how to save it. */
export interface Saver {
  element: () => HTMLElement | null;
  save: () => void;
}

/** Dialogs, open or closed (Astryx keeps a closed one in the page, without `open`). */
const DIALOG = 'dialog, [role="dialog"], [role="alertdialog"]';

/**
 * Which form Ctrl+S saves, or null for none:
 * - the one that has the focus (the innermost, if one is inside another);
 * - with the focus in a dialog that isn't a form, or a dialog open while the focus is elsewhere, none: a dialog is
 *   its own task, and saving the page behind it would be a surprise;
 * - otherwise (the focus on a button or the page itself), the page's form, if it has only one. With two (a half
 *   view), it can't tell which was meant, so none.
 */
export function pickSaver(savers: readonly Saver[], focused: Element | null, document: Document): Saver | null {
  const holding = savers.filter((s) => {
    const element = s.element();
    return element !== null && focused !== null && element.contains(focused);
  });
  if (holding.length > 0) {
    return holding.find((s) => !holding.some((other) => other !== s && s.element()!.contains(other.element()))) ?? holding[0];
  }
  if (focused?.closest(DIALOG) || document.querySelector('dialog[open]')) return null;
  const onPage = savers.filter((s) => {
    const element = s.element();
    return element !== null && element.closest(DIALOG) === null;
  });
  return onPage.length === 1 ? onPage[0] : null;
}

interface SaveShortcutContextValue {
  /** Adds a form Ctrl+S can save; the function returned takes it away again. */
  register: (saver: Saver) => () => void;
}

const SaveShortcutContext = createContext<SaveShortcutContextValue | null>(null);

/**
 * The save shortcut, Ctrl+S (⌘S on a Mac), for the signed-in app: it saves the form that's open (see `pickSaver`),
 * as its Save button would. Forms join with `useSaveShortcut`; FormCard does so itself.
 *
 * Unlike the single-key shortcuts (`KeyboardShortcuts`), it's always on, and works while typing in a field: it has
 * a modifier, so it can't be pressed by accident while typing (WCAG 2.1.4 is about single keys), and in a field is
 * where it's wanted. While any form is open it always stops the browser's own Save page dialog, even when it saves
 * nothing (a dialog is open, or it can't tell which of two forms was meant); with no form open, the browser has it.
 */
export function SaveShortcutProvider({children}: {children: ReactNode}) {
  const savers = useRef<Saver[]>([]);
  const [count, setCount] = useState(0);

  const register = useCallback((saver: Saver) => {
    savers.current = [...savers.current, saver];
    setCount((n) => n + 1);
    return () => {
      savers.current = savers.current.filter((s) => s !== saver);
      setCount((n) => n - 1);
    };
  }, []);

  useHotkeys([
    {
      keys: SAVE_KEYS,
      allowInInputs: true,
      isDisabled: count === 0,
      onPress: () => pickSaver(savers.current, document.activeElement, document)?.save(),
    },
  ]);

  const value = useMemo(() => ({register}), [register]);
  return <SaveShortcutContext.Provider value={value}>{children}</SaveShortcutContext.Provider>;
}

/**
 * Lets Ctrl+S save this form: `ref` is its element, and `save` does what its Save button does (undefined when it
 * has none, or can't save now). Outside `SaveShortcutProvider` (tests of one part), it does nothing.
 */
export function useSaveShortcut(ref: RefObject<HTMLElement | null>, save: (() => void) | undefined) {
  const context = useContext(SaveShortcutContext);
  const latest = useRef(save);
  useEffect(() => {
    latest.current = save;
  });

  const canSave = save !== undefined;
  useEffect(() => {
    if (!context || !canSave) return;
    return context.register({element: () => ref.current, save: () => latest.current?.()});
  }, [context, canSave, ref]);
}

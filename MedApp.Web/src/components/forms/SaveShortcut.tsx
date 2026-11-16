import {useToast} from '@astryxdesign/core/Toast';
import {createContext, useCallback, useContext, useEffect, useMemo, useRef, type ReactNode, type RefObject} from 'react';

/** The save shortcut, as `Kbd` shows it: Ctrl+S, or ⌘S on a Mac. */
export const SAVE_KEYS = 'mod+s';

/**
 * Whether a key press is the save shortcut: S with Ctrl or ⌘ (either, on any platform, so it never matters which
 * the browser thinks it's on), and no Alt or Shift (Ctrl+Shift+S is other things, e.g. Firefox's screenshots).
 */
export function isSaveShortcut(event: KeyboardEvent): boolean {
  return (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 's' && !event.isComposing;
}

/**
 * Something Ctrl+S can save: a form's element (where focus counts as "in it"), and how to save it. `save` returns
 * false when there was nothing it could save (no changes, or its button can't be pressed yet).
 */
export interface Saver {
  element: () => HTMLElement | null;
  save: () => boolean | void;
}

/** Said, on screen and to screen readers, when Ctrl+S was taken from the browser but saved nothing. */
export const NOTHING_TO_SAVE = 'Nothing to save.';
export const NO_FORM_CHOSEN = 'Nothing saved: click in the form you want to save, then try again.';

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
 * When it saves nothing, a brief message (an Astryx toast, which screen readers hear too) says so, so nobody takes
 * the key press for a save.
 */
export function SaveShortcutProvider({children}: {children: ReactNode}) {
  const savers = useRef<Saver[]>([]);
  const toast = useToast();
  const toastRef = useRef(toast);
  useEffect(() => {
    toastRef.current = toast;
  });

  const register = useCallback((saver: Saver) => {
    savers.current = [...savers.current, saver];
    return () => {
      savers.current = savers.current.filter((s) => s !== saver);
    };
  }, []);

  // One listener for every form, which looks at the forms open as the key is pressed.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || !isSaveShortcut(event) || savers.current.length === 0) return;
      event.preventDefault();
      const saver = pickSaver(savers.current, document.activeElement, document);
      const saved = saver ? saver.save() !== false : false;
      if (!saved) toastRef.current({body: saver ? NOTHING_TO_SAVE : NO_FORM_CHOSEN, isAutoHide: true});
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  const value = useMemo(() => ({register}), [register]);
  return <SaveShortcutContext.Provider value={value}>{children}</SaveShortcutContext.Provider>;
}

/**
 * Lets Ctrl+S save this form: `ref` is its element, and `save` does what its Save button does (undefined when it
 * has none), returning false when there's nothing it can save now. Outside `SaveShortcutProvider` (tests of one
 * part), it does nothing.
 */
export function useSaveShortcut(ref: RefObject<HTMLElement | null>, save: (() => boolean | void) | undefined) {
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

import {useMediaQuery} from '@astryxdesign/core/hooks';
import {createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode} from 'react';
import type {Procedure} from '../../api/procedures';
import {useHalfView} from '../pages/halfView';

/** The notepad's page, which a half view shows it in (the side nav's Notepad row). */
export const NOTEPAD_PATH = '/notepad';

/**
 * Whether the notepad is open is kept in this browser (it's a convenience, like the half view), under a `medapp.` key,
 * so signing out clears it with the rest. The notepad's text never is: that's on the server.
 */
export const NOTEPAD_OPEN_KEY = 'medapp.notepadOpen';

function storedOpen(): boolean {
  try {
    return localStorage.getItem(NOTEPAD_OPEN_KEY) === 'true';
  } catch {
    return false;
  }
}

/** Keeps whether the notepad is open in this browser, for the signed-in layout to read as it mounts. */
export function storeNotepadOpen(isOpen: boolean) {
  try {
    if (isOpen) localStorage.setItem(NOTEPAD_OPEN_KEY, 'true');
    else localStorage.removeItem(NOTEPAD_OPEN_KEY);
  } catch {
    // Without storage, the notepad just starts closed next time.
  }
}

/** A procedure page that's open, which the notepad can add a note to: where it is, and the procedure as it shows it. */
export interface OpenProcedure {
  key: string;
  /** In a half view, which half; undefined for a page on its own. */
  side?: 'left' | 'right';
  procedure: Procedure;
}

/**
 * Below this width the notepad opens as a full-screen sheet over the page; from it, docked on the right of every page
 * (a phone, a small tablet, or a window too narrow for a page and the notepad side by side).
 */
export const NOTEPAD_SHEET_QUERY = '(max-width: 1023px)';

/** How long the sheet takes to close (its animation), after which the focus is checked again. */
const FOCUS_AFTER_SHEET_MS = 600;

/** The docked notepad's width. */
export const NOTEPAD_WIDTH = '24rem';

/**
 * Pages beside the docked notepad take their narrow layout only when the room left is narrow: the window less the
 * notepad's 24rem (384px) under the narrow window's 1,400px (`NARROW_WINDOW_QUERY`). So a 1,920px window keeps wide
 * layouts beside it.
 */
export const NARROW_BESIDE_NOTEPAD_QUERY = '(max-width: 1783px)';

export interface NotepadState {
  isOpen: boolean;
  open: () => void;
  /** Closes it at once, without saving: the pane's own closer (`requestClose`) saves first. */
  close: () => void;
  /** Closes it after saving any change (the pane's closer, when it's showing the notepad); it stays open if that fails. */
  requestClose: () => void;
  /** The pane's way of closing (save, then close), until the function returned is called. */
  registerCloser: (closer: () => Promise<void>) => () => void;
  /** The procedure pages open now (the main page's, and a half view's), left before right. */
  openProcedures: OpenProcedure[];
  /** Offers a procedure page to the notepad, until the function returned is called. */
  offer: (open: OpenProcedure) => () => void;
  /**
   * Whether the notepad was just opened by its user (not reopened as a page loads), so the pane should take the focus:
   * true once, for the pane that asks first.
   */
  takeFocus: () => boolean;
}

const NotepadContext = createContext<NotepadState | null>(null);

/** The notepad, or null outside the signed-in layout (and in tests of one part). */
export const useNotepad = () => useContext(NotepadContext);

export interface NotepadProviderProps {
  children: ReactNode;
  /**
   * The notepad shown in a half view (`/notepad` in the half view's router), which closing (once saved) closes: it's
   * always open, takes the focus as it starts, keeps nothing in this browser, and Add Note to Procedure offers the
   * procedure pages of the signed-in layout's notepad, which it's inside.
   */
  onCloseHalf?: () => void;
}

/**
 * The notepad's state for the signed-in layout: whether it's open (remembered in this browser, read after mounting so
 * the server's render matches), and the procedure pages open, which its Add Note to Procedure can add to. Or, with
 * `onCloseHalf`, for the notepad shown in a half view.
 */
export function NotepadProvider({children, onCloseHalf}: NotepadProviderProps) {
  const isHalf = onCloseHalf !== undefined;
  // In a half view: the signed-in layout's notepad, whose procedure pages are the ones open beside this.
  const layout = useNotepad();
  const [isOpen, setIsOpen] = useState(isHalf);
  const [offers, setOffers] = useState<OpenProcedure[]>([]);
  const closer = useRef<(() => Promise<void>) | null>(null);
  // Opened by its user: the pane takes the focus, and closing it gives the focus back to what opened it. In a half
  // view, it was opened to write in, so it takes the focus as it starts.
  const shouldFocus = useRef(isHalf);
  const opener = useRef<HTMLElement | null>(null);
  // The latest way to close the half (the half view makes a new one each render), so `close` stays the same.
  const closeHalf = useRef(onCloseHalf);
  useEffect(() => {
    closeHalf.current = onCloseHalf;
  }, [onCloseHalf]);

  useEffect(() => {
    // Read from this browser once mounted: the server can't know it, and its render must match the first one here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!isHalf && storedOpen()) setIsOpen(true);
  }, [isHalf]);

  const open = useCallback(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    shouldFocus.current = true;
    setIsOpen(true);
    if (!isHalf) storeNotepadOpen(true);
  }, [isHalf]);
  const close = useCallback(() => {
    // In a half view: closing it closes its half.
    if (isHalf) {
      closeHalf.current?.();
      return;
    }
    setIsOpen(false);
    storeNotepadOpen(false);
    shouldFocus.current = false;
    // Focus that was in the pane is lost as it goes: give it back to what opened it, or the side nav's Notepad, if
    // either is still shown; otherwise (a phone, whose nav drawer has closed) to the page's title. Again once the
    // sheet's closing animation is over, as its own focus return can't find the nav drawer's item either.
    const restore = () => {
      const active = document.activeElement;
      // The notepad in a half view (the right half) is another notepad, whose box keeps the focus it took as it opened.
      const isInThisPane = !!active?.closest('[role="region"][aria-label="Notepad"]') && !active?.closest('[aria-label="Right half"]');
      const isLost = !active || active === document.body || !active.isConnected || isInThisPane;
      if (!isLost) return;
      const title = document.querySelector<HTMLElement>('main h1, [role="main"] h1');
      // The first that takes the focus (one that isn't shown can't).
      for (const place of [opener.current, document.querySelector<HTMLElement>('[data-notepad-toggle]'), title]) {
        if (!place?.isConnected) continue;
        // A title takes the focus only from code, and stays out of the tab order.
        if (place === title && !title.hasAttribute('tabindex')) title.setAttribute('tabindex', '-1');
        place.focus();
        if (document.activeElement === place) return;
      }
    };
    setTimeout(restore);
    setTimeout(restore, FOCUS_AFTER_SHEET_MS);
  }, [isHalf]);
  const takeFocus = useCallback(() => {
    const should = shouldFocus.current;
    shouldFocus.current = false;
    return should;
  }, []);
  const requestClose = useCallback(() => {
    if (closer.current) void closer.current();
    else close();
  }, [close]);
  const registerCloser = useCallback((registered: () => Promise<void>) => {
    closer.current = registered;
    return () => {
      if (closer.current === registered) closer.current = null;
    };
  }, []);
  const offer = useCallback((offered: OpenProcedure) => {
    setOffers((current) => [...current.filter((o) => o.key !== offered.key), offered]);
    return () => setOffers((current) => current.filter((o) => o.key !== offered.key));
  }, []);

  const ownProcedures = useMemo(
    () => [...offers].sort((a, b) => (a.side === 'right' ? 1 : 0) - (b.side === 'right' ? 1 : 0)),
    [offers],
  );
  // In a half view, the procedure pages beside it: the signed-in layout's.
  const layoutProcedures = layout?.openProcedures;
  const openProcedures = useMemo(() => (isHalf ? (layoutProcedures ?? []) : ownProcedures), [isHalf, layoutProcedures, ownProcedures]);
  const value = useMemo(
    () => ({isOpen, open, close, requestClose, registerCloser, openProcedures, offer, takeFocus}),
    [isOpen, open, close, requestClose, registerCloser, openProcedures, offer, takeFocus],
  );
  return <NotepadContext.Provider value={value}>{children}</NotepadContext.Provider>;
}

/**
 * Offers this procedure page to the notepad while it's open, so Add Note to Procedure can add to it (and asks which,
 * when a half view has two). Pass the procedure as the page shows it; nothing while it loads.
 */
export function useOfferToNotepad(procedure: Procedure | undefined) {
  const notepad = useNotepad();
  const offer = notepad?.offer;
  const {side} = useHalfView();
  const key = useId();

  useEffect(() => {
    if (!offer || !procedure) return;
    return offer({key, side, procedure});
  }, [offer, key, side, procedure]);
}

/** Whether a narrow window shows the notepad as a full-screen sheet rather than docked. */
export const useNotepadIsSheet = () => useMediaQuery(NOTEPAD_SHEET_QUERY, false);

/** Whether the notepad is open and docked beside the page. */
export function useNotepadDocked(): boolean {
  const isSheet = useNotepadIsSheet();
  return (useNotepad()?.isOpen ?? false) && !isSheet;
}

/**
 * Whether the docked notepad leaves pages too little room for their wide layout (`NARROW_BESIDE_NOTEPAD_QUERY`): they
 * then take their narrow one, as in a half view (`useIsNarrow`).
 */
export function useNarrowBesideNotepad(): boolean {
  const isDocked = useNotepadDocked();
  const isNarrow = useMediaQuery(NARROW_BESIDE_NOTEPAD_QUERY, false);
  return isDocked && isNarrow;
}

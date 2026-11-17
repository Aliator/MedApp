import {useMediaQuery} from '@astryxdesign/core/hooks';
import {createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode} from 'react';
import type {Procedure} from '../../api/procedures';
import {useHalfView} from '../pages/halfView';

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

function storeOpen(isOpen: boolean) {
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

/**
 * The notepad's state for the signed-in layout: whether it's open (remembered in this browser, read after mounting so
 * the server's render matches), and the procedure pages open, which its Add Note to Procedure can add to.
 */
export function NotepadProvider({children}: {children: ReactNode}) {
  const [isOpen, setIsOpen] = useState(false);
  const [offers, setOffers] = useState<OpenProcedure[]>([]);
  const closer = useRef<(() => Promise<void>) | null>(null);
  // Opened by its user: the pane takes the focus, and closing it gives the focus back to what opened it.
  const shouldFocus = useRef(false);
  const opener = useRef<HTMLElement | null>(null);

  useEffect(() => {
    // Read from this browser once mounted: the server can't know it, and its render must match the first one here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (storedOpen()) setIsOpen(true);
  }, []);

  const open = useCallback(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    shouldFocus.current = true;
    setIsOpen(true);
    storeOpen(true);
  }, []);
  const close = useCallback(() => {
    setIsOpen(false);
    storeOpen(false);
    shouldFocus.current = false;
    // Focus that was in the pane is lost as it goes: give it back to what opened it, or to the side nav's Notepad.
    setTimeout(() => {
      const active = document.activeElement;
      if (active && active !== document.body && active.isConnected) return;
      const back = opener.current?.isConnected ? opener.current : document.querySelector<HTMLElement>('[data-notepad-toggle]');
      back?.focus();
    });
  }, []);
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

  const openProcedures = useMemo(
    () => [...offers].sort((a, b) => (a.side === 'right' ? 1 : 0) - (b.side === 'right' ? 1 : 0)),
    [offers],
  );
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

import {useMediaQuery} from '@astryxdesign/core/hooks';
import {useQueryClient} from '@tanstack/react-query';
import {createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode} from 'react';
import {notepadQuery} from '../../api/notepad';
import {procedureQuery, type Procedure} from '../../api/procedures';
import {useHalfView} from '../pages/halfView';
import {newWindowId, openNotepadChannel, postNotepadMessage, type NotepadMessage} from './notepadChannel';
import {closeNotepadWindow} from './notepadWindow';

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
  /** Whether this is the notepad's own window (`/notepad`), which closing closes. */
  isWindow: boolean;
  /** Tells MedApp's other windows the notepad was saved, at this version, so a notepad there with nothing typed catches up. */
  shareSaved: (version: number) => void;
  /** Calls `listener` with the version each time another window saves the notepad, until the function returned is called. */
  onSavedElsewhere: (listener: (version: number) => void) => () => void;
  /** Tells MedApp's other windows a note was added to this procedure, so a page showing it reads it afresh. */
  shareNoteAdded: (procedureId: string) => void;
}

const NotepadContext = createContext<NotepadState | null>(null);

/** The notepad, or null outside the signed-in layout (and in tests of one part). */
export const useNotepad = () => useContext(NotepadContext);

/** Each window's procedure pages, as the notepad's own window has been told them, by the window's ID. */
type SharedProcedures = Record<string, OpenProcedure[]>;

/** The procedure pages open, left before right. */
const leftFirst = (procedures: OpenProcedure[]) =>
  [...procedures].sort((a, b) => (a.side === 'right' ? 1 : 0) - (b.side === 'right' ? 1 : 0));

/**
 * The procedure pages open in MedApp's other windows, for the notepad's own window: each procedure once (it may be
 * open in two), keyed by its window too, as keys are only unique within one.
 */
function sharedProcedures(shared: SharedProcedures): OpenProcedure[] {
  const seen = new Set<string>();
  const all: OpenProcedure[] = [];
  for (const [windowId, procedures] of Object.entries(shared)) {
    for (const open of procedures) {
      const id = open.procedure.id ?? open.key;
      if (seen.has(id)) continue;
      seen.add(id);
      all.push({...open, key: `${windowId}:${open.key}`});
    }
  }
  return all;
}

export interface NotepadProviderProps {
  children: ReactNode;
  /**
   * The notepad's own window (`/notepad`): always open, closing it closes the window, nothing is kept in this browser,
   * and Add Note to Procedure offers the procedure pages open in MedApp's other windows.
   */
  isWindow?: boolean;
}

/**
 * The notepad's state for the signed-in layout: whether it's open (remembered in this browser, read after mounting so
 * the server's render matches), and the procedure pages open, which its Add Note to Procedure can add to. Or, with
 * `isWindow`, for the notepad's own window.
 *
 * It keeps MedApp's windows in step (`notepadChannel`): it tells the others when the notepad is saved, and when a note
 * is added to a procedure (a window showing that procedure then reads it afresh). The signed-in layout shares its
 * procedure pages with the notepad's own window whenever they change and when asked, and none as its window goes.
 */
export function NotepadProvider({children, isWindow = false}: NotepadProviderProps) {
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(isWindow);
  const [offers, setOffers] = useState<OpenProcedure[]>([]);
  // In the notepad's own window: the procedure pages open in MedApp's other windows.
  const [shared, setShared] = useState<SharedProcedures>({});
  const closer = useRef<(() => Promise<void>) | null>(null);
  // Opened by its user: the pane takes the focus, and closing it gives the focus back to what opened it. The notepad's
  // own window was opened to write in, so it takes the focus as it starts.
  const shouldFocus = useRef(isWindow);
  const opener = useRef<HTMLElement | null>(null);
  const channel = useRef<BroadcastChannel | null>(null);
  const windowId = useRef<string | null>(null);
  const savedListeners = useRef(new Set<(version: number) => void>());

  useEffect(() => {
    // Read from this browser once mounted: the server can't know it, and its render must match the first one here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!isWindow && storedOpen()) setIsOpen(true);
  }, [isWindow]);

  const openProcedures = useMemo(
    () => leftFirst(isWindow ? [...offers, ...sharedProcedures(shared)] : offers),
    [offers, shared, isWindow],
  );
  // The latest, for answering a window that asks (the channel's listener is set up once).
  const latestProcedures = useRef(openProcedures);

  // The channel to MedApp's other windows, where the browser has one.
  useEffect(() => {
    windowId.current ??= newWindowId();
    const id = windowId.current;
    const post = (message: NotepadMessage) => postNotepadMessage(channel.current, message);
    channel.current = openNotepadChannel((message) => {
      switch (message.type) {
        case 'saved': {
          // A copy kept here from before is out of date: it's read afresh when a notepad next opens here. A pane open
          // now reads it afresh itself, if nothing's typed in it.
          const cached = queryClient.getQueryData(notepadQuery().queryKey);
          if (cached && (cached.version ?? 0) < message.version) {
            void queryClient.invalidateQueries({queryKey: notepadQuery().queryKey, refetchType: 'none'});
          }
          savedListeners.current.forEach((listener) => listener(message.version));
          break;
        }
        case 'note-added':
          void queryClient.invalidateQueries({queryKey: procedureQuery(message.procedureId).queryKey});
          break;
        case 'ask-procedures':
          if (!isWindow) post({type: 'procedures', windowId: id, procedures: latestProcedures.current});
          break;
        case 'procedures':
          if (isWindow) {
            setShared((current) => {
              const others = Object.fromEntries(Object.entries(current).filter(([key]) => key !== message.windowId));
              return message.procedures.length > 0 ? {...others, [message.windowId]: message.procedures} : others;
            });
          }
          break;
      }
    });
    if (isWindow) post({type: 'ask-procedures'});
    return () => {
      channel.current?.close();
      channel.current = null;
    };
  }, [isWindow, queryClient]);

  // The signed-in layout shares its procedure pages whenever they change (after the channel opens, above), none as its
  // window goes, and them again if the browser brings the page back.
  useEffect(() => {
    latestProcedures.current = openProcedures;
    if (!isWindow) postNotepadMessage(channel.current, {type: 'procedures', windowId: windowId.current!, procedures: openProcedures});
  }, [openProcedures, isWindow]);
  useEffect(() => {
    if (isWindow) return;
    const share = (procedures: OpenProcedure[]) =>
      postNotepadMessage(channel.current, {type: 'procedures', windowId: windowId.current!, procedures});
    const onHide = () => share([]);
    const onShow = (event: PageTransitionEvent) => {
      if (event.persisted) share(latestProcedures.current);
    };
    window.addEventListener('pagehide', onHide);
    window.addEventListener('pageshow', onShow);
    return () => {
      window.removeEventListener('pagehide', onHide);
      window.removeEventListener('pageshow', onShow);
    };
  }, [isWindow]);

  const shareSaved = useCallback((version: number) => postNotepadMessage(channel.current, {type: 'saved', version}), []);
  const onSavedElsewhere = useCallback((listener: (version: number) => void) => {
    savedListeners.current.add(listener);
    return () => void savedListeners.current.delete(listener);
  }, []);
  const shareNoteAdded = useCallback(
    (procedureId: string) => postNotepadMessage(channel.current, {type: 'note-added', procedureId}),
    [],
  );

  const open = useCallback(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    shouldFocus.current = true;
    setIsOpen(true);
    if (!isWindow) storeOpen(true);
  }, [isWindow]);
  const close = useCallback(() => {
    // The notepad's own window: closing it closes the window.
    if (isWindow) {
      closeNotepadWindow();
      return;
    }
    setIsOpen(false);
    storeOpen(false);
    shouldFocus.current = false;
    // Focus that was in the pane is lost as it goes: give it back to what opened it, or the side nav's Notepad, if
    // either is still shown; otherwise (a phone, whose nav drawer has closed) to the page's title. Again once the
    // sheet's closing animation is over, as its own focus return can't find the nav drawer's item either.
    const restore = () => {
      const active = document.activeElement;
      const isLost = !active || active === document.body || !active.isConnected || active.closest('[role="region"][aria-label="Notepad"]');
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
  }, [isWindow]);
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

  const value = useMemo(
    () => ({
      isOpen,
      open,
      close,
      requestClose,
      registerCloser,
      openProcedures,
      offer,
      takeFocus,
      isWindow,
      shareSaved,
      onSavedElsewhere,
      shareNoteAdded,
    }),
    [isOpen, open, close, requestClose, registerCloser, openProcedures, offer, takeFocus, isWindow, shareSaved, onSavedElsewhere, shareNoteAdded],
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

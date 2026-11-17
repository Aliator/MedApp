import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useCallback, useEffect, useLayoutEffect, useRef, useState} from 'react';
import {ApiError, problemMessage} from '../../api/errors';
import {notepadQuery, useSaveNotepad, type Notepad} from '../../api/notepad';
import {useAnnounce} from '../../app/announcements';
import {onFlushSettings} from '../../app/settingsStore';
import {formatTime} from '../../utils/dates';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField, type RichTextFieldHandle} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AddToProcedureDialog, type NoteToAdd} from './AddToProcedureDialog';
import {useNotepad} from './NotepadContext';

/** How long typing must pause before the notepad saves by itself. */
export const AUTOSAVE_MS = 1500;

/** Said, and shown, when a note was added but the text selected couldn't be taken out of the notepad exactly. */
export const NOT_REMOVED =
  'The note was added, but the selected text could not be taken out of the notepad exactly, as it changed meanwhile. Remove it yourself.';

export const SAVE_FAILED = 'The notepad could not be saved.';
export const NOTEPAD_CONFLICT_FALLBACK = 'Your notepad was saved somewhere else since this one loaded it. Reload it to see the latest.';

/** Where saving has got to. */
type SaveState =
  | {kind: 'idle'}
  | {kind: 'saving'}
  | {kind: 'saved'; at: string}
  | {kind: 'error'; message: string}
  /** Saved somewhere else meanwhile (another tab), or cleared after its days: only Reload can go on. */
  | {kind: 'conflict'; message: string};

/**
 * The notepad: a header with Close, a rich text box, where saving has got to, and Clear and Add Note to Procedure. It
 * reads the notepad once as it opens; the box is its copy from then on. Reload (after a 409) reads it afresh, replacing
 * what the box says.
 */
export function NotepadPane() {
  const notepad = useNotepad();
  const {data, error, isPending, refetch} = useQuery(notepadQuery());
  // Changed when the notepad is read afresh, so the box starts again from it.
  const [loaded, setLoaded] = useState(0);

  const reload = async () => {
    await refetch();
    setLoaded((n) => n + 1);
  };

  return (
    // A labelled region, not a complementary landmark: docked, it sits inside the page's main area.
    <VStack gap={0} height="100%" hAlign="stretch" aria-label="Notepad" role="region">
      <HStack gap={2} vAlign="center" paddingInline={4} paddingBlock={3} style={HEADER}>
        <Icon icon={AppIcon.Notes} size="sm" />
        <Text type="body" size="lg" weight="bold">
          Notepad
        </Text>
        <StackItem size="fill" />
        <IconButton label="Close notepad" icon={<Icon icon={AppIcon.Cancel} />} variant="ghost" onClick={() => notepad?.requestClose()} />
      </HStack>
      {isPending ? (
        <Center axis="horizontal" padding={10}>
          <Spinner size="lg" label="Loading the notepad..." />
        </Center>
      ) : error || !data ? (
        <VStack gap={3} padding={4} hAlign="start">
          <AlertMessage message="The notepad could not be loaded. Please try again." />
          <Button label="Try Again" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} onClick={() => void reload()} />
        </VStack>
      ) : (
        <NotepadEditor key={loaded} notepad={data} onReload={() => void reload()} onRefresh={() => void refetch()} />
      )}
    </VStack>
  );
}

/** The header bar, in the header bars' shade, so it follows the chosen colours. */
const HEADER = {background: 'var(--color-background-muted)'} as const;

interface NotepadEditorProps {
  /** The notepad as last read or saved (anywhere in this tab). */
  notepad: Notepad;
  /** Reads it afresh and starts the box again from it (after a 409). */
  onReload: () => void;
  /** Reads it afresh in the background; a newer copy then replaces the box's if nothing's typed in it. */
  onRefresh: () => void;
}

/**
 * The notepad's box, saving by itself a moment after typing stops (`AUTOSAVE_MS`), on Ctrl+S, before it closes, before
 * signing out, and as it goes (the window crossing between docked and sheet). Each save sends the version it started
 * from, so another tab's save in between is a 409, which stops saving until it's reloaded. With nothing typed, it reads
 * the notepad afresh when the tab is shown again, or when another of MedApp's windows saves it, so a change made there
 * shows here before anything's typed over it. In the notepad's own window, the browser warns before the window is
 * closed with something unsaved.
 */
function NotepadEditor({notepad, onReload, onRefresh}: NotepadEditorProps) {
  const context = useNotepad();
  const announce = useAnnounce();
  const save = useSaveNotepad();
  const queryClient = useQueryClient();
  const shareSaved = context?.shareSaved;

  // The text the box started from, and a key that starts it again when the text is replaced (cleared, or a note taken out).
  const [start, setStart] = useState({text: notepad.text ?? '', key: 0});
  const draft = useRef(notepad.text ?? '');
  const saved = useRef({text: notepad.text ?? '', version: notepad.version ?? 0});
  const [state, setStateNow] = useState<SaveState>(notepad.lastUpdatedUtc ? {kind: 'saved', at: notepad.lastUpdatedUtc} : {kind: 'idle'});
  // The same, for the save in progress to read at once.
  const stateRef = useRef(state);
  const setState = useCallback((next: SaveState) => {
    stateRef.current = next;
    setStateNow(next);
  }, []);
  const [isDirty, setIsDirty] = useState(false);
  const [isEmpty, setIsEmpty] = useState(!(notepad.text ?? '').trim());
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const inFlight = useRef<Promise<boolean> | null>(null);
  const [isConfirmingClear, setIsConfirmingClear] = useState(false);
  const [toAdd, setToAdd] = useState<NoteToAdd | null>(null);
  const box = useRef<HTMLDivElement>(null);
  const field = useRef<RichTextFieldHandle>(null);
  // What's selected in the box, as the browser shows it, kept as it changes (a click on a button outside it doesn't
  // change it): whether anything is, and its plain text in case the editor can't say exactly what.
  const selection = useRef('');
  // A note was added but its text couldn't be taken out: said until the box changes.
  const [notice, setNotice] = useState<string | null>(null);
  // The writing area's height, fitted to the room in the pane (at least 12rem).
  const [fitted, setFitted] = useState<number | undefined>(undefined);

  // The latest saveNow, for the timer to call (it's made below, and calls schedule itself).
  const saveLater = useRef<() => Promise<boolean>>(async () => true);
  const schedule = useCallback(() => {
    clearTimeout(timer.current);
    timer.current = setTimeout(() => void saveLater.current(), AUTOSAVE_MS);
  }, []);

  /** Saves what the box says now, if it differs from what was last saved. Whether it's saved (or had nothing to save). */
  const saveNow = useCallback(async (): Promise<boolean> => {
    clearTimeout(timer.current);
    // Wait for each save in turn: another may have started while this one waited, from the version the last returned,
    // and two from the same version would make the second a false conflict. Nothing can start between the loop ending
    // and `inFlight` being set below (there's no await between them).
    while (inFlight.current) await inFlight.current;
    if (stateRef.current.kind === 'conflict') return false;
    const text = draft.current;
    if (text === saved.current.text) return true;

    setState({kind: 'saving'});
    const attempt = save
      .mutateAsync({text, expectedVersion: saved.current.version})
      .then(
        (result) => {
          saved.current = {text, version: result.version ?? 0};
          setState({kind: 'saved', at: result.lastUpdatedUtc ?? new Date().toISOString()});
          setIsDirty(draft.current !== text);
          // MedApp's other windows catch up, if nothing's typed in their notepads.
          shareSaved?.(saved.current.version);
          return true;
        },
        (e: unknown) => {
          const isConflict = e instanceof ApiError && e.status === 409;
          setState(isConflict ? {kind: 'conflict', message: problemMessage(e, NOTEPAD_CONFLICT_FALLBACK)} : {kind: 'error', message: SAVE_FAILED});
          announce(isConflict ? 'The notepad was not saved: it changed somewhere else' : SAVE_FAILED);
          return false;
        },
      )
      .finally(() => {
        inFlight.current = null;
      });
    inFlight.current = attempt;
    const ok = await attempt;
    // Typed on while it saved: save that too, after the usual pause.
    if (ok && draft.current !== saved.current.text) schedule();
    return ok;
  }, [save, announce, setState, schedule, shareSaved]);

  useEffect(() => {
    saveLater.current = saveNow;
  }, [saveNow]);

  // As it goes with something unsaved (the window crossed between docked and sheet, and the other takes over), it saves
  // it, and first puts the text in the shared copy, so the pane taking over starts from it rather than from before.
  useEffect(
    () => () => {
      clearTimeout(timer.current);
      if (stateRef.current.kind === 'conflict' || draft.current === saved.current.text) return;
      const text = draft.current;
      queryClient.setQueryData(notepadQuery().queryKey, (current) => current && {...current, text});
      void saveLater.current();
    },
    [queryClient],
  );

  // A newer copy than this box's (another tab's change read afresh, or a save by the pane this one took over from): the
  // same text is just a newer version to save from; other text replaces the box's, unless something's typed in it.
  useEffect(() => {
    const version = notepad.version ?? 0;
    const text = notepad.text ?? '';
    if (version <= saved.current.version) return;
    if (text === saved.current.text) {
      saved.current = {text, version};
      return;
    }
    if (draft.current !== saved.current.text || inFlight.current) return;
    saved.current = {text, version};
    draft.current = text;
    selection.current = '';
    // The box starts again from the copy read afresh (a copy from outside this box, as the effect's own name says).
    /* eslint-disable react-hooks/set-state-in-effect */
    setStart((current) => ({text, key: current.key + 1}));
    setIsEmpty(!text.trim());
    if (notepad.lastUpdatedUtc) setState({kind: 'saved', at: notepad.lastUpdatedUtc});
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [notepad, setState]);

  // Nothing typed, no save on its way and no conflict: the box can take a newer copy.
  const isIdle = useCallback(
    () => draft.current === saved.current.text && !inFlight.current && stateRef.current.kind !== 'conflict',
    [],
  );

  // Back to this tab with nothing typed: read the notepad afresh, in case another tab changed it.
  useEffect(() => {
    const refresh = () => {
      if (document.visibilityState !== 'visible' || !isIdle()) return;
      onRefresh();
    };
    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('focus', refresh);
    return () => {
      document.removeEventListener('visibilitychange', refresh);
      window.removeEventListener('focus', refresh);
    };
  }, [onRefresh, isIdle]);

  // Saved in another of MedApp's windows, at a newer version than this box's, with nothing typed here: read it afresh
  // (the newer copy then replaces the box's, above). So a window opened while another's save was on its way catches up.
  const onSavedElsewhere = context?.onSavedElsewhere;
  useEffect(
    () =>
      onSavedElsewhere?.((version) => {
        if (version > saved.current.version && isIdle()) onRefresh();
      }),
    [onSavedElsewhere, onRefresh, isIdle],
  );

  // The notepad's own window: the browser asks before it's closed with something unsaved, or a save on its way.
  const isWindow = context?.isWindow ?? false;
  useEffect(() => {
    if (!isWindow) return;
    const warn = (event: BeforeUnloadEvent) => {
      if (draft.current === saved.current.text && !inFlight.current) return;
      event.preventDefault();
      // Older browsers ask only when this is set.
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [isWindow]);

  // Opened by its user: the cursor goes in the box, so they can write at once.
  useEffect(() => {
    if (context?.takeFocus()) field.current?.focus();
  }, [context]);

  // The writing area fills the room left in the pane (docked or a sheet), and grows past it with its text.
  useLayoutEffect(() => {
    const scroller = box.current;
    if (!scroller || typeof ResizeObserver === 'undefined') return;
    const fit = () => {
      const editable = scroller.querySelector<HTMLElement>('[contenteditable]');
      if (!editable) return;
      const top = editable.getBoundingClientRect().top - scroller.getBoundingClientRect().top + scroller.scrollTop;
      // What sits below the writing area in the scroller: the bottom padding, border and margin of each box it's in
      // (the editor's frame), and the scroller's own padding. Measured rather than guessed, so an empty notepad fits
      // exactly and shows no scrollbar.
      let below = parseFloat(getComputedStyle(scroller).paddingBlockEnd) || 0;
      for (let element = editable.parentElement; element && element !== scroller; element = element.parentElement) {
        const style = getComputedStyle(element);
        below += (parseFloat(style.paddingBlockEnd) || 0) + (parseFloat(style.borderBlockEndWidth) || 0) + (parseFloat(style.marginBlockEnd) || 0);
      }
      const least = 12 * (parseFloat(getComputedStyle(document.documentElement).fontSize) || 16);
      const height = Math.max(least, Math.floor(scroller.clientHeight - top - below));
      setFitted((current) => (current !== undefined && Math.abs(current - height) < 2 ? current : height));
    };
    const observer = new ResizeObserver(fit);
    observer.observe(scroller);
    fit();
    return () => observer.disconnect();
  }, []);

  const onChange = (markdown: string) => {
    draft.current = markdown;
    setIsDirty(markdown !== saved.current.text);
    setIsEmpty(!markdown.trim());
    setNotice(null);
    if (stateRef.current.kind !== 'conflict') schedule();
  };

  /** Replaces what the box says (cleared, or a note taken out), and saves it at once. */
  const replace = (text: string) => {
    draft.current = text;
    setStart((current) => ({text, key: current.key + 1}));
    setIsEmpty(!text.trim());
    selection.current = '';
    void saveNow();
  };

  // Ctrl+S saves now, when there's something to save.
  useSaveShortcut(box, () => {
    if (draft.current === saved.current.text && !inFlight.current) return false;
    void saveNow();
  });

  // Signing out saves a change first, as it does settings (useLogout).
  useEffect(() => onFlushSettings(async () => void (await saveNow())), [saveNow]);

  // Closing saves a change first, wherever it's closed from (Close, the side nav, Escape on the sheet); if the save
  // fails, it stays open, so nothing typed is lost.
  const close = context?.close;
  const registerCloser = context?.registerCloser;
  useEffect(
    () =>
      registerCloser?.(async () => {
        if (await saveNow()) close?.();
      }),
    [registerCloser, close, saveNow],
  );

  useEffect(() => {
    const onSelectionChange = () => {
      const current = document.getSelection();
      const inBox = current && current.rangeCount > 0 && box.current?.contains(current.anchorNode);
      selection.current = inBox && !current.isCollapsed ? current.toString() : '';
    };
    document.addEventListener('selectionchange', onSelectionChange);
    return () => document.removeEventListener('selectionchange', onSelectionChange);
  }, []);

  const procedures = context?.openProcedures ?? [];
  /**
   * The text selected (as Markdown, from the editor, so it keeps its formatting), or everything. Taking a selection out
   * afterwards removes exactly that range; if the editor can't say what's selected, it's added as the browser shows it,
   * and the dialog says it can't be taken out.
   */
  const addToProcedure = () => {
    if (selection.current.trim()) {
      const exact = field.current?.selection() ?? null;
      setToAdd(exact ? {text: exact.markdown, isSelection: true, remove: exact.remove} : {text: selection.current.trim(), isSelection: true});
      return;
    }
    const text = draft.current.trim();
    if (text) setToAdd({text, isSelection: false, remove: () => ''});
  };

  /**
   * After a note was added: MedApp's other windows showing the procedure read it afresh (the notepad's own window adds
   * to their pages); then take it out of the notepad if asked, exactly as selected, or say it couldn't be.
   */
  const added = (note: NoteToAdd, removeFromNotepad: boolean, procedureId: string) => {
    setToAdd(null);
    context?.shareNoteAdded(procedureId);
    if (!removeFromNotepad) return;
    if (!note.isSelection) {
      replace('');
      return;
    }
    const without = note.remove?.() ?? null;
    if (without === null) {
      setNotice(NOT_REMOVED);
      announce(NOT_REMOVED);
      return;
    }
    // The editor has taken it out itself (and reported the change): save that now.
    draft.current = without;
    selection.current = '';
    setIsEmpty(!without.trim());
    void saveNow();
  };

  return (
    <>
      <VStack ref={box} gap={3} padding={4} hAlign="stretch" style={{flex: 1, minHeight: 0, overflowY: 'auto'}}>
        {notice && <AlertMessage message={notice} />}
        {/* A failed save shows below, never on the box, so it has no error line. */}
        <VStack gap={0} style={NO_FIELD_STATUS_LINE}>
          <RichTextField
            ref={field}
            key={start.key}
            label="Notepad"
            isLabelHidden
            value={start.text}
            onChange={onChange}
            minHeight={fitted ?? '12rem'}
          />
        </VStack>
      </VStack>
      <VStack gap={2} paddingInline={4} paddingBlock={3} hAlign="stretch" style={FOOTER}>
        <SaveStatus state={state} isDirty={isDirty} onRetry={() => void saveNow()} onReload={onReload} />
        <HStack gap={2} wrap="wrap" vAlign="center">
          <Button
            label="Clear"
            variant="ghost"
            icon={<Icon icon={AppIcon.Delete} />}
            onClick={() => setIsConfirmingClear(true)}
            isDisabled={isEmpty || state.kind === 'conflict'}
          />
          <StackItem size="fill" />
          <Button
            label="Add Note to Procedure"
            variant="secondary"
            icon={<Icon icon={AppIcon.Add} />}
            onClick={addToProcedure}
            isDisabled={procedures.length === 0 || isEmpty}
            tooltip={procedures.length === 0 ? "Open a procedure's page to add a note to it" : undefined}
          />
        </HStack>
      </VStack>
      <ConfirmDialog
        isOpen={isConfirmingClear}
        title="Clear Notepad"
        message="Everything in the notepad will be removed. This can't be undone."
        isDanger
        confirmText="Clear Notepad"
        onConfirm={() => {
          setIsConfirmingClear(false);
          replace('');
          announce('Notepad cleared');
        }}
        onCancel={() => setIsConfirmingClear(false)}
      />
      {toAdd && (
        <AddToProcedureDialog note={toAdd} procedures={procedures} onAdded={(remove, procedureId) => added(toAdd, remove, procedureId)} onCancel={() => setToAdd(null)} />
      )}
    </>
  );
}

const FOOTER = {borderBlockStart: '1px solid var(--color-border)'} as const;

/** "Saving...", "Saved 10:32", "Unsaved changes", or what went wrong, with Retry or Reload. */
function SaveStatus({state, isDirty, onRetry, onReload}: {state: SaveState; isDirty: boolean; onRetry: () => void; onReload: () => void}) {
  if (state.kind === 'error' || state.kind === 'conflict') {
    return (
      <HStack gap={2} vAlign="center" wrap="wrap">
        <StackItem size="fill">
          <AlertMessage message={state.message} />
        </StackItem>
        {state.kind === 'error' ? (
          <Button label="Retry" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={onRetry} />
        ) : (
          <Button label="Reload" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={onReload} />
        )}
      </HStack>
    );
  }
  // Never saved, and nothing typed: there's nothing written yet (not something that failed to save).
  const words =
    state.kind === 'saving' ? 'Saving...' : isDirty ? 'Unsaved changes' : state.kind === 'saved' ? `Saved ${formatTime(state.at)}` : 'Nothing written yet';
  return (
    // Not a live region: it changes every few seconds while typing. A failed save is announced instead.
    <Text type="supporting" color="secondary">
      {words}
    </Text>
  );
}

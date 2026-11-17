import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useCallback, useEffect, useRef, useState} from 'react';
import {ApiError, problemMessage} from '../../api/errors';
import {notepadQuery, useSaveNotepad, type Notepad} from '../../api/notepad';
import {useAnnounce} from '../../app/announcements';
import {onFlushSettings} from '../../app/settingsStore';
import {formatTime} from '../../utils/dates';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AddToProcedureDialog, type NoteToAdd} from './AddToProcedureDialog';
import {useNotepad} from './NotepadContext';

/** How long typing must pause before the notepad saves by itself. */
export const AUTOSAVE_MS = 1500;

/** What the notepad says it's for, and how long it's kept (`keepDays`, from the API). */
export const notepadHint = (keepDays: number) =>
  `For jottings. Anything about a patient belongs in their record: use Add note to procedure. Cleared after ${keepDays} days without changes.`;

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
 * The notepad: a header with Close, what it's for, a rich text box, where saving has got to, and Clear and Add Note to
 * Procedure. It reads the notepad once as it opens; the box is its copy from then on. Reload (after a 409) reads it
 * afresh, replacing what the box says.
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
    <VStack gap={0} height="100%" hAlign="stretch" aria-label="Notepad" role="complementary">
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
        <NotepadEditor key={loaded} notepad={data} onReload={() => void reload()} />
      )}
    </VStack>
  );
}

/** The header bar, in the header bars' shade, so it follows the chosen colours. */
const HEADER = {background: 'var(--color-background-muted)'} as const;

interface NotepadEditorProps {
  notepad: Notepad;
  onReload: () => void;
}

/**
 * The notepad's box, saving by itself a moment after typing stops (`AUTOSAVE_MS`), on Ctrl+S, before it closes and
 * before signing out. Each save sends the version it started from, so another tab's save in between is a 409, which
 * stops saving until it's reloaded.
 */
function NotepadEditor({notepad, onReload}: NotepadEditorProps) {
  const context = useNotepad();
  const announce = useAnnounce();
  const save = useSaveNotepad();
  const keepDays = notepad.keepDays ?? 30;

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
  // What's selected in the box, kept as it changes (a click on a button outside it doesn't change it).
  const selection = useRef('');

  // The latest saveNow, for the timer to call (it's made below, and calls schedule itself).
  const saveLater = useRef<() => Promise<boolean>>(async () => true);
  const schedule = useCallback(() => {
    clearTimeout(timer.current);
    timer.current = setTimeout(() => void saveLater.current(), AUTOSAVE_MS);
  }, []);

  /** Saves what the box says now, if it differs from what was last saved. Whether it's saved (or had nothing to save). */
  const saveNow = useCallback(async (): Promise<boolean> => {
    clearTimeout(timer.current);
    if (inFlight.current) await inFlight.current;
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
  }, [save, announce, setState, schedule]);

  useEffect(() => {
    saveLater.current = saveNow;
  }, [saveNow]);

  useEffect(() => () => clearTimeout(timer.current), []);

  const onChange = (markdown: string) => {
    draft.current = markdown;
    setIsDirty(markdown !== saved.current.text);
    setIsEmpty(!markdown.trim());
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
  const addToProcedure = () => {
    const selected = selection.current.trim();
    const text = selected || draft.current.trim();
    if (!text) return;
    setToAdd({text, isSelection: selected !== ''});
  };

  /** After a note was added: take it out of the notepad if asked. */
  const added = (note: NoteToAdd, removeFromNotepad: boolean) => {
    setToAdd(null);
    if (!removeFromNotepad) return;
    if (!note.isSelection) {
      replace('');
      return;
    }
    const without = removeFirst(draft.current, note.text);
    if (without === null) announce('The selected text could not be taken out of the notepad: remove it yourself.');
    else replace(without);
  };

  return (
    <>
      <VStack ref={box} gap={3} padding={4} hAlign="stretch" style={{flex: 1, minHeight: 0, overflowY: 'auto'}}>
        <Text type="supporting" color="secondary">
          {notepadHint(keepDays)}
        </Text>
        {/* A failed save shows below, never on the box, so it has no error line. */}
        <VStack gap={0} style={NO_FIELD_STATUS_LINE}>
          <RichTextField
            key={start.key}
            label="Notepad"
            isLabelHidden
            value={start.text}
            onChange={onChange}
            placeholder="Jot something down"
            minHeight="12rem"
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
        <AddToProcedureDialog note={toAdd} procedures={procedures} onAdded={(remove) => added(toAdd, remove)} onCancel={() => setToAdd(null)} />
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
  const words =
    state.kind === 'saving' ? 'Saving...' : isDirty ? 'Unsaved changes' : state.kind === 'saved' ? `Saved ${formatTime(state.at)}` : 'Not saved yet';
  return (
    // Not a live region: it changes every few seconds while typing. A failed save is announced instead.
    <Text type="supporting" color="secondary">
      {words}
    </Text>
  );
}

/**
 * The text without the first occurrence of `part` (blank lines left behind tidied up), or null when it isn't there as
 * it was selected (a selection is plain text; formatting inside it, such as bold, keeps it from matching).
 */
export function removeFirst(text: string, part: string): string | null {
  const at = text.indexOf(part);
  if (at < 0) return null;
  return (text.slice(0, at) + text.slice(at + part.length)).replace(/\n{3,}/g, '\n\n').trim();
}

import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useRef, useState} from 'react';
import {ApiError, problemMessage} from '../../api/errors';
import {useProcedureNoteActions, useReloadProcedure, type Procedure, type ProcedureNote} from '../../api/procedures';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime} from '../../utils/dates';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';

/** The most a note can hold, as the API allows. */
export const MAX_NOTE_LENGTH = 4000;

/** Said when an edit is refused because the note changed somewhere else first (a 409), with the API's own words if any. */
export const NOTE_CONFLICT_FALLBACK = 'This note was changed somewhere else after you opened it. Reload to see it as it is now.';

/** A problem saving a note: what to say, and whether it was a 409 (so Reload is offered). */
interface NoteProblem {
  message: string;
  isConflict: boolean;
}

const toProblem = (error: unknown, fallback: string): NoteProblem => {
  const isConflict = error instanceof ApiError && error.status === 409;
  return {message: problemMessage(error, isConflict ? NOTE_CONFLICT_FALLBACK : fallback), isConflict};
};

/** "k.ashford, 3 Nov 2026, 10:32": who wrote a note and when. */
export const noteByline = (note: ProcedureNote) => `${note.authorUsername ?? ''}, ${formatDateTime(note.createdAtUtc ?? '')}`;

export interface ProcedureNotesProps {
  procedure: Procedure;
}

/**
 * A procedure's notes: Add Note (a rich text box; Ctrl+S adds it too), then every note, newest first, each with who
 * wrote it and when, "Edited" with when if it has been, and its text. Your own notes have Edit, while the procedure
 * takes notes (`canAddNotes`: not once it's cancelled or archived, nor while its patient's record is restricted or
 * anonymised). Nobody edits anyone else's note, admins included, and nobody deletes one.
 *
 * Saving goes through `useProcedureNoteActions`, which puts the note into the cached procedure at once. An edit sends
 * the version it started from: if the note was changed somewhere else meanwhile (another tab), the API refuses it
 * (409), and this says so with Reload, which reads the procedure afresh and closes the edit.
 */
export function ProcedureNotes({procedure}: ProcedureNotesProps) {
  const id = procedure.id ?? '';
  const actions = useProcedureNoteActions(id);
  const reload = useReloadProcedure(id);
  const announce = useAnnounce();
  const canAdd = procedure.canAddNotes ?? false;
  const notes = procedure.notes ?? [];

  const [draft, setDraft] = useState('');
  // Changed after each note is added, so the box starts empty again (it isn't controlled).
  const [addKey, setAddKey] = useState(0);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [problem, setProblem] = useState<NoteProblem | null>(null);
  const isBusy = actions.add.isPending || actions.edit.isPending;
  const form = useRef<HTMLDivElement>(null);

  const add = () => {
    if (isBusy) return;
    if (!draft.trim()) return false;
    setProblem(null);
    actions.add.mutate(draft, {
      onSuccess: () => {
        setDraft('');
        setAddKey((k) => k + 1);
        announce('Note added');
      },
      onError: (e) => setProblem(toProblem(e, 'The note could not be added.')),
    });
  };
  useSaveShortcut(form, canAdd ? add : undefined);

  const doReload = () => {
    setProblem(null);
    setEditingId(null);
    void reload();
  };

  return (
    <VStack gap={4} hAlign="stretch">
      {problem && (
        <VStack gap={2} hAlign="start">
          <AlertMessage message={problem.message} />
          {problem.isConflict && (
            <Button label="Reload" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} onClick={doReload} />
          )}
        </VStack>
      )}
      {canAdd && (
        <VStack ref={form} gap={2} hAlign="stretch">
          {/* A failed save shows above, never on the box, so it has no error line. */}
          <VStack gap={0} style={NO_FIELD_STATUS_LINE}>
            <RichTextField
              key={addKey}
              label="New note"
              value=""
              onChange={setDraft}
              placeholder="Something the team should know about this procedure"
              isDisabled={actions.add.isPending}
              minHeight="5rem"
            />
          </VStack>
          <HStack gap={2} vAlign="center" wrap="wrap">
            {draft.length > MAX_NOTE_LENGTH && (
              <Text type="supporting" color="secondary">{`Too long: at most ${MAX_NOTE_LENGTH} characters.`}</Text>
            )}
            <StackItem size="fill" />
            <Button
              label="Add Note"
              variant="secondary"
              icon={<Icon icon={AppIcon.Add} />}
              onClick={() => void add()}
              isDisabled={!draft.trim() || isBusy}
            />
          </HStack>
        </VStack>
      )}
      {notes.length === 0 ? (
        <Text type="body" color="secondary">
          No notes yet.
        </Text>
      ) : (
        <List hasDividers edge="bleed" aria-label="Notes">
          {notes.map((note) =>
            note.id === editingId ? (
              <ListItem
                key={note.id}
                label={<Text type="body" weight="semibold">{`Editing your note of ${formatDateTime(note.createdAtUtc ?? '')}`}</Text>}
                description={
                  <NoteEditor
                    note={note}
                    isBusy={isBusy}
                    onCancel={() => setEditingId(null)}
                    onSave={(text) => {
                      setProblem(null);
                      actions.edit.mutate(
                        {noteId: note.id ?? '', text, expectedVersion: note.version ?? 1},
                        {
                          onSuccess: () => {
                            setEditingId(null);
                            announce('Note saved');
                          },
                          onError: (e) => setProblem(toProblem(e, 'The note could not be saved.')),
                        },
                      );
                    }}
                  />
                }
              />
            ) : (
              <ListItem
                key={note.id}
                align="first-line"
                label={
                  <HStack gap={2} vAlign="center" wrap="wrap">
                    <Text type="body" weight="semibold">
                      {noteByline(note)}
                    </Text>
                    {note.lastEditedAtUtc && <Token label={`Edited ${formatDateTime(note.lastEditedAtUtc)}`} size="sm" />}
                  </HStack>
                }
                description={<RichText>{note.text ?? ''}</RichText>}
                endContent={
                  note.canEdit && (
                    <Button
                      label="Edit"
                      tooltip="Edit your note"
                      variant="ghost"
                      size="sm"
                      icon={<Icon icon={AppIcon.Edit} />}
                      onClick={() => {
                        setProblem(null);
                        setEditingId(note.id ?? null);
                      }}
                      isDisabled={isBusy || editingId !== null}
                    />
                  )
                }
              />
            ),
          )}
        </List>
      )}
    </VStack>
  );
}

interface NoteEditorProps {
  note: ProcedureNote;
  isBusy: boolean;
  onSave: (text: string) => void;
  onCancel: () => void;
}

/** One of your notes being edited: its text in a rich text box, Save (or Ctrl+S) and Cancel. */
function NoteEditor({note, isBusy, onSave, onCancel}: NoteEditorProps) {
  const saved = note.text ?? '';
  const [draft, setDraft] = useState(saved);
  const box = useRef<HTMLDivElement>(null);
  const isChanged = draft !== saved;
  const canSave = isChanged && draft.trim() !== '' && !isBusy;
  useSaveShortcut(box, () => {
    if (isBusy) return;
    if (!canSave) return false;
    onSave(draft);
  });

  return (
    <VStack ref={box} gap={2} hAlign="stretch">
      <VStack gap={0} style={NO_FIELD_STATUS_LINE}>
        <RichTextField label="Note" isLabelHidden value={saved} onChange={setDraft} isDisabled={isBusy} minHeight="5rem" />
      </VStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        {isChanged && (
          <Text type="supporting" color="secondary">
            Unsaved changes
          </Text>
        )}
        <StackItem size="fill" />
        <Button label="Cancel" variant="ghost" onClick={onCancel} isDisabled={isBusy} />
        <Button label="Save Note" variant="secondary" icon={<Icon icon={AppIcon.Save} />} onClick={() => onSave(draft)} isDisabled={!canSave} />
      </HStack>
    </VStack>
  );
}

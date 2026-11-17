import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {Token} from '@astryxdesign/core/Token';
import {useEffect, useRef, useState} from 'react';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  useNoteDeletionActions,
  useProcedureNoteActions,
  useReloadProcedure,
  type Procedure,
  type ProcedureNote,
} from '../../api/procedures';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {formatDateTime} from '../../utils/dates';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField, type RichTextFieldHandle} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {MAX_DELETION_REASON_LENGTH, NoteDeletionRequestDialog} from './NoteDeletionRequestDialog';

/** The most a note can hold, as the API allows. */
export const MAX_NOTE_LENGTH = 4000;

/** Said when an edit is refused because the note changed somewhere else first (a 409), with the API's own words if any. */
export const NOTE_CONFLICT_FALLBACK = 'This note was changed somewhere else after you opened it. Reload to see it as it is now.';

/** A problem saving a note: what to say, whether it was a 409 (so Reload is offered), and where (the new note's box, or the note being edited). */
interface NoteProblem {
  message: string;
  isConflict: boolean;
  /** The note being edited, or null for the new note. */
  noteId: string | null;
}

const toProblem = (error: unknown, fallback: string, noteId: string | null): NoteProblem => {
  const isConflict = error instanceof ApiError && error.status === 409;
  return {message: problemMessage(error, isConflict ? NOTE_CONFLICT_FALLBACK : fallback), isConflict, noteId};
};

/** Why notes can't be added now, as the stage section says why its stages can't be worked on. */
export function whyNoNotes(procedure: Procedure): string {
  if (procedure.readOnlyReason) return procedure.readOnlyReason;
  const status = procedure.status === 'Cancelled' ? 'cancelled' : procedure.status === 'Archived' ? 'archived' : null;
  return status ? `Notes can't be added or edited while the procedure is ${status}.` : "Notes can't be added or edited now.";
}

/** "k.ashford, 3 Nov 2026, 10:32": who wrote a note and when. */
export const noteByline = (note: ProcedureNote) => `${note.authorUsername ?? ''}, ${formatDateTime(note.createdAtUtc ?? '')}`;

export interface ProcedureNotesProps {
  procedure: Procedure;
}

/**
 * A procedure's notes: Add Note (a rich text box; Ctrl+S adds it too), then every note, newest first, each with who
 * wrote it and when, "Edited" with when if it has been, and its text. Your own notes have Edit, while the procedure
 * takes notes (`canAddNotes`: not once it's cancelled or archived, nor while its patient's record is restricted or
 * anonymised). Nobody edits anyone else's note, admins included.
 *
 * Admins have Delete on every note (`canDelete`: in any status, but not while the record is restricted or
 * anonymised), and other staff Request deletion (`canRequestDeletion`); each asks for a reason first. Deleting is soft:
 * the note is hidden here, but kept. A note whose deletion has been asked for shows "Deletion requested", and an admin
 * can open the request there (`NoteDeletionRequestDialog`) to decide it.
 *
 * Saving goes through `useProcedureNoteActions`, which puts the note into the cached procedure at once. An edit sends
 * the version it started from (kept as Edit is pressed): if the note was changed somewhere else meanwhile (another
 * tab, or the other half of a half view), the API refuses it (409), and this says so beside the note, with Reload,
 * which reads the procedure afresh and closes the edit. While notes can't be added, it says why.
 */
export function ProcedureNotes({procedure}: ProcedureNotesProps) {
  const id = procedure.id ?? '';
  const actions = useProcedureNoteActions(id);
  const reload = useReloadProcedure(id);
  const announce = useAnnounce();
  const canAdd = procedure.canAddNotes ?? false;
  const notes = procedure.noteList ?? [];
  const {isAdmin} = useCurrentUser();
  const deletion = useNoteDeletionActions(id);
  // The note whose deletion is being confirmed (an admin's Delete) or asked for (Request deletion).
  const [asking, setAsking] = useState<{note: ProcedureNote; kind: 'delete' | 'request'} | null>(null);
  // The deletion request an admin opened from a note.
  const [openRequestId, setOpenRequestId] = useState<string | null>(null);

  const [draft, setDraft] = useState('');
  // Changed after each note is added, so the box starts empty again (it isn't controlled).
  const [addKey, setAddKey] = useState(0);
  // The note being edited, and the version its editor started from: that's what an edit is checked against, not the
  // version the note has by the time it's saved (another half or tab may have saved it meanwhile).
  const [editing, setEditing] = useState<{id: string; version: number} | null>(null);
  const [problem, setProblem] = useState<NoteProblem | null>(null);
  const isBusy = actions.add.isPending || actions.edit.isPending;
  const isTooLong = draft.length > MAX_NOTE_LENGTH;
  const form = useRef<HTMLDivElement>(null);
  const newNote = useRef<RichTextFieldHandle>(null);

  // After a note is added the box starts again, empty: the cursor goes back into it, rather than being lost.
  useEffect(() => {
    if (addKey > 0) newNote.current?.focus();
  }, [addKey]);

  const add = () => {
    if (isBusy) return;
    if (!draft.trim() || isTooLong) return false;
    setProblem(null);
    actions.add.mutate(draft, {
      onSuccess: () => {
        setDraft('');
        setAddKey((k) => k + 1);
        announce('Note added');
      },
      onError: (e) => setProblem(toProblem(e, 'The note could not be added.', null)),
    });
  };
  useSaveShortcut(form, canAdd ? add : undefined);

  const doReload = () => {
    setProblem(null);
    setEditing(null);
    void reload();
  };

  /** What went wrong saving, where it happened (the new note's box, or the note being edited), with Reload after a 409. */
  const problemShown = (noteId: string | null) =>
    problem &&
    problem.noteId === noteId && (
      <VStack gap={2} hAlign="start">
        <AlertMessage message={problem.message} />
        {problem.isConflict && <Button label="Reload" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} onClick={doReload} />}
      </VStack>
    );

  return (
    <VStack gap={4} hAlign="stretch">
      {!canAdd && (
        <Text type="body" color="secondary">
          {whyNoNotes(procedure)}
        </Text>
      )}
      {canAdd && (
        <VStack ref={form} gap={2} hAlign="stretch">
          {/* A failed save shows above, never on the box, so it has no error line. */}
          <VStack gap={0} style={NO_FIELD_STATUS_LINE}>
            <RichTextField
              ref={newNote}
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
            {isTooLong && <Text type="supporting" color="secondary">{`Too long: at most ${MAX_NOTE_LENGTH} characters.`}</Text>}
            <StackItem size="fill" />
            <Button
              label="Add Note"
              variant="secondary"
              icon={<Icon icon={AppIcon.Add} />}
              onClick={() => void add()}
              isDisabled={!draft.trim() || isTooLong || isBusy}
            />
          </HStack>
          {problemShown(null)}
        </VStack>
      )}
      {notes.length === 0 ? (
        <Text type="body" color="secondary">
          No notes yet.
        </Text>
      ) : (
        <List hasDividers edge="bleed" aria-label="Notes">
          {notes.map((note) =>
            editing && note.id === editing.id ? (
              <ListItem
                key={note.id}
                label={<Text type="body" weight="semibold">{`Editing your note of ${formatDateTime(note.createdAtUtc ?? '')}`}</Text>}
                description={
                  <VStack gap={2} hAlign="stretch">
                    <NoteEditor
                      note={note}
                      isBusy={isBusy}
                      onCancel={() => {
                        setProblem(null);
                        setEditing(null);
                      }}
                      onSave={(text) => {
                        setProblem(null);
                        actions.edit.mutate(
                          {noteId: editing.id, text, expectedVersion: editing.version},
                          {
                            onSuccess: () => {
                              setEditing(null);
                              announce('Note saved');
                            },
                            onError: (e) => setProblem(toProblem(e, 'The note could not be saved.', editing.id)),
                          },
                        );
                      }}
                    />
                    {/* Beside the note it's about, not at the top of the section, where it may be out of sight. */}
                    {problemShown(editing.id)}
                  </VStack>
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
                    {note.deletionRequestId && <Token label="Deletion requested" size="sm" color="orange" />}
                    {note.deletionRequestId && isAdmin && (
                      <Button
                        label="Review Request"
                        tooltip="Review the request to delete this note"
                        variant="ghost"
                        size="sm"
                        onClick={() => setOpenRequestId(note.deletionRequestId ?? null)}
                      />
                    )}
                  </HStack>
                }
                description={<RichText>{note.text ?? ''}</RichText>}
                endContent={
                  (note.canEdit || (isAdmin ? note.canDelete : note.canRequestDeletion)) && (
                    <HStack gap={1} vAlign="center">
                      {note.canEdit && (
                        <Button
                          label="Edit"
                          tooltip="Edit your note"
                          variant="ghost"
                          size="sm"
                          icon={<Icon icon={AppIcon.Edit} />}
                          onClick={() => {
                            setProblem(null);
                            setEditing(note.id ? {id: note.id, version: note.version ?? 1} : null);
                          }}
                          isDisabled={isBusy || editing !== null}
                        />
                      )}
                      {isAdmin && note.canDelete && (
                        <Button
                          label="Delete"
                          tooltip="Delete this note"
                          variant="ghost"
                          size="sm"
                          icon={<Icon icon={AppIcon.Delete} />}
                          onClick={() => setAsking({note, kind: 'delete'})}
                          isDisabled={isBusy || editing !== null}
                        />
                      )}
                      {!isAdmin && note.canRequestDeletion && (
                        <Button
                          label="Request Deletion"
                          tooltip="Ask an admin to delete this note"
                          variant="ghost"
                          size="sm"
                          icon={<Icon icon={AppIcon.Delete} />}
                          onClick={() => setAsking({note, kind: 'request'})}
                          isDisabled={isBusy || editing !== null}
                        />
                      )}
                    </HStack>
                  )
                }
              />
            ),
          )}
        </List>
      )}
      {asking && (
        <NoteDeletionReasonDialog
          key={`${asking.kind}-${asking.note.id}`}
          note={asking.note}
          kind={asking.kind}
          isProcessing={deletion.remove.isPending || deletion.request.isPending}
          onCancel={() => setAsking(null)}
          onConfirm={(reason, fail) => {
            const noteId = asking.note.id ?? '';
            const mutation = asking.kind === 'delete' ? deletion.remove : deletion.request;
            mutation.mutate(
              {noteId, reason},
              {
                onSuccess: () => {
                  setAsking(null);
                  announce(asking.kind === 'delete' ? 'Note deleted' : 'Deletion requested. An admin will decide.');
                },
                onError: fail,
              },
            );
          }}
        />
      )}
      <NoteDeletionRequestDialog requestId={openRequestId} onClose={() => setOpenRequestId(null)} />
    </VStack>
  );
}

/** Asking to delete a note: an admin's Delete, or staff's Request deletion. */
const REASON_DIALOG = {
  delete: {
    title: 'Delete Note',
    message:
      'Delete this note? It will be hidden from the procedure for everyone, but kept in the record\'s history and export, with your reason. If someone asked for it to be deleted, they are told.',
    confirmText: 'Delete Note',
  },
  request: {
    title: 'Request Note Deletion',
    message: 'Ask an admin to delete this note? Every admin is told, and one of them decides. Say why it should go.',
    confirmText: 'Request Deletion',
  },
} as const;

interface NoteDeletionReasonDialogProps {
  note: ProcedureNote;
  kind: 'delete' | 'request';
  isProcessing: boolean;
  /** Sends it; `fail` shows what went wrong (a 400 on the reason, or the API's words for a 409). */
  onConfirm: (reason: string, fail: (error: unknown) => void) => void;
  onCancel: () => void;
}

/** Confirms deleting a note, or asking for it to be, with the note shown and a reason (required, 500 characters at most). */
function NoteDeletionReasonDialog({note, kind, isProcessing, onConfirm, onCancel}: NoteDeletionReasonDialogProps) {
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const isTooLong = reason.length > MAX_DELETION_REASON_LENGTH;
  const words = REASON_DIALOG[kind];

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError && error.errors.fields.reason?.length) setErrors(error.errors);
    else setProblem(problemMessage(error, kind === 'delete' ? 'The note could not be deleted.' : 'The request could not be sent.'));
  };

  return (
    <ConfirmDialog
      isOpen
      title={words.title}
      message={words.message}
      icon={AppIcon.Delete}
      isDanger={kind === 'delete'}
      intent={kind === 'delete' ? 'danger' : 'warning'}
      confirmText={words.confirmText}
      confirmDisabled={!reason.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        setProblem(null);
        setErrors(null);
        onConfirm(reason.trim(), fail);
      }}
      onCancel={onCancel}>
      <VStack gap={1} hAlign="stretch">
        <Text type="body" weight="semibold">
          {noteByline(note)}
        </Text>
        <RichText>{note.text ?? ''}</RichText>
      </VStack>
      <TextArea
        label="Reason"
        value={reason}
        onChange={setReason}
        rows={3}
        isRequired
        isDisabled={isProcessing}
        status={
          fieldStatus(errors, 'reason') ?? (isTooLong ? {type: 'error', message: `At most ${MAX_DELETION_REASON_LENGTH} characters.`} : undefined)
        }
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
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

import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useEffect, useRef, useState} from 'react';
import {ApiError, ApiValidationError, problemMessage, type ValidationErrors} from '../../api/errors';
import type {NoteDeletionReason} from '../../api/noteDeletion';
import {
  useNoteDeletionActions,
  usePathwayNoteActions,
  useReloadPathway,
  type Pathway,
  type PathwayNote,
} from '../../api/pathways';
import {useAnnounce} from '../../app/announcements';
import {useCan} from '../../auth/auth';
import {formatDateTime} from '../../utils/dates';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField, type RichTextFieldHandle} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {NoteDeletionReasonFields, reasonFieldsComplete} from './NoteDeletionReasonFields';
import {NoteDeletionRequestDialog} from './NoteDeletionRequestDialog';

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

/**
 * Why notes can't be added now, as the stage section says why its stages can't be worked on: the record, the
 * pathway's status, or the person's roles (`canWork`: WorkPathways).
 */
export function whyNoNotes(pathway: Pathway, canWork = true): string {
  if (pathway.readOnlyReason) return pathway.readOnlyReason;
  if (!canWork) return "Your roles don't allow adding notes to pathways.";
  const status = pathway.status === 'Cancelled' ? 'cancelled' : pathway.status === 'Archived' ? 'archived' : null;
  return status ? `Notes can't be added or edited while the pathway is ${status}.` : "Notes can't be added or edited now.";
}

/** "k.ashford, 3 Nov 2026, 10:32": who wrote a note and when. */
export const noteByline = (note: PathwayNote) => `${note.authorUsername ?? ''}, ${formatDateTime(note.createdAtUtc ?? '')}`;

/**
 * Each note's buttons named for their note ("Delete note by k.ashford, 3 Nov 2026, 10:32"), so a screen reader user
 * moving through the buttons can tell one note's from another's. Each starts with the button's visible words (WCAG
 * 2.5.3).
 */
export const noteButtonName = {
  edit: (note: PathwayNote) => `Edit note by ${noteByline(note)}`,
  delete: (note: PathwayNote) => `Delete note by ${noteByline(note)}`,
  request: (note: PathwayNote) => `Request deletion of note by ${noteByline(note)}`,
  review: (note: PathwayNote) => `Review request for note by ${noteByline(note)}`,
};

export interface PathwayNotesProps {
  pathway: Pathway;
}

/**
 * A pathway's notes: Add Note (a rich text box; Ctrl+S adds it too), then every note, newest first, each with who
 * wrote it and when, "Edited" with when if it has been, and its text. Your own notes have Edit, while the pathway
 * takes notes (`canAddNotes`: not once it's cancelled or archived, nor while its patient's record is restricted or
 * anonymised). Nobody edits anyone else's note, admins included.
 *
 * Delete (`canDelete`: admins, in any status, but not while the record is restricted or anonymised) and Request
 * Deletion (`canRequestDeletion`: everyone else) follow the API's flags, which are for the signed-in user. Each asks
 * for a reason (a choice, with an optional detail) under the patient, as the patient banner shows them, and the note;
 * Delete sends the note's version, so one edited since is refused. Deleting is soft: the note is hidden here, but
 * kept. A note whose deletion has been asked for shows "Deletion requested", and an admin can open the request beside
 * it (Review Request, `NoteDeletionRequestDialog`) to decide it. Every note's buttons are named for their note.
 *
 * Saving goes through `usePathwayNoteActions`, which puts the note into the cached pathway at once. An edit sends
 * the version it started from (kept as Edit is pressed): if the note was changed somewhere else meanwhile (another
 * tab, or the other half of a half view), the API refuses it (409), and this says so beside the note, with Reload,
 * which reads the pathway afresh and closes the edit. While notes can't be added, it says why.
 */
export function PathwayNotes({pathway}: PathwayNotesProps) {
  const id = pathway.id ?? '';
  const actions = usePathwayNoteActions(id);
  const reload = useReloadPathway(id);
  const announce = useAnnounce();
  const canAdd = pathway.canAddNotes ?? false;
  const notes = pathway.noteList ?? [];
  // Deciding deletion requests is for holders of CorrectRecords.
  const canCorrectRecords = useCan('CorrectRecords');
  const canWork = useCan('WorkPathways');
  const deletion = useNoteDeletionActions(id);
  // The note whose deletion is being confirmed (an admin's Delete) or asked for (Request deletion).
  const [asking, setAsking] = useState<{note: PathwayNote; kind: 'delete' | 'request'} | null>(null);
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
        <AlertMessage label={noteId === null ? 'Note not added' : 'Note not saved'} message={problem.message} />
        {problem.isConflict && <Button label="Reload" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} onClick={doReload} />}
      </VStack>
    );

  return (
    <VStack gap={4} hAlign="stretch">
      {!canAdd && (
        <Text type="body" color="secondary">
          {whyNoNotes(pathway, canWork)}
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
              placeholder="Something the team should know about this pathway"
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
                    {/* The token and its button wrap as one, so the button never sits alone between the byline and the note. */}
                    {note.deletionRequestId && (
                      <HStack gap={1} vAlign="center">
                        <Token label="Deletion requested" size="sm" color="orange" />
                        {canCorrectRecords && (
                          <Button
                            label="Review Request"
                            aria-label={noteButtonName.review(note)}
                            tooltip="Review the request to delete this note"
                            variant="ghost"
                            size="sm"
                            onClick={() => setOpenRequestId(note.deletionRequestId ?? null)}
                          />
                        )}
                      </HStack>
                    )}
                  </HStack>
                }
                description={<RichText>{note.text ?? ''}</RichText>}
                endContent={
                  (note.canEdit || note.canDelete || note.canRequestDeletion) && (
                    <HStack gap={1} vAlign="center">
                      {note.canEdit && (
                        <Button
                          label="Edit"
                          aria-label={noteButtonName.edit(note)}
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
                      {note.canDelete && (
                        <Button
                          label="Delete"
                          aria-label={noteButtonName.delete(note)}
                          tooltip="Delete this note"
                          variant="ghost"
                          size="sm"
                          icon={<Icon icon={AppIcon.Delete} />}
                          onClick={() => setAsking({note, kind: 'delete'})}
                          isDisabled={isBusy || editing !== null}
                        />
                      )}
                      {note.canRequestDeletion && (
                        <Button
                          label="Request Deletion"
                          aria-label={noteButtonName.request(note)}
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
          patient={pathway.patient}
          kind={asking.kind}
          isProcessing={deletion.remove.isPending || deletion.request.isPending}
          onCancel={() => setAsking(null)}
          onConfirm={(input, fail) => {
            const noteId = asking.note.id ?? '';
            const done = {
              onSuccess: () => {
                setAsking(null);
                announce(asking.kind === 'delete' ? 'Note deleted' : 'Deletion requested. An admin will decide.');
              },
              onError: fail,
            };
            if (asking.kind === 'delete') deletion.remove.mutate({noteId, ...input, expectedVersion: asking.note.version ?? 1}, done);
            else deletion.request.mutate({noteId, ...input}, done);
          }}
        />
      )}
      <NoteDeletionRequestDialog requestId={openRequestId} pathwayId={id} onClose={() => setOpenRequestId(null)} />
    </VStack>
  );
}

/** Asking to delete a note: an admin's Delete, or staff's Request deletion. */
const REASON_DIALOG = {
  delete: {
    title: 'Delete Note',
    message:
      'Delete this note? It will be hidden from the pathway for everyone, but kept in the record\'s history and export, with your reason. Its author is told, and so is anyone who asked for it to be deleted.',
    confirmText: 'Delete Note',
  },
  request: {
    title: 'Request Note Deletion',
    message: 'Ask an admin to delete this note? Every admin is told, and one of them decides. Say why it should go.',
    confirmText: 'Request Deletion',
  },
} as const;

interface NoteDeletionReasonDialogProps {
  note: PathwayNote;
  /** Who the note is about, as the patient banner shows them (a dialog covers the page's own banner). */
  patient?: Pathway['patient'];
  kind: 'delete' | 'request';
  isProcessing: boolean;
  /** Sends it; `fail` shows what went wrong (a 400 on the reason or detail, or the API's words for a 409). */
  onConfirm: (input: {reason: NoteDeletionReason; detail: string}, fail: (error: unknown) => void) => void;
  onCancel: () => void;
}

/**
 * Confirms deleting a note, or asking for it to be: the patient (as the patient banner shows them), the note, and a
 * reason (a required choice, with an optional detail).
 */
function NoteDeletionReasonDialog({note, patient, kind, isProcessing, onConfirm, onCancel}: NoteDeletionReasonDialogProps) {
  const [reason, setReason] = useState<NoteDeletionReason | ''>('');
  const [detail, setDetail] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const words = REASON_DIALOG[kind];

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError && (error.errors.fields.reason?.length || error.errors.fields.detail?.length))
      setErrors(error.errors);
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
      confirmDisabled={!reasonFieldsComplete(reason, detail)}
      isProcessing={isProcessing}
      onConfirm={() => {
        if (!reason) return;
        setProblem(null);
        setErrors(null);
        onConfirm({reason, detail: detail.trim()}, fail);
      }}
      onCancel={onCancel}>
      {patient && <PatientBanner patient={patient} isCompact />}
      <VStack gap={1} hAlign="stretch">
        <Text type="body" weight="semibold">
          {noteByline(note)}
        </Text>
        <RichText>{note.text ?? ''}</RichText>
      </VStack>
      <NoteDeletionReasonFields
        reason={reason}
        onReasonChange={setReason}
        detail={detail}
        onDetailChange={setDetail}
        errors={errors}
        isDisabled={isProcessing}
      />
      {problem && <AlertMessage label={kind === 'delete' ? 'Note not deleted' : 'Request not sent'} message={problem} />}
    </ConfirmDialog>
  );
}

interface NoteEditorProps {
  note: PathwayNote;
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

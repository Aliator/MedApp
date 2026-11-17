import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {Card} from '@astryxdesign/core/Card';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {useAddProcedureNote} from '../../api/procedures';
import {useAnnounce} from '../../app/announcements';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {MAX_NOTE_LENGTH} from '../procedures/ProcedureNotes';
import type {OpenProcedure} from './NotepadContext';

/** What the notepad would add: the text selected in it, or all of it. */
export interface NoteToAdd {
  /** As Markdown, with its formatting. */
  text: string;
  /** Only the part selected, not the whole notepad. */
  isSelection: boolean;
  /**
   * Takes exactly this text out of the notepad, returning what's left (null if it can't now). Missing when it can't be
   * done exactly at all: "Remove it from the notepad" is then off, and says why.
   */
  remove?: () => string | null;
}

/** Why "Remove it from the notepad" is off, for a selection the notepad couldn't pin down exactly. */
export const CANT_REMOVE_SELECTION =
  "The selected text can't be taken out of the notepad exactly, so it won't be: remove it yourself after adding it.";

export interface AddToProcedureDialogProps {
  note: NoteToAdd;
  /** The procedure pages open: one, or two in a half view (it then asks which). */
  procedures: OpenProcedure[];
  /** Added (to this procedure); and whether to take it out of the notepad. */
  onAdded: (removeFromNotepad: boolean, procedureId: string) => void;
  onCancel: () => void;
}

/** "Left: Knee review" in a half view, or the title on its own. */
const where = (open: OpenProcedure) =>
  `${open.side === 'left' ? 'Left: ' : open.side === 'right' ? 'Right: ' : ''}${open.procedure.title ?? 'Procedure'}`;

/**
 * Adding the notepad's text (or the part selected) to a procedure as a new note, by you. Adding to the wrong patient
 * is a clinical hazard (H-37 in `docs/clinical-safety/hazard-log.md`), so it asks first, showing who the patient is as
 * the patient banner does (name, date of birth, NHS number), the procedure's title and the text. With two procedures
 * open (a half view), it asks which, and nothing is chosen until you choose. "Remove it from the notepad" starts
 * ticked, so patient details don't linger there, when the text can be taken out exactly; otherwise it's off and says
 * why.
 */
export function AddToProcedureDialog({note, procedures, onAdded, onCancel}: AddToProcedureDialogProps) {
  const add = useAddProcedureNote();
  const announce = useAnnounce();
  const [chosenKey, setChosenKey] = useState(procedures.length === 1 ? procedures[0].key : '');
  // Taken out of the notepad by default, so patient details don't linger there; but only when that can be done exactly.
  const canRemove = note.remove !== undefined;
  const [remove, setRemove] = useState(canRemove);
  const [problem, setProblem] = useState<string | null>(null);
  const chosen = procedures.find((p) => p.key === chosenKey);
  const procedure = chosen?.procedure;
  const isTooLong = note.text.length > MAX_NOTE_LENGTH;
  const cantTakeNotes = procedure !== undefined && !(procedure.canAddNotes ?? false);

  const confirm = () => {
    const procedureId = procedure?.id;
    if (!procedure || !procedureId) return;
    setProblem(null);
    add.mutate(
      {procedureId, text: note.text},
      {
        onSuccess: () => {
          announce(`Note added to ${procedure.title ?? 'the procedure'}`);
          onAdded(remove, procedureId);
        },
        onError: (e) => setProblem(problemMessage(e, 'The note could not be added.')),
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Add Note to Procedure"
      message={
        note.isSelection
          ? 'Add the text you selected in the notepad to this procedure as a new note, by you? Check it is the right patient.'
          : 'Add everything in the notepad to this procedure as a new note, by you? Check it is the right patient.'
      }
      icon={AppIcon.Notes}
      confirmText="Add Note"
      confirmDisabled={!procedure || cantTakeNotes || isTooLong}
      isProcessing={add.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {procedures.length > 1 && (
          <RadioList label="Which procedure?" value={chosenKey} onChange={setChosenKey}>
            {procedures.map((open) => (
              <RadioListItem
                key={open.key}
                value={open.key}
                label={where(open)}
                description={open.procedure.patientName ?? undefined}
              />
            ))}
          </RadioList>
        )}
        {procedure && (
          <VStack gap={3} hAlign="stretch">
            {procedure.patient && <PatientBanner patient={procedure.patient} isCompact />}
            <Text type="body">
              <Text type="body" weight="semibold">
                Procedure:
              </Text>{' '}
              {procedure.title}
            </Text>
            {cantTakeNotes && (
              <AlertMessage message={procedure.readOnlyReason ?? "This procedure can't take notes now (it's cancelled or archived)."} />
            )}
          </VStack>
        )}
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            The note
          </Text>
          <Card variant="muted" padding={3}>
            <RichText>{note.text}</RichText>
          </Card>
          {isTooLong && <AlertMessage message={`Too long for a note: at most ${MAX_NOTE_LENGTH} characters. Select a part of it.`} />}
        </VStack>
        <CheckboxInput
          label="Remove it from the notepad"
          value={remove}
          onChange={setRemove}
          isDisabled={add.isPending || !canRemove}
          description={canRemove ? undefined : CANT_REMOVE_SELECTION}
        />
        {problem && <AlertMessage message={problem} />}
      </VStack>
    </ConfirmDialog>
  );
}

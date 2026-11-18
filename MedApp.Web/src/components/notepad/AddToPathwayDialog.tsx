import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {Card} from '@astryxdesign/core/Card';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {useAddPathwayNote} from '../../api/pathways';
import {useAnnounce} from '../../app/announcements';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {MAX_NOTE_LENGTH} from '../pathways/PathwayNotes';
import type {OpenPathway} from './NotepadContext';

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

export interface AddToPathwayDialogProps {
  note: NoteToAdd;
  /** The pathway pages open: one, or two in a half view (it then asks which). */
  pathways: OpenPathway[];
  /** Added; and whether to take it out of the notepad. */
  onAdded: (removeFromNotepad: boolean) => void;
  onCancel: () => void;
}

/** "Left: Knee review" in a half view, or the title on its own. */
const where = (open: OpenPathway) =>
  `${open.side === 'left' ? 'Left: ' : open.side === 'right' ? 'Right: ' : ''}${open.pathway.title ?? 'Pathway'}`;

/**
 * Adding the notepad's text (or the part selected) to a pathway as a new note, by you. Adding to the wrong patient
 * is a clinical hazard (H-37 in `docs/clinical-safety/hazard-log.md`), so it asks first, showing who the patient is as
 * the patient banner does (name, date of birth, NHS number), the pathway's title and the text. With two pathways
 * open (a half view), it asks which, and nothing is chosen until you choose. "Remove it from the notepad" starts
 * ticked, so patient details don't linger there, when the text can be taken out exactly; otherwise it's off and says
 * why.
 */
export function AddToPathwayDialog({note, pathways, onAdded, onCancel}: AddToPathwayDialogProps) {
  const add = useAddPathwayNote();
  const announce = useAnnounce();
  const [chosenKey, setChosenKey] = useState(pathways.length === 1 ? pathways[0].key : '');
  // Taken out of the notepad by default, so patient details don't linger there; but only when that can be done exactly.
  const canRemove = note.remove !== undefined;
  const [remove, setRemove] = useState(canRemove);
  const [problem, setProblem] = useState<string | null>(null);
  const chosen = pathways.find((p) => p.key === chosenKey);
  const pathway = chosen?.pathway;
  const isTooLong = note.text.length > MAX_NOTE_LENGTH;
  const cantTakeNotes = pathway !== undefined && !(pathway.canAddNotes ?? false);

  const confirm = () => {
    if (!pathway?.id) return;
    setProblem(null);
    add.mutate(
      {pathwayId: pathway.id, text: note.text},
      {
        onSuccess: () => {
          announce(`Note added to ${pathway.title ?? 'the pathway'}`);
          onAdded(remove);
        },
        onError: (e) => setProblem(problemMessage(e, 'The note could not be added.')),
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Add Note to Pathway"
      message={
        note.isSelection
          ? 'Add the text you selected in the notepad to this pathway as a new note, by you? Check it is the right patient.'
          : 'Add everything in the notepad to this pathway as a new note, by you? Check it is the right patient.'
      }
      icon={AppIcon.Notes}
      confirmText="Add Note"
      confirmDisabled={!pathway || cantTakeNotes || isTooLong}
      isProcessing={add.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {pathways.length > 1 && (
          <RadioList label="Which pathway?" value={chosenKey} onChange={setChosenKey}>
            {pathways.map((open) => (
              <RadioListItem
                key={open.key}
                value={open.key}
                label={where(open)}
                description={open.pathway.patientName ?? undefined}
              />
            ))}
          </RadioList>
        )}
        {pathway && (
          <VStack gap={3} hAlign="stretch">
            {pathway.patient && <PatientBanner patient={pathway.patient} isCompact />}
            <Text type="body">
              <Text type="body" weight="semibold">
                Pathway:
              </Text>{' '}
              {pathway.title}
            </Text>
            {cantTakeNotes && (
              <AlertMessage message={pathway.readOnlyReason ?? "This pathway can't take notes now (it's cancelled or archived)."} />
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

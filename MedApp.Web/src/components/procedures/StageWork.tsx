import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import type {ProcedureStage} from '../../api/procedures';
import {FillingTextArea} from '../forms/FillingTextArea';
import {AppIcon} from '../shared/icons';
import {StageNotes} from './StageList';

export interface StageWorkProps {
  stage: ProcedureStage;
  /** Shows Unlock on a finished, locked stage. */
  isAdmin: boolean;
  /** Something is being saved: the buttons wait. */
  isBusy: boolean;
  onStart: () => void;
  onSaveNotes: (notes: string) => void;
  /** Finish the stage, with the notes as they are in the box (saved first if changed). */
  onFinish: (notes: string) => void;
  onUnlock: () => void;
  /**
   * The notes box grows to fill the free space of the area that scrolls around it (at least two lines), rather
   * than a fixed five. For where this stage is the main thing on a card, not for stages in a list.
   */
  isNotesFilling?: boolean;
}

/**
 * The working part of a stage. While it can be edited (it's the current stage, or an admin unlocked it): a notes
 * box with Save Notes and Finish Stage, and Start Stage before work begins. Otherwise its notes, read-only, and
 * for an admin, Unlock Stage on a finished stage.
 *
 * The notes box starts from the saved notes; give it a `key` that changes with them, so it resets after a save.
 */
export function StageWork({stage, isAdmin, isBusy, onStart, onSaveNotes, onFinish, onUnlock, isNotesFilling = false}: StageWorkProps) {
  const saved = stage.notes ?? '';
  const [draft, setDraft] = useState(saved);
  const isChanged = draft !== saved;

  if (!stage.canEdit) {
    return (
      <VStack gap={3}>
        <StageNotes notes={saved} />
        {isAdmin && stage.canUnlock && (
          <HStack>
            <Button
              label="Unlock Stage"
              variant="secondary"
              icon={<Icon icon={AppIcon.Lock} />}
              onClick={onUnlock}
              isDisabled={isBusy}
            />
          </HStack>
        )}
      </VStack>
    );
  }

  const notesProps = {
    label: 'Notes',
    value: draft,
    onChange: setDraft,
    placeholder: 'What was done, found or agreed at this stage',
    isDisabled: isBusy,
  };

  return (
    <VStack gap={3}>
      {isNotesFilling ? <FillingTextArea {...notesProps} /> : <TextArea {...notesProps} rows={5} />}
      <HStack gap={2} vAlign="center">
        {isChanged && (
          <Text type="supporting" color="secondary">
            Unsaved changes
          </Text>
        )}
        <StackItem size="fill" />
        {stage.state === 'NotStarted' && <Button label="Start Stage" variant="ghost" onClick={onStart} isDisabled={isBusy} />}
        <Button
          label="Save Notes"
          variant="secondary"
          icon={<Icon icon={AppIcon.Save} />}
          onClick={() => onSaveNotes(draft)}
          isDisabled={!isChanged || isBusy}
        />
        <Button
          label="Finish Stage"
          variant="primary"
          icon={<Icon icon={AppIcon.Check} />}
          onClick={() => onFinish(draft)}
          isDisabled={isBusy}
        />
      </HStack>
    </VStack>
  );
}

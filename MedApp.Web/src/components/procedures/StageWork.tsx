import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useLayoutEffect, useRef, useState} from 'react';
import type {ProcedureStage} from '../../api/procedures';
import {NO_STATUS_LINE} from '../forms/fieldSpace';
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
  /** The notes box's height in pixels, once fitted (five lines until then). */
  notesHeight?: number;
  /**
   * Fits the notes box to its place once: with no `notesHeight` yet, it measures the free space in the area that
   * scrolls around it, as it first shows, and reports the height that fills it exactly, without making that area
   * scroll (its five-line height, when there's no room to grow). The page keeps the answer and passes it back as
   * `notesHeight`, so the box keeps that size afterwards, whatever else changes.
   */
  onFitNotes?: (height: number) => void;
}

/** The notes box's usual size, in lines, and the smallest a fitted one is: fitting only ever grows it. */
const DEFAULT_ROWS = 5;

/** The nearest ancestor that scrolls up and down: the space the box has to fit in. */
function scrollingParent(element: HTMLElement): HTMLElement | undefined {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const {overflowY} = getComputedStyle(parent);
    if (overflowY === 'auto' || overflowY === 'scroll') return parent;
  }
  return undefined;
}

/** How tall the scrolling area's content is, whether or not it overflows. */
function contentHeight(scroller: HTMLElement) {
  const style = getComputedStyle(scroller);
  let height = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
  for (const child of scroller.children) height += (child as HTMLElement).offsetHeight;
  return height;
}

/**
 * The height that fills the free space around the box exactly (to the pixel, not in whole lines, so no gap is left
 * under it), from its height now: never less than its usual five lines.
 */
function fittedHeight(box: HTMLTextAreaElement): number | undefined {
  const scroller = scrollingParent(box);
  // Nothing laid out (e.g. hidden, or in tests): no answer, so it keeps its size.
  if (!scroller || !box.offsetHeight || !scroller.clientHeight) return undefined;
  const spare = Math.floor(scroller.clientHeight - contentHeight(scroller));
  return box.offsetHeight + Math.max(0, spare);
}

/**
 * The working part of a stage. While it can be edited (it's the current stage, or an admin unlocked it): a notes
 * box with Save Notes and Finish Stage, and Start Stage before work begins. Otherwise its notes, read-only, and
 * for an admin, Unlock Stage on a finished stage.
 *
 * The notes box starts from the saved notes; give it a `key` that changes with them, so it resets after a save.
 */
export function StageWork({stage, isAdmin, isBusy, onStart, onSaveNotes, onFinish, onUnlock, notesHeight, onFitNotes}: StageWorkProps) {
  const saved = stage.notes ?? '';
  const [draft, setDraft] = useState(saved);
  const isChanged = draft !== saved;
  const box = useRef<HTMLTextAreaElement>(null);

  // Once, as the box first shows (the next frame, when the page around it is laid out), if it's to be fitted.
  useLayoutEffect(() => {
    if (!onFitNotes || notesHeight !== undefined) return;
    const frame = requestAnimationFrame(() => {
      const height = box.current && fittedHeight(box.current);
      if (height !== undefined && height !== null) onFitNotes(height);
    });
    return () => cancelAnimationFrame(frame);
    // Only when it first shows: a later change (a resize, a section folding) leaves it as it is.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // The fitted height, on the box itself (TextArea sizes it only in whole lines). A box someone has dragged to
  // another size keeps theirs until the page opens again.
  useLayoutEffect(() => {
    if (box.current && notesHeight !== undefined) box.current.style.height = `${notesHeight}px`;
  }, [notesHeight]);

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
    // A failed save shows in the page's header, never on the box, so it needs no error line.
    containerStyle: NO_STATUS_LINE,
  };

  return (
    <VStack gap={3}>
      <TextArea {...notesProps} ref={box} rows={DEFAULT_ROWS} />
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

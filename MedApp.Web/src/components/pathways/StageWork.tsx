import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useLayoutEffect, useRef, useState, type ReactNode} from 'react';
import type {PathwayStage} from '../../api/pathways';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';
import {RichTextField} from '../forms/RichTextField';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {AppIcon} from '../shared/icons';
import {StageNotes} from './StageList';

export interface StageWorkProps {
  stage: PathwayStage;
  /** Shows Unlock on a finished, locked stage: the person's roles grant UnlockStages. */
  canUnlock: boolean;
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
  /** The stage's Visit part (`StageVisit`), above its notes, for a stage that involves a visit. */
  visit?: ReactNode;
  /** Why the stage can't be finished yet (e.g. "Needs an attended visit first"): Finish is disabled, with this beside it. */
  finishDisabledReason?: string;
  /** Skip an optional stage, with a reason: offered while the API says it can be (`canSkip`). */
  onSkip?: () => void;
  /** Reinstate a skipped stage, with a reason: offered while the API says the viewer can (`canReinstate`). */
  onReinstate?: () => void;
}

/** The notes box's usual height (five lines), and the least a fitted one has: fitting only ever grows it. */
const DEFAULT_HEIGHT = '7.5rem';

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
 * The height of the box's writing area that fills the free space around it exactly (to the pixel, so no gap is
 * left under it), from its height now: never less than its usual five lines.
 */
function fittedHeight(box: HTMLElement): number | undefined {
  const surface = box.querySelector<HTMLElement>('[contenteditable]');
  const scroller = scrollingParent(box);
  // Nothing laid out (e.g. hidden, or in tests): no answer, so it keeps its size.
  if (!surface || !scroller || !surface.offsetHeight || !scroller.clientHeight) return undefined;
  const spare = Math.floor(scroller.clientHeight - contentHeight(scroller));
  return surface.offsetHeight + Math.max(0, spare);
}

/**
 * The working part of a stage. While it can be edited (it's the current stage, or an admin unlocked it): its visit,
 * if it involves one, then a notes box with Save Notes and Finish Stage, and Start Stage before work begins.
 * Otherwise its notes, read-only, and for a holder of UnlockStages, Unlock Stage on a finished stage. Skip Stage
 * shows on an optional current stage, and Reinstate Stage on a skipped one, as the API's flags allow.
 *
 * The notes box starts from the saved notes; give it a `key` that changes with them, so it resets after a save.
 * Ctrl+S (⌘S on a Mac) in it saves the notes.
 */
export function StageWork({
  stage,
  canUnlock,
  isBusy,
  onStart,
  onSaveNotes,
  onFinish,
  onUnlock,
  notesHeight,
  onFitNotes,
  visit,
  finishDisabledReason,
  onSkip,
  onReinstate,
}: StageWorkProps) {
  const saved = stage.notes ?? '';
  const [draft, setDraft] = useState(saved);
  const isChanged = draft !== saved;
  const box = useRef<HTMLDivElement>(null);
  const work = useRef<HTMLDivElement>(null);
  // Ctrl+S saves the notes, as Save Notes does (only when they've changed, and not while saving).
  useSaveShortcut(
    work,
    stage.canEdit
      ? () => {
          // Busy saving already; or nothing changed, which Ctrl+S then says.
          if (isBusy) return;
          if (!isChanged) return false;
          onSaveNotes(draft);
        }
      : undefined,
  );

  // Once, as the box first shows, if it's to be fitted: once the page around it is laid out and the editor has drawn
  // its writing area (a frame or two; it gives up after a few, keeping its size).
  useLayoutEffect(() => {
    if (!onFitNotes || notesHeight !== undefined) return;
    let frame = 0;
    let tries = 0;
    const fit = () => {
      const height = box.current && fittedHeight(box.current);
      if (height !== undefined && height !== null) onFitNotes(height);
      else if (tries++ < 10) frame = requestAnimationFrame(fit);
    };
    frame = requestAnimationFrame(fit);
    return () => cancelAnimationFrame(frame);
    // Only when it first shows: a later change (a resize, a section folding) leaves it as it is.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // An optional stage can be skipped while it's current, whether or not work on it has begun.
  const skip = onSkip && stage.canSkip && (
    <Button label="Skip Stage" variant="ghost" icon={<Icon icon={AppIcon.Skip} />} onClick={onSkip} isDisabled={isBusy} />
  );

  if (!stage.canEdit) {
    const reinstate = onReinstate && stage.canReinstate && (
      <Button label="Reinstate Stage" variant="secondary" icon={<Icon icon={AppIcon.Restore} />} onClick={onReinstate} isDisabled={isBusy} />
    );
    return (
      <VStack gap={3}>
        <StageNotes notes={saved} />
        {((canUnlock && stage.canUnlock) || skip || reinstate) && (
          <HStack gap={2} wrap="wrap">
            {canUnlock && stage.canUnlock && (
              <Button
                label="Unlock Stage"
                variant="secondary"
                icon={<Icon icon={AppIcon.Lock} />}
                onClick={onUnlock}
                isDisabled={isBusy}
              />
            )}
            {reinstate}
            {skip}
          </HStack>
        )}
      </VStack>
    );
  }

  return (
    <VStack ref={work} gap={3}>
      {visit}
      {/* A failed save shows in the page's header, never on the box, so it has no error line. Its text grows the
          box past its height. */}
      <VStack gap={0} ref={box} style={NO_FIELD_STATUS_LINE}>
        <RichTextField
          label="Notes"
          value={saved}
          onChange={setDraft}
          placeholder="What was done, found or agreed at this stage"
          isDisabled={isBusy}
          minHeight={notesHeight ?? DEFAULT_HEIGHT}
        />
      </VStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        {isChanged && (
          <Text type="supporting" color="secondary">
            Unsaved changes
          </Text>
        )}
        <StackItem size="fill" />
        {finishDisabledReason && (
          <Text type="supporting" color="secondary">
            {finishDisabledReason}
          </Text>
        )}
        {skip}
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
          isDisabled={isBusy || finishDisabledReason !== undefined}
          // Also describes the button, so the reason is heard on it too.
          tooltip={finishDisabledReason}
        />
      </HStack>
    </VStack>
  );
}

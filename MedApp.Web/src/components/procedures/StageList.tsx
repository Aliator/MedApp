import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import type {ReactNode} from 'react';
import type {ProcedureStage} from '../../api/procedures';
import {formatDateTime} from '../../utils/dates';

/** What a stage's state looks like: finished, under way or waiting. */
export function StageStateTokens({stage}: {stage: ProcedureStage}) {
  return (
    <HStack gap={2} vAlign="center">
      {stage.state === 'Completed' ? (
        <Token size="sm" label="Finished" color="green" />
      ) : stage.state === 'InProgress' ? (
        <Token size="sm" label="In progress" color="teal" />
      ) : (
        <Token size="sm" label="Not started" color="gray" />
      )}
      {stage.isCurrent && <Token size="sm" label="Current stage" color="blue" />}
      {stage.state === 'Completed' && !stage.isLocked && <Token size="sm" label="Unlocked for editing" color="orange" />}
    </HStack>
  );
}

/** Who started and finished the stage, and when. */
function StageActivity({stage}: {stage: ProcedureStage}) {
  const lines = [
    stage.startedAtUtc && `Started by ${stage.startedByUsername ?? 'unknown'}, ${formatDateTime(stage.startedAtUtc)}`,
    stage.completedAtUtc && `Finished by ${stage.completedByUsername ?? 'unknown'}, ${formatDateTime(stage.completedAtUtc)}`,
  ].filter(Boolean) as string[];

  if (lines.length === 0) return null;
  return (
    <VStack gap={0}>
      {lines.map((line) => (
        <Text key={line} type="supporting" color="secondary">
          {line}
        </Text>
      ))}
    </VStack>
  );
}

/** The stage's notes, a paragraph per line, or a note that there are none. */
export function StageNotes({notes}: {notes: string}) {
  const lines = notes.split('\n');
  return (
    <VStack gap={1}>
      <Text type="body" weight="semibold">
        Notes
      </Text>
      {notes.trim() ? (
        lines.map((line, i) => (
          <Text key={i} type="body">
            {line || '\u00a0'}
          </Text>
        ))
      ) : (
        <Text type="body" color="secondary">
          No notes yet.
        </Text>
      )}
    </VStack>
  );
}

/** Whether a stage's instructions are open, held by the page; without it, they start folded and open on their own. */
export interface InstructionsFolding {
  isOpen: (stage: ProcedureStage) => boolean;
  onOpenChange: (stage: ProcedureStage, isOpen: boolean) => void;
}

/** The stage's instructions, folded under their heading until it's clicked. */
function StageInstructions({stage, folding}: {stage: ProcedureStage; folding?: InstructionsFolding}) {
  return (
    <Collapsible
      defaultIsOpen={false}
      isOpen={folding?.isOpen(stage)}
      onOpenChange={folding ? (isOpen) => folding.onOpenChange(stage, isOpen) : undefined}
      chevronPosition="end"
      trigger={
        <Text type="body" weight="semibold">
          Instructions
        </Text>
      }>
      <VStack gap={0} paddingBlockStart={1}>
        <Text type="body">{stage.instructions}</Text>
      </VStack>
    </Collapsible>
  );
}

/** A stage's title: its number and name, as headers and the current stage show it. */
export function StageTitle({stage}: {stage: ProcedureStage}) {
  return (
    <HStack gap={3} vAlign="center">
      <Text type="body" size="lg" weight="bold">{`${stage.stageOrder}. ${stage.name}`}</Text>
      <StageStateTokens stage={stage} />
    </HStack>
  );
}

export interface StageDetailsProps {
  stage: ProcedureStage;
  /** Replaces the read-only notes, e.g. with a notes box and the stage's actions. */
  work?: ReactNode;
  instructions?: InstructionsFolding;
}

/**
 * Everything about one stage under its title: description, who started and finished it, instructions (which fold)
 * and notes.
 */
export function StageDetails({stage, work, instructions}: StageDetailsProps) {
  return (
    <VStack gap={3}>
      {stage.description && (
        <Text type="body" color="secondary">
          {stage.description}
        </Text>
      )}
      <StageActivity stage={stage} />
      {stage.instructions && <StageInstructions stage={stage} folding={instructions} />}
      {work ?? <StageNotes notes={stage.notes ?? ''} />}
    </VStack>
  );
}

export interface StageListProps {
  stages: readonly ProcedureStage[];
  /**
   * Open the current stage to begin with (the default). Pass false where the current stage is already shown in
   * full elsewhere on the page, so every stage starts closed.
   */
  isCurrentOpen?: boolean;
  /** What to show in place of a stage's read-only notes, e.g. its notes box and actions; nothing keeps them read-only. */
  renderWork?: (stage: ProcedureStage) => ReactNode;
  /**
   * Which stages are open, held by the page (with `onStageOpenChange`), e.g. so they stay open or closed when the
   * page changes layout. Without it, each stage keeps its own state and `isCurrentOpen` sets where it starts.
   */
  openStageIds?: readonly string[];
  onStageOpenChange?: (stageId: string, isOpen: boolean) => void;
  /** Whether each stage's instructions are open, held by the page. Without it, they start folded. */
  instructions?: InstructionsFolding;
}

/**
 * A procedure's stages in order, read-only. Each stage folds: its header shows its number, name and state, and
 * opening it shows who started and finished it, its instructions and its notes. The current stage starts open
 * (unless `isCurrentOpen` is false) and the others closed, so the page leads with the work in hand.
 */
export function StageList({stages, isCurrentOpen = true, renderWork, openStageIds, onStageOpenChange, instructions}: StageListProps) {
  return (
    <VStack gap={0}>
      {[...stages]
        .sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0))
        .map((stage, index) => (
          <Section key={stage.id} variant="transparent" paddingBlock={4} paddingInline={6} dividers={index > 0 ? ['top'] : []}>
            <Collapsible
              defaultIsOpen={isCurrentOpen && (stage.isCurrent ?? false)}
              isOpen={openStageIds ? openStageIds.includes(stage.id ?? '') : undefined}
              onOpenChange={openStageIds ? (isOpen) => onStageOpenChange?.(stage.id ?? '', isOpen) : undefined}
              chevronPosition="end"
              trigger={<StageTitle stage={stage} />}>
              <StageDetails stage={stage} work={renderWork?.(stage)} instructions={instructions} />
            </Collapsible>
          </Section>
        ))}
    </VStack>
  );
}

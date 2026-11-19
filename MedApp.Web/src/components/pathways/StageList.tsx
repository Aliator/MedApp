import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import type {ReactNode} from 'react';
import type {Schemas} from '../../api/client';
import {needsLetterWords} from '../../api/letters';
import {choiceKindLabel, stageDepths, waitsForWords} from '../../api/pathwayRoutes';
import type {PathwayStage} from '../../api/pathways';
import {skipReasonLabel} from '../../api/stageSkips';
import {formatDateTime} from '../../utils/dates';
import {RichText} from '../shared/RichText';
import {SnomedConcept} from './SnomedConcept';

/** A stage that was skipped: an optional one (or, before choice nodes, one in a branch the clinician didn't choose). */
export const isSkipped = (stage: PathwayStage) => stage.state === 'Skipped';

/**
 * What a stage's state looks like: finished, skipped (and why), under way or waiting; and whether it's optional, or a
 * choice node (its kind, and the outcome recorded).
 */
export function StageStateTokens({stage}: {stage: PathwayStage}) {
  return (
    <HStack gap={2} vAlign="center" wrap="wrap">
      {stage.state === 'Completed' ? (
        <Token size="sm" label="Finished" color="green" />
      ) : isSkipped(stage) ? (
        <Token size="sm" label={`Skipped: ${skipReasonLabel(stage.skipReason) || 'no reason given'}`} color="gray" />
      ) : stage.state === 'InProgress' ? (
        <Token size="sm" label="In progress" color="teal" />
      ) : (
        <Token size="sm" label="Not started" color="gray" />
      )}
      {stage.isCurrent && <Token size="sm" label="Current stage" color="blue" />}
      {stage.state === 'Completed' && !stage.isLocked && <Token size="sm" label="Unlocked for editing" color="orange" />}
      {stage.isOptional && <Token size="sm" label="Optional" color="gray" />}
      {stage.choiceKind && <Token size="sm" label={`Choice: ${choiceKindLabel(stage.choiceKind).toLowerCase()}`} color="purple" />}
      {stage.outcome && <Token size="sm" label={`Outcome: ${stage.outcome}`} color="purple" />}
      {/* The relay: a stage another team works (the lab's), named on it wherever the pathway is seen. */}
      {stage.ownerTeamName && (
        <Token size="sm" label={`Worked by ${stage.ownerTeamName}`} color="orange" />
      )}
      {stage.isCurrent && stage.handedOverFromTeamName && !stage.pickedUpAtUtc && <Token size="sm" label="Not picked up yet" color="red" />}
    </HStack>
  );
}

/** Who started, finished or skipped the stage, and when; a skip's reason in words, and its detail. */
function StageActivity({stage}: {stage: PathwayStage}) {
  const lines = [
    stage.handedOverFromTeamName &&
      stage.reachedAtUtc &&
      `Handed over by ${stage.handedOverFromTeamName}, ${formatDateTime(stage.reachedAtUtc)}`,
    stage.pickedUpAtUtc && `Picked up by ${stage.pickedUpByUsername ?? 'unknown'}, ${formatDateTime(stage.pickedUpAtUtc)}`,
    stage.startedAtUtc && `Started by ${stage.startedByUsername ?? 'unknown'}, ${formatDateTime(stage.startedAtUtc)}`,
    stage.completedAtUtc && `Finished by ${stage.completedByUsername ?? 'unknown'}, ${formatDateTime(stage.completedAtUtc)}`,
    stage.skippedAtUtc &&
      `Skipped by ${stage.skippedByUsername ?? 'unknown'}, ${formatDateTime(stage.skippedAtUtc)}: ${skipReasonLabel(stage.skipReason) || 'no reason given'}`,
    stage.skippedAtUtc && stage.skipDetail?.trim() && `Detail: ${stage.skipDetail.trim()}`,
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

/** The stage's notes, formatted (`RichText`), or a note that there are none. */
export function StageNotes({notes}: {notes: string}) {
  return (
    <VStack gap={1}>
      <Text type="body" weight="semibold">
        Notes
      </Text>
      {notes.trim() ? (
        <RichText>{notes}</RichText>
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
  isOpen: (stage: PathwayStage) => boolean;
  onOpenChange: (stage: PathwayStage, isOpen: boolean) => void;
}

/** The stage's instructions, folded under their heading until it's clicked. */
function StageInstructions({stage, folding}: {stage: PathwayStage; folding?: InstructionsFolding}) {
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
        <RichText>{stage.instructions ?? ''}</RichText>
      </VStack>
    </Collapsible>
  );
}

/**
 * A stage's title: its number and name, as headers and the current stage show it, and its state; `extra` adds more
 * after them (its visit, `StageVisitTokens`). It wraps in a narrow space.
 */
export function StageTitle({stage, extra}: {stage: PathwayStage; extra?: ReactNode}) {
  return (
    <HStack gap={3} vAlign="center" wrap="wrap">
      {/* A skipped stage is struck through, as well as saying so in its state, so it reads as not done at a glance. */}
      <Text type="body" size="lg" weight="bold" hasStrikethrough={isSkipped(stage)}>{`${stage.stageOrder}. ${stage.name}`}</Text>
      <StageStateTokens stage={stage} />
      {extra}
    </HStack>
  );
}

export interface StageDetailsProps {
  stage: PathwayStage;
  /** Replaces the read-only notes, e.g. with a notes box and the stage's actions. */
  work?: ReactNode;
  instructions?: InstructionsFolding;
  /** The stage's files, under its notes (e.g. an `AttachmentsFold`). */
  files?: ReactNode;
  /** A join's words (`waitsForWords`): the stages before it that must each be finished or skipped first. */
  waitsFor?: string | null;
  /** The lab's results on the stages straight before it (`resultsBefore`), for a lab result choice node to show. */
  resultsBefore?: readonly PathwayStage[];
}

/** The stages straight before a stage that carry the lab's result: what its lab result choice node is recorded from. */
export const resultsBefore = (stage: PathwayStage, stages: readonly PathwayStage[]) =>
  stages.filter((s) => s.result?.text && stage.predecessorIds?.includes(s.id ?? ''));

/** A choice node's line: its kind and the outcomes the clinician records one of ("Records one of: Benign or Malignant"). */
export function choiceWords(stage: PathwayStage): string | null {
  if (!stage.choiceKind) return null;
  if (stage.outcome) return `${choiceKindLabel(stage.choiceKind)}: the outcome recorded was ${stage.outcome}.`;
  const names = (stage.outcomes ?? []).map((o) => o.name ?? '');
  const either = names.length < 2 ? (names[0] ?? '') : `${names.slice(0, -1).join(', ')} or ${names[names.length - 1]}`;
  return `${choiceKindLabel(stage.choiceKind)}: finishing it records one outcome (${either}), and that outcome's stages are added.`;
}

/** The lab's result on a stage: who entered it and when, then its text as written. */
export function StageResult({stage, title}: {stage: PathwayStage; title?: string}) {
  if (!stage.result?.text) return null;
  return (
    <VStack gap={1}>
      <Text type="body" weight="semibold">
        {title ?? 'Result'}
      </Text>
      <Text type="supporting" color="secondary">
        {`Entered by ${stage.result.enteredByUsername ?? 'unknown'}${stage.result.enteredAtUtc ? `, ${formatDateTime(stage.result.enteredAtUtc)}` : ''}`}
      </Text>
      <Text type="body">{stage.result.text}</Text>
    </VStack>
  );
}

/** What a stage that waits for results says, under its title. */
export const WAITS_FOR_RESULTS_WORDS = 'Waits for results: every result on the pathway must be signed off before it can be finished.';

/**
 * Everything about one stage under its title: description, its SNOMED CT concept (copied from its definition when
 * the pathway was made), who started and finished it, instructions (which fold), notes and files.
 */
export function StageDetails({stage, work, instructions, files, waitsFor, resultsBefore: before = []}: StageDetailsProps) {
  return (
    <VStack gap={3}>
      {stage.description && (
        <Text type="body" color="secondary">
          {stage.description}
        </Text>
      )}
      <SnomedConcept code={stage.snomedCode} term={stage.snomedTerm} />
      {/* A stage that waits for a letter (decision 105): it can't be finished until one of that kind is approved and sent. */}
      {needsLetterWords(stage.needsLetterKind) && (
        <Text type="body" weight="semibold">
          {needsLetterWords(stage.needsLetterKind)}
        </Text>
      )}
      {/* A stage that waits for results (decision 97): it can't be finished while any of the pathway's results awaits sign-off. */}
      {stage.waitsForResults && (
        <Text type="body" weight="semibold">
          {WAITS_FOR_RESULTS_WORDS}
        </Text>
      )}
      {/* A stage that needs consent (decision 146): it can't be finished until a Valid consent record is recorded for it. */}
      {stage.needsConsent && (
        <Text type="body" weight="semibold">
          Needs consent recorded before it can be finished
        </Text>
      )}
      {/* A join (MC8): it starts only when every route into it is finished or skipped. */}
      {waitsFor && (
        <Text type="body" color="secondary">
          {waitsFor}
        </Text>
      )}
      {/* A choice node (an assessment or a lab result): the clinician records its outcome; MedApp never chooses one. */}
      {choiceWords(stage) && (
        <Text type="body" weight="semibold">
          {choiceWords(stage)}
        </Text>
      )}
      {before.map((s) => (
        <StageResult key={s.id} stage={s} title={`Result from ${s.name}`} />
      ))}
      <StageResult stage={stage} />
      <StageActivity stage={stage} />
      {stage.instructions && <StageInstructions stage={stage} folding={instructions} />}
      {work ?? <StageNotes notes={stage.notes ?? ''} />}
      {files}
    </VStack>
  );
}

export interface StageListProps {
  stages: readonly PathwayStage[];
  /**
   * Open the current stage to begin with (the default). Pass false where the current stage is already shown in
   * full elsewhere on the page, so every stage starts closed.
   */
  isCurrentOpen?: boolean;
  /** What to show in place of a stage's read-only notes, e.g. its notes box and actions; nothing keeps them read-only. */
  renderWork?: (stage: PathwayStage) => ReactNode;
  /**
   * Which stages are open, held by the page (with `onStageOpenChange`), e.g. so they stay open or closed when the
   * page changes layout. Without it, each stage keeps its own state and `isCurrentOpen` sets where it starts.
   */
  openStageIds?: readonly string[];
  onStageOpenChange?: (stageId: string, isOpen: boolean) => void;
  /** Whether each stage's instructions are open, held by the page. Without it, they start folded. */
  instructions?: InstructionsFolding;
  /** More for a stage's title, after its state, e.g. its visit (`StageVisitTokens`). */
  renderVisit?: (stage: PathwayStage) => ReactNode;
  /** Each stage's files, under its notes; nothing shows none. */
  renderFiles?: (stage: PathwayStage) => ReactNode;
  /** The branches added to the pathway on new information (MC10), so their stages say so, and the first says why. */
  branchAdditions?: readonly PathwayBranchAddition[];
}

/** A branch added to a pathway on new information (MC10). */
export type PathwayBranchAddition = Schemas['PatientPathwayBranchAdditionResponse'];

/** What a stage added with a branch says under its title: which branch, and on its first stage, who added it, when and why. */
export function addedWords(stage: PathwayStage, additions: readonly PathwayBranchAddition[]): string | null {
  const addition = additions.find((a) => a.id === stage.branchAdditionId);
  if (!addition) return null;
  if (addition.stageIds?.[0] !== stage.id) return `Added with the ${addition.name} branch.`;
  const when = addition.addedAtUtc ? ` on ${formatDateTime(addition.addedAtUtc)}` : '';
  return `Added with the ${addition.name} branch by ${addition.addedByUsername ?? 'unknown'}${when}: ${addition.reason}`;
}

/** Indents a stage on a route after a fork or branch point, one step for each (the tree view's outline, MC9). */
const indentFor = (depth: number) =>
  depth > 0 ? {paddingInlineStart: `calc(var(--spacing-6) + ${depth} * var(--spacing-8))`} : undefined;

/**
 * A pathway's stages in order (a topological order: routes side by side are listed one after the other), read-only. Each stage folds: its header shows its number, name and state, and
 * opening it shows who started and finished it, its instructions and its notes. The current stage starts open
 * (unless `isCurrentOpen` is false) and the others closed, so the page leads with the work in hand.
 *
 * The tree view (MC9): an indented outline, each stage on a route after a fork or branch point indented under it and
 * back out at the join, with its status; a stage added with a branch (MC10) says so.
 */
export function StageList({
  stages,
  isCurrentOpen = true,
  renderWork,
  openStageIds,
  onStageOpenChange,
  instructions,
  renderVisit,
  renderFiles,
  branchAdditions = [],
}: StageListProps) {
  const depths = stageDepths(stages);
  return (
    <VStack gap={0}>
      {[...stages]
        .sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0))
        .map((stage, index) => (
          <Section
            key={stage.id}
            variant="transparent"
            paddingBlock={4}
            paddingInline={6}
            dividers={index > 0 ? ['top'] : []}
            style={indentFor(depths.get(stage.id ?? '') ?? 0)}>
            <Collapsible
              defaultIsOpen={isCurrentOpen && (stage.isCurrent ?? false)}
              isOpen={openStageIds ? openStageIds.includes(stage.id ?? '') : undefined}
              onOpenChange={openStageIds ? (isOpen) => onStageOpenChange?.(stage.id ?? '', isOpen) : undefined}
              chevronPosition="end"
              trigger={
                <StageTitle
                  stage={stage}
                  extra={
                    <>
                      {stage.branchAdditionId && <Token size="sm" label="Added branch" color="purple" />}
                      {renderVisit?.(stage)}
                    </>
                  }
                />
              }>
              <StageDetails
                stage={stage}
                work={renderWork?.(stage)}
                instructions={instructions}
                files={renderFiles?.(stage)}
                waitsFor={[addedWords(stage, branchAdditions), waitsForWords(stage, stages)].filter(Boolean).join(' ') || null}
                resultsBefore={stage.choiceKind === 'LabResult' ? resultsBefore(stage, stages) : []}
              />
            </Collapsible>
          </Section>
        ))}
    </VStack>
  );
}

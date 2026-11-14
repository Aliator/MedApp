import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
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
function StageNotes({notes}: {notes: string}) {
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

/** The stage's instructions, always shown while the stage is open. */
function StageInstructions({instructions}: {instructions: string}) {
  return (
    <VStack gap={1}>
      <Text type="body" weight="semibold">
        Instructions
      </Text>
      <Text type="body">{instructions}</Text>
    </VStack>
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

/** Everything about one stage under its title: description, who started and finished it, instructions and notes. */
export function StageDetails({stage}: {stage: ProcedureStage}) {
  return (
    <VStack gap={3}>
      {stage.description && (
        <Text type="body" color="secondary">
          {stage.description}
        </Text>
      )}
      <StageActivity stage={stage} />
      {stage.instructions && <StageInstructions instructions={stage.instructions} />}
      <StageNotes notes={stage.notes ?? ''} />
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
}

/**
 * A procedure's stages in order, read-only. Each stage folds: its header shows its number, name and state, and
 * opening it shows who started and finished it, its instructions and its notes. The current stage starts open
 * (unless `isCurrentOpen` is false) and the others closed, so the page leads with the work in hand.
 */
export function StageList({stages, isCurrentOpen = true}: StageListProps) {
  return (
    <VStack gap={0}>
      {[...stages]
        .sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0))
        .map((stage, index) => (
          <Section key={stage.id} variant="transparent" paddingBlock={4} paddingInline={6} dividers={index > 0 ? ['top'] : []}>
            <Collapsible
              defaultIsOpen={isCurrentOpen && (stage.isCurrent ?? false)}
              chevronPosition="end"
              trigger={<StageTitle stage={stage} />}>
              <StageDetails stage={stage} />
            </Collapsible>
          </Section>
        ))}
    </VStack>
  );
}

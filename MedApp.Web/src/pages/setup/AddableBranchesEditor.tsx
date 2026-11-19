import {Button} from '@astryxdesign/core/Button';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import type {Schemas} from '../../api/client';
import type {PathwayTemplate} from '../../api/pathways';
import type {StageDefinition} from '../../api/pathwaySetup';
import {AnchoredMultiSelector, AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {AppIcon} from '../../components/shared/icons';

/**
 * A template's addable branch as the editor holds it (MC10): its name, its stages in order, the template stages it may
 * follow (by row key; none: any but the last) and whether it can be added more than once.
 */
export interface BranchRow {
  key: string;
  /** Its saved ID, kept so pathways that added it still name it; none for a new one. */
  id: string | null;
  name: string;
  stageDefinitionIds: string[];
  afterKeys: string[];
  isRepeatable: boolean;
}

let branchCount = 0;
const branchKey = () => `branch-${++branchCount}`;

/** A new, empty addable branch. */
export const newBranchRow = (): BranchRow => ({key: branchKey(), id: null, name: '', stageDefinitionIds: [], afterKeys: [], isRepeatable: false});

/** A saved template's addable branches, with the stages they may follow mapped onto the editor's rows by order. */
export function templateBranchRows(template: PathwayTemplate | null, rows: readonly {key: string}[]): BranchRow[] {
  return (template?.addableBranches ?? []).map((branch) => ({
    key: branchKey(),
    id: branch.id ?? null,
    name: branch.name ?? '',
    stageDefinitionIds: (branch.stages ?? []).map((s) => s.stageDefinitionId ?? ''),
    afterKeys: (branch.attachAfterStageOrders ?? []).flatMap((order) => (rows[order - 1] ? [rows[order - 1].key] : [])),
    isRepeatable: branch.isRepeatable ?? false,
  }));
}

/** What a save sends as `addableBranches`: each with the stages it may follow by order (rows removed are dropped). */
export function branchesToSend(branches: readonly BranchRow[], rows: readonly {key: string}[]): Schemas['PathwayTemplateBranchRequest'][] {
  const order = new Map(rows.map((r, i) => [r.key, i + 1]));
  return branches.map((b) => ({
    id: b.id,
    name: b.name.trim(),
    stageDefinitionIds: b.stageDefinitionIds,
    attachAfterStageOrders: b.afterKeys.flatMap((key) => (order.has(key) && order.get(key)! < rows.length ? [order.get(key)!] : [])).sort((x, y) => x - y),
    isRepeatable: b.isRepeatable,
  }));
}

export interface AddableBranchesEditorProps {
  branches: BranchRow[];
  onChange: (branches: BranchRow[]) => void;
  /** The template's stages, in order, as "1. First clinic visit", by row key. */
  stages: {key: string; label: string}[];
  /** The stage library's active definitions, by name. */
  definitions: StageDefinition[];
  /** Every definition (archived too), for names. */
  byId: Map<string, StageDefinition>;
  isDisabled: boolean;
}

/**
 * A template's branches added on new information (MC10, H-70): routes a clinician may add to a pathway with a reason,
 * never created with it and never suggested. Each has a name, its stages in order (added from the library, the last
 * removable), the template stages it may follow (none chosen: any but the last) and whether it can be added more
 * than once. The API's check is the last word.
 */
export function AddableBranchesEditor({branches, onChange, stages, definitions, byId, isDisabled}: AddableBranchesEditorProps) {
  const change = (key: string, update: Partial<BranchRow>) => onChange(branches.map((b) => (b.key === key ? {...b, ...update} : b)));
  const followable = stages.slice(0, -1).map((s) => ({value: s.key, label: s.label}));
  return (
    <VStack gap={3}>
      <Text type="body" color="secondary">
        Routes a clinician may add to a pathway made from this template when new information comes in, with a reason. They're
        never suggested, and their stages run beside what's left, before the last stage.
      </Text>
      {branches.length > 0 && (
        <List hasDividers aria-label="Branches added on new information">
          {branches.map((branch, i) => {
            const label = branch.name.trim() || `Branch ${i + 1}`;
            return (
              <ListItem
                key={branch.key}
                label={label}
                description={
                  <VStack as="span" gap={2}>
                    <TextInput
                      label="Name"
                      size="sm"
                      value={branch.name}
                      onChange={(name) => change(branch.key, {name})}
                      placeholder="e.g. Melanoma route"
                      autoComplete="off"
                      isDisabled={isDisabled}
                      containerStyle={NO_STATUS_LINE}
                    />
                    <Text type="body">
                      {branch.stageDefinitionIds.length > 0
                        ? `Its stages: ${branch.stageDefinitionIds.map((id, n) => `${n + 1}. ${byId.get(id)?.name ?? 'Unknown stage'}`).join(', ')}`
                        : 'No stages yet: add at least one.'}
                    </Text>
                    <HStack as="span" gap={2} vAlign="end" wrap="wrap">
                      <AnchoredSelector
                        label={`Add a stage to ${label}`}
                        size="sm"
                        placeholder="Choose a stage"
                        options={definitions.map((d) => ({value: d.id ?? '', label: d.name ?? ''}))}
                        value={undefined}
                        onChange={(id: string) => change(branch.key, {stageDefinitionIds: [...branch.stageDefinitionIds, id]})}
                        isDisabled={isDisabled}
                      />
                      {branch.stageDefinitionIds.length > 0 && (
                        <Button
                          label="Remove Last Stage"
                          variant="ghost"
                          size="sm"
                          onClick={() => change(branch.key, {stageDefinitionIds: branch.stageDefinitionIds.slice(0, -1)})}
                          isDisabled={isDisabled}
                        />
                      )}
                    </HStack>
                    <AnchoredMultiSelector
                      label="May follow"
                      description="The stages it can be added after. None chosen: any stage but the last."
                      size="sm"
                      options={followable}
                      value={branch.afterKeys.filter((key) => followable.some((f) => f.value === key))}
                      onChange={(afterKeys) => change(branch.key, {afterKeys})}
                      isDisabled={isDisabled}
                    />
                    <CheckboxInput
                      label="Can be added more than once"
                      size="sm"
                      value={branch.isRepeatable}
                      onChange={(isRepeatable) => change(branch.key, {isRepeatable})}
                      isDisabled={isDisabled}
                    />
                  </VStack>
                }
                endContent={
                  <IconButton
                    label={`Remove the ${label} branch`}
                    tooltip="Remove"
                    icon={<Icon icon={AppIcon.Cancel} />}
                    variant="ghost"
                    size="sm"
                    isDisabled={isDisabled}
                    onClick={() => onChange(branches.filter((b) => b.key !== branch.key))}
                  />
                }
              />
            );
          })}
        </List>
      )}
      <HStack gap={2}>
        <Button
          label="Add a Branch"
          variant="secondary"
          size="sm"
          icon={<Icon icon={AppIcon.Add} />}
          onClick={() => onChange([...branches, newBranchRow()])}
          isDisabled={isDisabled}
        />
      </HStack>
    </VStack>
  );
}

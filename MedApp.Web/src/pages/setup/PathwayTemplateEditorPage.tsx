import {createReorderable} from '@astryxdesign/core';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, fieldStatus} from '../../api/errors';
import type {PathwayTemplate} from '../../api/pathways';
import {stageDefinitionsQuery, templateQuery, useSaveTemplate, type StageDefinition} from '../../api/pathwaySetup';
import {teamsQuery, type Team} from '../../api/teams';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {stageDescription} from '../../components/pathways/SnomedConcept';
import {NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {
  isAfterBranchPoint,
  PLAIN_STAGE,
  stageOptionProblems,
  stageOptionsToSend,
  type StageOptionsInput,
} from './templateBranches';

/**
 * A stage in the template being edited, with its options (optional, branch point, branch). The key is the row's own,
 * as a definition may appear more than once.
 */
export interface StageRow extends StageOptionsInput {
  key: string;
  definitionId: string;
}

const BACK = '/admin/pathway-templates';

/** The teams a template can be given: the active ones (the route preloads them). */
export const templateTeamsQuery = teamsQuery(false);

/** Shown on Team when a new template is created without one (the API says the same, as the last word). */
export const TEAM_REQUIRED = 'Choose the team this template belongs to.';

/**
 * The retention classes a template can have (decision 139): how long its patients' records are kept before they're
 * reviewed. Standard keeps the adult or child period; the others are longer, from the Retention:Classes settings.
 */
export const RETENTION_CLASS_CHOICES = [
  {value: 'Standard', label: 'Standard (adult or child period)'},
  {value: 'Oncology', label: 'Oncology'},
  {value: 'Maternity', label: 'Maternity'},
  {value: 'MentalHealth', label: 'Mental health'},
];

/** No team: what the API sends for a template without one. */
const NO_ID = '00000000-0000-0000-0000-000000000000';

/**
 * The teams a template can have: the active ones, by name, and the one it has now if that's archived (it may
 * keep it, but no other template can be given it), marked so.
 */
export function teamChoices(active: readonly Team[], template: PathwayTemplate | null) {
  const choices = active
    .filter((s) => !s.isArchived)
    .map((s) => ({value: s.id ?? '', label: s.name ?? ''}))
    .sort((a, b) => a.label.localeCompare(b.label, undefined, {sensitivity: 'base'}));
  const current = template?.teamId;
  if (current && current !== NO_ID && !choices.some((c) => c.value === current)) {
    choices.unshift({value: current, label: `${template?.teamName ?? 'Unknown team'} (archived)`});
  }
  return choices;
}

/** The clock-stop choice when no stage stops the clock. */
const NO_STOP = 'none';

/** What the clock-stop choice says about itself. */
export const CLOCK_STOP_DESCRIPTION = 'The waiting-time clock stops when this stage is finished.';

/**
 * The row whose stage stops the waiting-time clock, from a saved template: its `clockStopStageOrder` (1-based), or
 * the stage marked `stopsClock`; none when it has neither.
 */
export function startingStopKey(template: PathwayTemplate | null, rows: readonly Pick<StageRow, 'key'>[]): string {
  const order = template?.clockStopStageOrder ?? (template?.stages ?? []).find((s) => s.stopsClock)?.stageOrder ?? null;
  return (order != null && rows[order - 1]?.key) || NO_STOP;
}

/** The 1-based position of the row that stops the clock, or null when none does (or it has been removed). */
export function stopStageOrder(rows: readonly Pick<StageRow, 'key'>[], stopKey: string): number | null {
  const index = rows.findIndex((r) => r.key === stopKey);
  return index < 0 ? null : index + 1;
}

let rowCount = 0;
/** A new row for a stage, with a key of its own (a definition can be in a template more than once), plain unless given options. */
const row = (definitionId: string, options: Partial<StageOptionsInput> = {}): StageRow => ({
  key: `stage-${++rowCount}`,
  definitionId,
  ...PLAIN_STAGE,
  ...options,
});

/** A saved template's rows: its stages in order, each with the options the template gave it (plain when it has none). */
function templateRows(template: PathwayTemplate | null): StageRow[] {
  const stages = [...(template?.stages ?? [])].sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0));
  return (template?.stageDefinitionIdsInOrder ?? []).map((definitionId, i) => {
    const stage = stages[i]?.stageDefinitionId === definitionId ? stages[i] : undefined;
    return row(definitionId, {
      isOptional: stage?.isOptional ?? false,
      choosesBranch: stage?.choosesBranch ?? false,
      branch: stage?.branch ?? '',
      // Stage 1 allows only the lab as another owner (the relay, MC7).
      byLab: Boolean(stage?.ownerTeamId),
    });
  });
}

/** A stage's options in a few words, for the confirmation ("optional", "branch point", "Benign"); empty when plain. */
function optionWords(stage: StageRow): string {
  const words = [stage.isOptional && 'optional', stage.choosesBranch && 'branch point', stage.branch.trim(), stage.byLab && 'the lab’s'].filter(
    Boolean,
  );
  return words.length > 0 ? ` (${words.join(', ')})` : '';
}

/**
 * A new template (no `id`), or one to edit. This part loads the template and the stage definitions; an unknown
 * template goes back to the list. The editor mounts once they're here, so it starts from the template.
 */
export function PathwayTemplateEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...templateQuery(id ?? ''), enabled: id !== undefined});
  const definitions = useQuery(stageDefinitionsQuery);
  const teams = useQuery(templateTeamsQuery);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  const isLoading = definitions.isPending || teams.isPending || (id !== undefined && template.isPending) || isMissing;
  if (!isLoading && definitions.data && teams.data && (id === undefined || template.data)) {
    return <TemplateEditor template={template.data ?? null} definitions={definitions.data} teams={teams.data} />;
  }
  const failed = definitions.isError || teams.isError || (template.isError && !isMissing);
  return (
    <Page
      title={id ? 'Edit Pathway Template' : 'New Pathway Template'}
      backHref={BACK}
      backLabel="Back to pathway templates"
      isLoading={isLoading}
      loadingMessage="Loading template..."
      message={failed && <AlertMessage message="The template could not be loaded. Please try again." />}
    />
  );
}

/**
 * The template editor (decision 17): its name, the team it belongs to, and its stages in order. Stages are added from the active stage
 * definitions (a definition may be added more than once), dragged into order (or moved with the keyboard from
 * each row's handle) and removed. Each stage can be made optional or a branch point, and a stage after a branch point
 * put in a branch, shown indented under it (docs/centre-phases-3-5-plan.md, decisions 113 and 117); what the API would
 * refuse in them shows on the stage. Saving asks first, then goes back to the list.
 *
 * Wide, the stages and the definitions to add sit side by side; narrow, they're one form card. Either way the
 * cards scroll, not the page.
 */
function TemplateEditor({
  template,
  definitions,
  teams,
}: {
  template: PathwayTemplate | null;
  definitions: StageDefinition[];
  teams: Team[];
}) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const save = useSaveTemplate();
  const announce = useAnnounce();
  const [name, setName] = useState(template?.name ?? '');
  const choices = useMemo(() => teamChoices(teams, template), [teams, template]);
  const startingTeam = template?.teamId && template.teamId !== NO_ID ? template.teamId : '';
  const [teamId, setTeamId] = useState(startingTeam);
  const [isTeamMissing, setIsTeamMissing] = useState(false);
  const startingClass = template?.retentionClass ?? 'Standard';
  const [retentionClass, setRetentionClass] = useState(startingClass);
  const [stages, setStages] = useState<StageRow[]>(() => templateRows(template));
  // The stage that stops the clock, by its row's own key: it moves with the row, and goes with it when it's removed.
  const [stopKey, setStopKey] = useState(() => startingStopKey(template, stages));
  const stopOrder = stopStageOrder(stages, stopKey);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const byId = useMemo(() => new Map(definitions.map((d) => [d.id ?? '', d])), [definitions]);
  const available = definitions
    .filter((d) => !d.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const backToList = () => void navigate({to: BACK});
  const usedBy = template?.pathwayCount ?? 0;

  const reorderable = useMemo(
    () =>
      createReorderable({
        onReorder: ({key, to}) =>
          setStages((current) => {
            const from = current.findIndex((s) => s.key === key);
            if (from < 0) return current;
            const next = [...current];
            const [moved] = next.splice(from, 1);
            next.splice(to, 0, moved);
            return next;
          }),
      }),
    [],
  );

  const add = (definitionId: string) => setStages((current) => [...current, row(definitionId)]);
  const remove = (key: string) => setStages((current) => current.filter((s) => s.key !== key));
  const change = (key: string, options: Partial<StageOptionsInput>) =>
    setStages((current) => current.map((s) => (s.key === key ? {...s, ...options} : s)));
  // What the API would refuse in the stages' options, said on each stage before saving (the API's check is the last word).
  const optionProblems = stageOptionProblems(stages);
  // The relay (MC7): a stage can be the lab's, unless the template is the lab's own. A template that had one sends
  // every stage's options, so one cleared is cleared.
  const labTeamId = teams.find((t) => t.kind === 'Lab')?.id ?? null;
  const canBeLabs = labTeamId !== null && teamId !== labTeamId;
  const hadLabStages = (template?.stages ?? []).some((s) => s.ownerTeamId);
  const stageOptions = stageOptionsToSend(
    stages.map((s) => ({...s, byLab: canBeLabs && s.byLab})),
    labTeamId,
    hadLabStages,
  );

  const submit = () =>
    save.mutate(
      {
        id: template?.id,
        name: name.trim(),
        stageDefinitionIdsInOrder: stages.map((s) => s.definitionId),
        clockStopStageOrder: stopOrder,
        // Only when a stage has an option: a template without any saves exactly as before.
        ...(stageOptions ? {stageOptions} : {}),
        // A new template always has one; a saved one sends it only when it changed, so a template keeps an archived one.
        ...(teamId && teamId !== startingTeam ? {teamId} : {}),
        // Only when it changed: a saved template's class also changes its existing pathways'.
        ...(retentionClass !== startingClass ? {retentionClass} : {}),
      },
      {
        onSuccess: () => {
          announce(template ? 'Template saved' : 'Template created');
          backToList();
        },
        onError: (e) => {
          setProblem(saveProblem(e, template ? 'save' : 'add', 'template'));
          setIsConfirming(false);
        },
      },
    );

  const teamField = (
    <AnchoredSelector
      label="Team"
      description="Which team its pathways belong to."
      placeholder="Choose a team"
      options={choices}
      value={teamId || undefined}
      onChange={(value: string) => {
        setTeamId(value);
        setIsTeamMissing(false);
      }}
      isRequired
      isDisabled={save.isPending}
      status={isTeamMissing ? {type: 'error', message: TEAM_REQUIRED} : fieldStatus(problem.validation, 'teamId')}
    />
  );

  const retentionField = (
    <AnchoredSelector
      label="Retention class"
      description={
        usedBy > 0
          ? "How long its patients' records are kept before review. A longer class applies to its existing pathways too; a shorter one doesn't shorten their records."
          : "How long its patients' records are kept before review."
      }
      options={RETENTION_CLASS_CHOICES}
      value={retentionClass}
      onChange={setRetentionClass}
      isDisabled={save.isPending}
      status={fieldStatus(problem.validation, 'retentionClass')}
    />
  );

  const nameField = (
    <TextInput
      label="Name"
      description="What staff choose when they create a pathway."
      value={name}
      onChange={setName}
      placeholder="e.g. Diagnostic pathway"
      autoComplete="off"
      isRequired
      isDisabled={save.isPending}
      status={fieldStatus(problem.validation, 'name')}
    />
  );

  const details = (
    <>
      {nameField}
      {teamField}
      {retentionField}
    </>
  );

  const stageList =
    stages.length === 0 ? (
      <EmptyState
        title="No stages yet"
        description={isNarrow ? 'Add stages from the list below.' : 'Add stages from All Stages, beside this.'}
        isCompact
      />
    ) : (
      <List reorderable={reorderable} hasDividers aria-label="Stages in order">
        {stages.map((stage, i) => {
          const definition = byId.get(stage.definitionId);
          const afterBranchPoint = isAfterBranchPoint(stages, i);
          // A stage in a branch sits indented under the branch point before it, so the branches read as groups.
          const isInBranch = afterBranchPoint && !stage.choosesBranch && stage.branch.trim() !== '';
          return (
            <ListItem
              key={stage.key}
              itemKey={stage.key}
              style={isInBranch ? BRANCH_INDENT : undefined}
              label={`${i + 1}. ${definition?.name ?? 'Unknown stage'}`}
              description={
                <StageOptionFields
                  stage={stage}
                  description={stageDescription(definition?.description, {code: definition?.snomedCode, term: definition?.snomedTerm})}
                  canBeInBranch={afterBranchPoint}
                  canBeLabs={canBeLabs}
                  problem={optionProblems.get(i)}
                  isDisabled={save.isPending}
                  onChange={(options) => change(stage.key, options)}
                />
              }
              endContent={
                <HStack gap={2} vAlign="center">
                  {stage.key === stopKey && <Token size="sm" label="Stops the clock" color="blue" />}
                  <VisitRequirementToken requirement={definition?.visitRequirement} />
                  {definition?.isArchived && <Token size="sm" label="Archived" color="gray" />}
                  <IconButton
                    label={`Remove ${definition?.name ?? 'stage'} (stage ${i + 1})`}
                    tooltip="Remove"
                    icon={<Icon icon={AppIcon.Cancel} />}
                    variant="ghost"
                    size="sm"
                    isDisabled={save.isPending}
                    onClick={() => remove(stage.key)}
                  />
                </HStack>
              }
            />
          );
        })}
      </List>
    );

  const definitionList = (
    <List hasDividers aria-label="Stages to add">
      {available.map((definition) => (
        <ListItem
          key={definition.id}
          label={definition.name ?? ''}
          description={stageDescription(definition.description, {code: definition.snomedCode, term: definition.snomedTerm})}
          endContent={
            <HStack gap={2} vAlign="center">
              <VisitRequirementToken requirement={definition.visitRequirement} />
              <IconButton
                label={`Add ${definition.name}`}
                tooltip="Add to the end"
                icon={<Icon icon={AppIcon.Add} />}
                variant="ghost"
                size="sm"
                isDisabled={save.isPending}
                onClick={() => add(definition.id ?? '')}
              />
            </HStack>
          }
        />
      ))}
    </List>
  );

  const form = (children: ReactNode) => (
    <FormCard
      fill
      saveText={template ? 'Save' : 'Create Template'}
      onSave={() => {
        setProblem(NO_PROBLEM);
        // A new template needs a team: say so on the field rather than ask to confirm one that can't be saved.
        if (!teamId) {
          setIsTeamMissing(true);
          return;
        }
        setIsConfirming(true);
      }}
      onCancel={backToList}
      isProcessing={save.isPending}>
      {children}
    </FormCard>
  );

  const stagesTitle = `Stages (${stages.length})`;

  const stageLabel = (stage: StageRow, i: number) => `${i + 1}. ${byId.get(stage.definitionId)?.name ?? 'Unknown stage'}`;
  const clockStopField = (
    <AnchoredSelector
      label="Clock-stop stage"
      description={CLOCK_STOP_DESCRIPTION}
      options={[{value: NO_STOP, label: 'None'}, ...stages.map((stage, i) => ({value: stage.key, label: stageLabel(stage, i)}))]}
      value={stopOrder === null ? NO_STOP : stopKey}
      onChange={(value: string) => setStopKey(value)}
      isDisabled={save.isPending || stages.length === 0}
      status={fieldStatus(problem.validation, 'clockStopStageOrder')}
    />
  );

  return (
    <Page
      title={template ? 'Edit Pathway Template' : 'New Pathway Template'}
      subtitle={template?.name ?? undefined}
      onBack={backToList}
      backLabel="Back to pathway templates"
      fill
      message={
        problem.validation ? (
          // The name's errors show on it; the stages' (e.g. none chosen) show here.
          <ValidationAlert errors={problem.validation} fields={['name', 'teamId', 'clockStopStageOrder']} />
        ) : (
          problem.message && <AlertMessage message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title={template ? 'Confirm Changes' : 'Confirm New Template'}
          message={
            template
              ? usedBy > 0
                ? `New pathways will use these stages. The ${usedBy} ${usedBy === 1 ? 'pathway' : 'pathways'} already made from it keep theirs.`
                : 'Are you sure you want to save these changes?'
              : 'Are you sure you want to create this template?'
          }
          icon={template ? AppIcon.Save : AppIcon.Add}
          confirmText={template ? 'Confirm Save' : 'Create Template'}
          isProcessing={save.isPending}
          onConfirm={submit}
          onCancel={() => setIsConfirming(false)}>
          <MetadataList>
            <MetadataListItem label="Template">{name.trim() || 'No name'}</MetadataListItem>
            <MetadataListItem label="Team">{choices.find((c) => c.value === teamId)?.label ?? 'None'}</MetadataListItem>
            <MetadataListItem label="Stages">
              {stages.length ? stages.map((s, i) => `${i + 1}. ${byId.get(s.definitionId)?.name}${optionWords(s)}`).join(', ') : 'None'}
            </MetadataListItem>
            <MetadataListItem label="Clock stops">
              {stopOrder === null ? 'At no stage' : `When ${stageLabel(stages[stopOrder - 1], stopOrder - 1)} is finished`}
            </MetadataListItem>
          </MetadataList>
        </ConfirmDialog>
      }>
      {isNarrow ? (
        form(
          <>
            <FormSection title="Details" useGrid={false}>
              {details}
            </FormSection>
            <FormSection title={stagesTitle} useGrid={false}>
              {stageList}
              {clockStopField}
            </FormSection>
            <FormSection title="Add Stages" useGrid={false}>
              {definitionList}
            </FormSection>
          </>,
        )
      ) : (
        <TwoColumns
          start={form(
            <>
              <FormSection title="Details" useGrid={false}>
                {details}
              </FormSection>
              <FormSection title={stagesTitle} useGrid={false}>
                {stageList}
                {clockStopField}
              </FormSection>
            </>,
          )}
          end={<PanelCard title="All Stages">{definitionList}</PanelCard>}
        />
      )}
    </Page>
  );
}

/** Indents a stage in a branch under its branch point (a layout value the list has no prop for). */
const BRANCH_INDENT = {marginInlineStart: 'var(--spacing-8)'} as const;

interface StageOptionFieldsProps {
  stage: StageOptionsInput;
  /** The definition's description and SNOMED CT concept, as the row showed before options. */
  description: ReactNode;
  /** It comes after a branch point, so it can be put in a branch. */
  canBeInBranch: boolean;
  /** The template isn't the lab's, so a stage can be the lab's to work (the relay). */
  canBeLabs?: boolean;
  /** What the API would refuse in its options, if anything. */
  problem?: string;
  isDisabled: boolean;
  onChange: (options: Partial<StageOptionsInput>) => void;
}

/**
 * A template stage's description, then its options (decisions 113 and 117): Optional (staff may skip it, with a
 * reason, while it's current), Branch point (finishing it, the clinician chooses one of the branches after it) and,
 * for a stage after a branch point, the Branch it's in (left empty, it always runs). Any problem the API would refuse
 * shows under them. Plain stages show just the two boxes, unticked.
 */
function StageOptionFields({stage, description, canBeInBranch, canBeLabs = false, problem, isDisabled, onChange}: StageOptionFieldsProps) {
  // The branch field stays while it has a name, so one left after its branch point moved can still be cleared.
  const hasBranchField = (canBeInBranch && !stage.choosesBranch) || stage.branch !== '';
  return (
    <VStack as="span" gap={2}>
      {description}
      <HStack as="span" gap={4} vAlign="center" wrap="wrap">
        <CheckboxInput
          label="Optional"
          size="sm"
          value={stage.isOptional}
          onChange={(isOptional) => onChange({isOptional})}
          isDisabled={isDisabled}
        />
        <CheckboxInput
          label="Branch point"
          size="sm"
          value={stage.choosesBranch}
          onChange={(choosesBranch) => onChange({choosesBranch})}
          isDisabled={isDisabled}
        />
        {canBeLabs && (
          <CheckboxInput
            label="The lab’s stage"
            size="sm"
            value={stage.byLab ?? false}
            onChange={(byLab) => onChange({byLab})}
            isDisabled={isDisabled}
          />
        )}
        {hasBranchField && (
          <TextInput
            label="Branch"
            size="sm"
            value={stage.branch}
            onChange={(branch) => onChange({branch})}
            placeholder="e.g. Treatment"
            autoComplete="off"
            isDisabled={isDisabled}
            containerStyle={NO_STATUS_LINE}
          />
        )}
      </HStack>
      {problem && (
        <HStack as="span" gap={1} vAlign="center">
          <Icon icon={AppIcon.Warning} size="xsm" color="warning" />
          <Text type="supporting" color="primary">
            {problem}
          </Text>
        </HStack>
      )}
    </VStack>
  );
}

import {createReorderable} from '@astryxdesign/core';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {AnchoredMultiSelector, AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {Button} from '@astryxdesign/core/Button';
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
import {chainLinks, CHOICE_KINDS, choiceKindLabel, hasRoutes, routeDepths, templateGraphProblems} from '../../api/pathwayRoutes';
import {AddableBranchesEditor, branchesToSend, templateBranchRows, type BranchRow} from './AddableBranchesEditor';
import {
  linksFromRoutes,
  outcomeRow,
  outcomeRows,
  outcomesFromRows,
  PLAIN_STAGE,
  routesFromLinks,
  tidyRoutes,
  type OutcomeRow,
  type Routes,
  stageOptionProblems,
  stageOptionsToSend,
  type StageOptionsInput,
} from './templateBranches';

/**
 * A stage in the template being edited, with its options (optional, a choice node and its outcomes). The key is the row's own,
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

/** The choice-node choice for a plain stage. */
const NO_CHOICE = 'none';

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
  const ids = template?.stageDefinitionIdsInOrder ?? [];
  const saved = (i: number) => (stages[i]?.stageDefinitionId === ids[i] ? stages[i] : undefined);
  const rows = ids.map((definitionId, i) =>
    row(definitionId, {
      isOptional: saved(i)?.isOptional ?? false,
      choiceKind: saved(i)?.choiceKind ?? '',
      // Stage 1 allows only the lab as another owner (the relay, MC7).
      byLab: Boolean(saved(i)?.ownerTeamId),
    }),
  );
  // A choice node's outcomes name the rows they lead to, by key, so they follow them as rows move.
  return rows.map((r, i) => (r.choiceKind ? {...r, outcomes: outcomeRows(saved(i)?.outcomes, rows)} : r));
}

/** What the editor holds: a template's details, stages, routes and branches. */
export interface TemplateEdit {
  name: string;
  teamId: string;
  retentionClass: string;
  stages: StageRow[];
  /** The stage that stops the clock, by its row's key. */
  stopKey: string;
  /** Each row's predecessors by key (MC9), or null while the stages are a plain list. */
  routes: Routes | null;
  branches: BranchRow[];
}

/** The editor's state for a template (null: a new one), as it opens. */
export function startingEdit(template: PathwayTemplate | null): TemplateEdit {
  const stages = templateRows(template);
  return {
    name: template?.name ?? '',
    teamId: template?.teamId && template.teamId !== NO_ID ? template.teamId : '',
    retentionClass: template?.retentionClass ?? 'Standard',
    stages,
    stopKey: startingStopKey(template, stages),
    routes: template !== null && hasRoutes(template) ? routesFromLinks(stages, template.links ?? []) : null,
    branches: templateBranchRows(template, stages),
  };
}

/** The links the routes draw (MC9), kept valid as rows move, or null for a plain list. */
export const editLinks = (stages: readonly StageRow[], routes: Routes | null) => (routes ? linksFromRoutes(stages, tidyRoutes(stages, routes)) : null);

/**
 * What Save sends for the editor's state: a saved template's team and retention class only when they changed, and its
 * options, links and branches only when it has (or had) any, so a template opened and saved unchanged stays as it was.
 */
export function templateSaveRequest(template: PathwayTemplate | null, edit: TemplateEdit, labTeamId: string | null) {
  const start = {teamId: template?.teamId && template.teamId !== NO_ID ? template.teamId : '', retentionClass: template?.retentionClass ?? 'Standard'};
  const links = editLinks(edit.stages, edit.routes);
  // The relay (MC7): a stage can be the lab's, unless the template is the lab's own. A template that had one sends
  // every stage's options, so one cleared is cleared.
  const canBeLabs = labTeamId !== null && edit.teamId !== labTeamId;
  const hadLabStages = (template?.stages ?? []).some((s) => s.ownerTeamId);
  const stageOptions = stageOptionsToSend(
    edit.stages.map((s) => ({...s, byLab: canBeLabs && s.byLab})),
    labTeamId,
    hadLabStages,
    links,
  );
  return {
    id: template?.id,
    name: edit.name.trim(),
    stageDefinitionIdsInOrder: edit.stages.map((s) => s.definitionId),
    clockStopStageOrder: stopStageOrder(edit.stages, edit.stopKey),
    // Only when a stage has an option: a template without any saves exactly as before.
    ...(stageOptions ? {stageOptions} : {}),
    // Routes drawn (MC9) send every link; a list sends none, and the API links it as a list.
    ...(links ? {links} : {}),
    // Only when it has, or had, any: a template without them saves exactly as before.
    ...(edit.branches.length > 0 || (template?.addableBranches ?? []).length > 0 ? {addableBranches: branchesToSend(edit.branches, edit.stages)} : {}),
    // A new template always has one; a saved one sends it only when it changed, so a template keeps an archived one.
    ...(edit.teamId && edit.teamId !== start.teamId ? {teamId: edit.teamId} : {}),
    // Only when it changed: a saved template's class also changes its existing pathways'.
    ...(edit.retentionClass !== start.retentionClass ? {retentionClass: edit.retentionClass} : {}),
  };
}

/** A stage's options in a few words, for the confirmation ("optional", "lab result: Benign or Malignant"); empty when plain. */
function optionWords(stage: StageRow): string {
  const outcomes = stage.outcomes.map((o) => o.name.trim()).filter(Boolean);
  const kind = stage.choiceKind && choiceKindLabel(stage.choiceKind).toLowerCase();
  const words = [
    stage.isOptional && 'optional',
    kind && (outcomes.length > 0 ? `${kind}: ${outcomes.join(' or ')}` : kind),
    stage.byLab && 'the lab’s',
  ].filter(Boolean);
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
      message={failed && <AlertMessage label="Template not loaded" message="The template could not be loaded. Please try again." />}
    />
  );
}

/**
 * The template editor (decision 17): its name, the team it belongs to, and its stages in order. Stages are added from the active stage
 * definitions (a definition may be added more than once), dragged into order (or moved with the keyboard from
 * each row's handle) and removed. Each stage can be made optional or a choice node (an assessment or a lab result,
 * whose outcome the clinician records); what the API would refuse in them shows on the stage. Saving asks first, then
 * goes back to the list.
 *
 * The route editor (MC9): Draw Routes turns the list into routes, where each stage says what it Comes after (any
 * stages above it), so routes can run side by side (a fork) and meet again (a join), and end anywhere (a discharge, a
 * referral). A template is the full tree: a choice node lists its outcomes, each naming the stages after it that it
 * leads to (several side by side, or none to end the route; several outcomes may lead to one stage). Choosing a choice
 * node draws routes; a template with routes opens this way. Stages indent by how deep in the routes they sit. The graph rules (`templateGraphProblems`)
 * show on each stage before saving.
 *
 * Branches added on new information (MC10): the template's addable branches, edited below the stages.
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
  const [start] = useState(() => startingEdit(template));
  const [name, setName] = useState(start.name);
  const choices = useMemo(() => teamChoices(teams, template), [teams, template]);
  const [teamId, setTeamId] = useState(start.teamId);
  const [isTeamMissing, setIsTeamMissing] = useState(false);
  const [retentionClass, setRetentionClass] = useState(start.retentionClass);
  const [stages, setStages] = useState<StageRow[]>(start.stages);
  // The stage that stops the clock, by its row's own key: it moves with the row, and goes with it when it's removed.
  const [stopKey, setStopKey] = useState(start.stopKey);
  const stopOrder = stopStageOrder(stages, stopKey);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const byId = useMemo(() => new Map(definitions.map((d) => [d.id ?? '', d])), [definitions]);
  const available = definitions
    .filter((d) => !d.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const backToList = () => void navigate({to: BACK});
  const usedBy = template?.pathwayCount ?? 0;
  // The route editor (MC9): each row's predecessors by key, or null while the stages are a plain list.
  const [rawRoutes, setRoutes] = useState<Routes | null>(start.routes);
  const routes = rawRoutes && tidyRoutes(stages, rawRoutes);
  const links = editLinks(stages, rawRoutes);
  const depths = links ? routeDepths(stages.length, links) : [];
  const chainRoutes = () => routesFromLinks(stages, chainLinks(stages.map((_, i) => ({stageOrder: i + 1}))));
  const drawRoutes = () => setRoutes(chainRoutes());
  const [branches, setBranches] = useState<BranchRow[]>(start.branches);

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
  const change = (key: string, options: Partial<StageOptionsInput>) => {
    // A choice node's outcomes are routes: choosing one in a list draws them, and a new one starts with an unnamed
    // outcome for each stage straight after it.
    const now = routes ?? chainRoutes();
    if (options.choiceKind && !routes) setRoutes(now);
    const stage = stages.find((s) => s.key === key);
    const fresh = options.choiceKind && stage && stage.outcomes.length === 0;
    const more = fresh ? {outcomes: stages.filter((s) => now[s.key]?.includes(key)).map((s) => outcomeRow('', [s.key]))} : {};
    setStages((current) => current.map((s) => (s.key === key ? {...s, ...options, ...more} : s)));
  };
  // An outcome leading to a stage makes that stage come after its choice node.
  const changeOutcomes = (key: string, outcomes: OutcomeRow[]) => {
    if (routes) {
      const led = new Set(outcomes.flatMap((o) => o.nextKeys));
      setRoutes(Object.fromEntries(Object.entries(routes).map(([row, before]) => [row, led.has(row) && !before.includes(key) ? [...before, key] : before])));
    }
    change(key, {outcomes});
  };
  // What the API would refuse in the stages' options, said on each stage before saving (the API's check is the last word).
  const optionProblems = links
    ? templateGraphProblems(
        stages.map((s, i) => ({isOptional: s.isOptional, choiceKind: s.choiceKind || null, outcomes: s.choiceKind ? outcomesFromRows(stages, links, i) : null})),
        links,
      )
    : stageOptionProblems(stages);
  // The relay (MC7): a stage can be the lab's, unless the template is the lab's own.
  const labTeamId = teams.find((t) => t.kind === 'Lab')?.id ?? null;
  const canBeLabs = labTeamId !== null && teamId !== labTeamId;

  const submit = () =>
    save.mutate(
      templateSaveRequest(template, {name, teamId, retentionClass, stages, stopKey, routes: rawRoutes, branches}, labTeamId),
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

  const stageLabel = (stage: StageRow, i: number) => `${i + 1}. ${byId.get(stage.definitionId)?.name ?? 'Unknown stage'}`;

  // Draw Routes (MC9): from a list, each stage then says what it comes after.
  const routesControl = routes ? (
    <Text type="supporting" color="secondary">
      Routes: each stage starts when every stage it comes after is finished or skipped. Stages after a fork run side by side.
    </Text>
  ) : (
    stages.length > 1 && (
      <HStack gap={2}>
        <Button
          label="Draw Routes"
          tooltip="Let stages run side by side and meet again"
          variant="secondary"
          size="sm"
          onClick={drawRoutes}
          isDisabled={save.isPending}
        />
      </HStack>
    )
  );

  const branchesEditor = (
    <AddableBranchesEditor
      branches={branches}
      onChange={setBranches}
      stages={stages.map((stage, i) => ({key: stage.key, label: stageLabel(stage, i)}))}
      definitions={available}
      byId={byId}
      isDisabled={save.isPending}
    />
  );

  const stageList = stages.length === 0 ? (
      <EmptyState
        title="No stages yet"
        description={isNarrow ? 'Add stages from the list below.' : 'Add stages from All Stages, beside this.'}
        isCompact
      />
    ) : (
      <List reorderable={reorderable} hasDividers aria-label="Stages in order">
        {stages.map((stage, i) => {
          const definition = byId.get(stage.definitionId);
          const before = routes?.[stage.key] ?? [];
          // With routes, a stage sits indented by how deep in them it is.
          const indent = routes ? (depths[i + 1] ?? 0) : 0;
          return (
            <ListItem
              key={stage.key}
              itemKey={stage.key}
              style={indent > 0 ? {marginInlineStart: `calc(${indent} * var(--spacing-8))`} : undefined}
              label={`${i + 1}. ${definition?.name ?? 'Unknown stage'}`}
              description={
                <StageOptionFields
                  stage={stage}
                  description={stageDescription(definition?.description, {code: definition?.snomedCode, term: definition?.snomedTerm})}
                  canBeLabs={canBeLabs}
                  outcomes={
                    routes &&
                    stage.choiceKind && (
                      <OutcomesEditor
                        outcomes={stage.outcomes}
                        stages={stages.slice(i + 1).map((other, n) => ({value: other.key, label: stageLabel(other, i + 1 + n)}))}
                        after={stages.filter((other) => routes[other.key]?.includes(stage.key)).map((other) => other.key)}
                        isDisabled={save.isPending}
                        onChange={(outcomes) => changeOutcomes(stage.key, outcomes)}
                      />
                    )
                  }
                  comesAfter={
                    routes &&
                    i > 0 && (
                      <AnchoredMultiSelector
                        label={`${i + 1}. ${definition?.name ?? 'Stage'} comes after`}
                        size="sm"
                        options={stages.slice(0, i).map((other, n) => ({value: other.key, label: stageLabel(other, n)}))}
                        value={before}
                        onChange={(keys) => setRoutes({...routes, [stage.key]: keys})}
                        isDisabled={save.isPending}
                      />
                    )
                  }
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
          problem.message && <AlertMessage label={template ? 'Not saved' : 'Not created'} message={problem.message} />
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
              {routesControl}
              {stageList}
              {clockStopField}
            </FormSection>
            <FormSection title="Branches Added on New Information" useGrid={false}>
              {branchesEditor}
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
                {routesControl}
                {stageList}
                {clockStopField}
              </FormSection>
              <FormSection title="Branches Added on New Information" useGrid={false}>
                {branchesEditor}
              </FormSection>
            </>,
          )}
          end={<PanelCard title="All Stages">{definitionList}</PanelCard>}
        />
      )}
    </Page>
  );
}

interface StageOptionFieldsProps {
  stage: StageOptionsInput;
  /** The definition's description and SNOMED CT concept, as the row showed before options. */
  description: ReactNode;
  /** The template isn't the lab's, so a stage can be the lab's to work (the relay). */
  canBeLabs?: boolean;
  /** With routes (MC9), the stages it comes after. */
  comesAfter?: ReactNode;
  /** With routes, a choice node's outcomes and the stages each leads to. */
  outcomes?: ReactNode;
  /** What the API would refuse in its options, if anything. */
  problem?: string;
  isDisabled: boolean;
  onChange: (options: Partial<StageOptionsInput>) => void;
}

/**
 * A template stage's description, then its options: Optional (staff may skip it, with a reason, while it's current),
 * Choice node (no, an assessment or a lab result: finishing it, the clinician records one of its outcomes) and, on a
 * choice node, its outcomes. Any problem the API would refuse shows under them.
 */
function StageOptionFields({stage, description, canBeLabs = false, comesAfter, outcomes, problem, isDisabled, onChange}: StageOptionFieldsProps) {
  return (
    <VStack as="span" gap={2}>
      {description}
      {comesAfter}
      <HStack as="span" gap={4} vAlign="center" wrap="wrap">
        <CheckboxInput
          label="Optional"
          size="sm"
          value={stage.isOptional}
          onChange={(isOptional) => onChange({isOptional})}
          isDisabled={isDisabled}
        />
        <AnchoredSelector
          label="Choice node"
          size="sm"
          options={[{value: NO_CHOICE, label: 'No'}, ...CHOICE_KINDS.map((k) => ({value: k.value, label: k.label}))]}
          value={stage.choiceKind || NO_CHOICE}
          onChange={(value: string) => onChange({choiceKind: value === NO_CHOICE ? '' : value})}
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
      </HStack>
      {outcomes}
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

interface OutcomesEditorProps {
  outcomes: OutcomeRow[];
  /** The stages below the choice node, any of which an outcome may lead to (choosing one makes it come after it). */
  stages: {value: string; label: string}[];
  /** The keys of the stages that come straight after it: an outcome leads only to those. */
  after: string[];
  isDisabled: boolean;
  onChange: (outcomes: OutcomeRow[]) => void;
}

/**
 * A choice node's outcomes, in order: each named, with the stages after it that it leads to (several run side by side;
 * none ends the route). Several outcomes may lead to the same stage.
 */
function OutcomesEditor({outcomes, stages, after, isDisabled, onChange}: OutcomesEditorProps) {
  const set = (key: string, change: Partial<OutcomeRow>) => onChange(outcomes.map((o) => (o.key === key ? {...o, ...change} : o)));
  return (
    <VStack as="span" gap={2}>
      {outcomes.map((outcome, n) => (
        <HStack as="span" key={outcome.key} gap={2} vAlign="end" wrap="wrap">
          <TextInput
            label={`Outcome ${n + 1}`}
            size="sm"
            value={outcome.name}
            onChange={(name) => set(outcome.key, {name})}
            placeholder="e.g. Benign"
            autoComplete="off"
            isDisabled={isDisabled}
            containerStyle={NO_STATUS_LINE}
          />
          <AnchoredMultiSelector
            label={`Outcome ${n + 1} leads to`}
            size="sm"
            placeholder="Ends the route"
            options={stages}
            value={outcome.nextKeys.filter((key) => after.includes(key))}
            onChange={(nextKeys) => set(outcome.key, {nextKeys})}
            isDisabled={isDisabled}
          />
          <IconButton
            label={`Remove outcome ${n + 1}`}
            tooltip="Remove"
            icon={<Icon icon={AppIcon.Cancel} />}
            variant="ghost"
            size="sm"
            isDisabled={isDisabled}
            onClick={() => onChange(outcomes.filter((o) => o.key !== outcome.key))}
          />
        </HStack>
      ))}
      <HStack as="span" gap={2}>
        <Button label="Add Outcome" variant="secondary" size="sm" isDisabled={isDisabled} onClick={() => onChange([...outcomes, outcomeRow()])} />
      </HStack>
    </VStack>
  );
}

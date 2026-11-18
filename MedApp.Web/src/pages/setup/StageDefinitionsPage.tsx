import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Grid} from '@astryxdesign/core/Grid';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {
  stageDefinitionsQuery,
  useSaveStageDefinition,
  useStageDefinitionActions,
  type StageDefinition,
  type StageDefinitionInput,
  type VisitRequirement,
} from '../../api/pathwaySetup';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {SnomedCell, snomedLabel} from '../../components/pathways/SnomedConcept';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';
import {useListSettings} from '../../app/listSettings';
import {RichTextField} from '../../components/forms/RichTextField';

type DefinitionRow = StageDefinition & Record<string, unknown>;

type DialogState =
  | {kind: 'edit'; definition: StageDefinition | null}
  | {kind: 'archive' | 'delete'; definition: StageDefinition}
  | null;

const EMPTY: StageDefinitionInput = {name: '', description: '', instructions: '', snomedCode: '', snomedTerm: '', visitRequirement: 'None'};

/** The Visit choices, each saying what it means for staff finishing the stage (decisions 22 to 25). */
const VISIT_CHOICES: {value: VisitRequirement; label: string; description: string}[] = [
  {value: 'None', label: 'None', description: 'No appointment needed.'},
  {value: 'Suggested', label: 'Suggested', description: 'Staff confirm if they finish it without an attended appointment.'},
  {
    value: 'Required',
    label: 'Required',
    description: "It can't be finished until an appointment is marked Attended; an admin can override.",
  },
];

/** A definition's visit requirement, as one of the choices (None for anything else). */
const visitRequirementOf = (definition: StageDefinition): VisitRequirement =>
  VISIT_CHOICES.find((choice) => choice.value === definition.visitRequirement)?.value ?? 'None';

const FIELDS = Object.keys(EMPTY);

export interface StageDefinitionsPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

const templatesUsing = (n: number) => (n === 0 ? 'No templates' : `${n} ${n === 1 ? 'template' : 'templates'}`);

/**
 * Stage definitions (admin, decision 17): the reusable steps templates are built from. Admins add and edit them
 * (name, description, instructions, the SNOMED CT concept it records, and whether it involves a visit), archive ones no
 * longer wanted (kept by the templates and pathways that have them, but not offered for new templates), restore them,
 * and delete ones no template has used (decision 11).
 * A pathway keeps its own copy of each stage, so editing a definition changes templates, not existing pathways.
 */
export function StageDefinitionsPage({search, onSearchChange}: StageDefinitionsPageProps) {
  const {data = [], isPending, isError} = useQuery(stageDefinitionsQuery);
  const save = useSaveStageDefinition();
  const actions = useStageDefinitionActions();
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<StageDefinitionInput>(EMPTY);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);
  const set = (key: keyof StageDefinitionInput) => (value: string) => setForm((current) => ({...current, [key]: value}));

  const openEditor = (definition: StageDefinition | null) => {
    setForm(
      definition
        ? {
            name: definition.name ?? '',
            description: definition.description ?? '',
            instructions: definition.instructions ?? '',
            snomedCode: definition.snomedCode ?? '',
            snomedTerm: definition.snomedTerm ?? '',
            visitRequirement: visitRequirementOf(definition),
          }
        : EMPTY,
    );
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', definition});
  };
  const open = (kind: 'archive' | 'delete', definition: StageDefinition) => {
    setError(null);
    setDialog({kind, definition});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'stage').message);
    close();
  };

  const editing = dialog?.kind === 'edit' ? dialog.definition : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.definition : null;

  const saveDefinition = () =>
    save.mutate(
      {id: editing?.id, ...form},
      {
        onSuccess: () => {
          announce(editing ? 'Stage saved' : 'Stage added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editing ? 'save' : 'add', 'stage')),
      },
    );

  const columns: DataTableColumn<DefinitionRow>[] = [
    {
      key: 'name',
      header: 'Stage',
      width: proportional(3, {minWidth: 120}),
      renderCell: (d) => (
        <SetupNameCell name={d.name ?? ''} secondary={d.description} token={<VisitRequirementToken requirement={d.visitRequirement} />} />
      ),
    },
    {key: 'snomed', header: 'SNOMED CT', width: pixel(180), renderCell: (d) => <SnomedCell code={d.snomedCode} term={d.snomedTerm} />},
    {key: 'templates', header: 'Used in', width: pixel(140), renderCell: (d) => <Text type="body">{templatesUsing(d.templateCount ?? 0)}</Text>},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (d) => <SetupStatusToken isArchived={d.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (d) => (
        <TableActions
          actions={[
            {label: 'Edit stage', icon: AppIcon.Edit, onClick: () => openEditor(d)},
            d.isArchived
              ? {
                  label: 'Restore stage',
                  icon: AppIcon.Restore,
                  onClick: () => actions.restore.mutate(d.id ?? '', {onError: failed('restore')}),
                }
              : {label: 'Archive stage', icon: AppIcon.Archive, onClick: () => open('archive', d)},
            {
              label: d.canBeDeleted ? 'Delete stage' : 'Used by a template, so it can be archived but not deleted',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !d.canBeDeleted,
              onClick: () => open('delete', d),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading stages...' : setupSubtitle(total, search, 'stage', 'stages');

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Stage">{target.name}</MetadataListItem>
      {snomedLabel({code: target.snomedCode, term: target.snomedTerm}) && (
        <MetadataListItem label="SNOMED CT">{snomedLabel({code: target.snomedCode, term: target.snomedTerm})}</MetadataListItem>
      )}
      <MetadataListItem label="Used in">{templatesUsing(target.templateCount ?? 0)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Stages"
      subtitle={subtitle}
      fill
      message={
        error ? <AlertMessage message={error} /> : isError && <AlertMessage message="The stages could not be loaded. Please try again." />
      }
      actions={<Button label="Add Stage" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Stage' : 'Add Stage'}
            message={
              editing
                ? 'Templates use the changes from now on. Pathways already started keep the stage as it was.'
                : 'A step that templates can be built from.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Stage'}
            isProcessing={save.isPending}
            confirmDisabled={!form.name.trim()}
            hasSaveShortcut
            onConfirm={saveDefinition}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={form.name}
                onChange={set('name')}
                placeholder="e.g. Consent"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <TextInput
                label="Description"
                description="Shown under the stage's name: what it means for the stage to be done."
                value={form.description}
                onChange={set('description')}
                placeholder="e.g. Informed consent has been obtained and recorded."
                autoComplete="off"
                isRequired
                status={fieldStatus(problem.validation, 'description')}
              />
              {/* Started again for each stage opened (it reads its text as it opens). */}
              <RichTextField
                key={`${editing?.id ?? 'new'}:${dialog?.kind}`}
                label="Instructions"
                description="How staff carry the stage out."
                value={form.instructions}
                onChange={set('instructions')}
                isRequired
                status={fieldStatus(problem.validation, 'instructions')}
              />
              {/* The clinical concept the stage records, for reports and other systems: optional, both or neither. */}
              <Grid columns={2} gap={4}>
                <TextInput
                  label="SNOMED CT Code"
                  description="The concept's ID, e.g. 80146002."
                  value={form.snomedCode}
                  onChange={set('snomedCode')}
                  placeholder="e.g. 80146002"
                  inputMode="numeric"
                  autoComplete="off"
                  isOptional
                  status={fieldStatus(problem.validation, 'snomedCode')}
                />
                <TextInput
                  label="Display Term"
                  description="The concept's name, e.g. Appendectomy."
                  value={form.snomedTerm}
                  onChange={set('snomedTerm')}
                  placeholder="e.g. Appendectomy"
                  autoComplete="off"
                  isOptional
                  status={fieldStatus(problem.validation, 'snomedTerm')}
                />
              </Grid>
              <RadioList
                label="Visit"
                description="Whether the stage involves an appointment with the patient. New pathways ask for one to be booked when they reach it."
                value={form.visitRequirement}
                onChange={(value) => setForm((current) => ({...current, visitRequirement: value as VisitRequirement}))}
                status={fieldStatus(problem.validation, 'visitRequirement')}>
                {VISIT_CHOICES.map((choice) => (
                  <RadioListItem key={choice.value} value={choice.value} label={choice.label} description={choice.description} />
                ))}
              </RadioList>
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Stage"
            message="It won't be offered for new templates. Templates and pathways that have it keep it, and it can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => target && actions.archive.mutate(target.id ?? '', {onSuccess: close, onError: failed('archive')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="No template uses this stage. This action cannot be undone."
            isDanger
            confirmText="Delete Stage"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No stages yet"
          description="Stages are the steps templates are built from."
          icon={<Icon icon={AppIcon.StageDefinitions} size="lg" />}
          actions={<Button label="Add Your First Stage" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable
          data={rows as DefinitionRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          // The search: its name, and Active (to begin with) or Archived.
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="stages"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search stages"
              placeholder="Search stages by name, or filter by status..."
              textKey="name"
              favourites={SETUP_FAVOURITES}
            />
          }
          pagination={{
            page,
            pageSize,
            totalItems: total,
            onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
          }}
          emptyState={
            <EmptyState
              title="No stages match your search"
              description="Search for archived ones too, to see and restore them."
              isCompact
              actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
            />
          }
        />
      )}
    </Page>
  );
}

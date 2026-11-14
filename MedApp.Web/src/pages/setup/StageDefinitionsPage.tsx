import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {
  stageDefinitionsQuery,
  useSaveStageDefinition,
  useStageDefinitionActions,
  type StageDefinition,
  type StageDefinitionInput,
} from '../../api/procedureSetup';
import {PAGE_SIZE} from '../../api/search';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/procedures/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {setupPage} from './setupList';

type DefinitionRow = StageDefinition & Record<string, unknown>;

type DialogState =
  | {kind: 'edit'; definition: StageDefinition | null}
  | {kind: 'archive' | 'delete'; definition: StageDefinition}
  | null;

const EMPTY: StageDefinitionInput = {name: '', description: '', instructions: ''};
const FIELDS = Object.keys(EMPTY);

export interface StageDefinitionsPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

const templatesUsing = (n: number) => (n === 0 ? 'No templates' : `${n} ${n === 1 ? 'template' : 'templates'}`);

/**
 * Stage definitions (admin, decision 17): the reusable steps templates are built from. Admins add and edit them
 * (name, description, instructions), archive ones no longer wanted (kept by the templates and procedures that have
 * them, but not offered for new templates), restore them, and delete ones no template has used (decision 11).
 * A procedure keeps its own copy of each stage, so editing a definition changes templates, not existing procedures.
 */
export function StageDefinitionsPage({search, onSearchChange}: StageDefinitionsPageProps) {
  const {data = [], isPending, isError} = useQuery(stageDefinitionsQuery);
  const save = useSaveStageDefinition();
  const actions = useStageDefinitionActions();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<StageDefinitionInput>(EMPTY);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const {rows, page, total, archivedCount} = setupPage(data, search);
  const close = () => setDialog(null);
  const set = (key: keyof StageDefinitionInput) => (value: string) => setForm((current) => ({...current, [key]: value}));

  const openEditor = (definition: StageDefinition | null) => {
    setForm(
      definition
        ? {name: definition.name ?? '', description: definition.description ?? '', instructions: definition.instructions ?? ''}
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
    setError(saveProblem(e, action, 'stage definition').message);
    close();
  };

  const editing = dialog?.kind === 'edit' ? dialog.definition : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.definition : null;

  const saveDefinition = () =>
    save.mutate(
      {id: editing?.id, ...form},
      {onSuccess: close, onError: (e) => setProblem(saveProblem(e, editing ? 'save' : 'add', 'stage definition'))},
    );

  const columns: DataTableColumn<DefinitionRow>[] = [
    {
      key: 'name',
      header: 'Stage',
      width: proportional(3, {minWidth: 120}),
      renderCell: (d) => <SetupNameCell name={d.name ?? ''} secondary={d.description} />,
    },
    {key: 'templates', header: 'Used in', width: pixel(140), renderCell: (d) => <Text type="body">{templatesUsing(d.templateCount ?? 0)}</Text>},
    {key: 'status', header: 'Status', width: pixel(110), renderCell: (d) => <SetupStatusToken isArchived={d.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (d) => (
        <TableActions
          actions={[
            {label: 'Edit stage definition', icon: AppIcon.Edit, onClick: () => openEditor(d)},
            d.isArchived
              ? {
                  label: 'Restore stage definition',
                  icon: AppIcon.Restore,
                  onClick: () => actions.restore.mutate(d.id ?? '', {onError: failed('restore')}),
                }
              : {label: 'Archive stage definition', icon: AppIcon.Archive, onClick: () => open('archive', d)},
            {
              label: d.canBeDeleted ? 'Delete stage definition' : 'Used by a template, so it can be archived but not deleted',
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

  const subtitle = isPending
    ? 'Loading stage definitions...'
    : `${total} ${search.archived ? '' : 'active '}${total === 1 ? 'stage definition' : 'stage definitions'}`;

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Stage">{target.name}</MetadataListItem>
      <MetadataListItem label="Used in">{templatesUsing(target.templateCount ?? 0)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Stage Definitions"
      subtitle={subtitle}
      fill
      message={
        error ? <AlertMessage message={error} /> : isError && <AlertMessage message="The stage definitions could not be loaded. Please try again." />
      }
      actions={
        <>
          <Switch
            label={`Show archived (${archivedCount})`}
            value={search.archived ?? false}
            onChange={(archived) => onSearchChange({archived: archived || undefined})}
          />
          <Button label="Add Stage Definition" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />
        </>
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Stage Definition' : 'Add Stage Definition'}
            message={
              editing
                ? 'Templates use the changes from now on. Procedures already started keep the stage as it was.'
                : 'A step that templates can be built from.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Stage Definition'}
            isProcessing={save.isPending}
            confirmDisabled={!form.name.trim()}
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
              <TextArea
                label="Instructions"
                description="How staff carry the stage out."
                value={form.instructions}
                onChange={set('instructions')}
                rows={5}
                isRequired
                status={fieldStatus(problem.validation, 'instructions')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Stage Definition"
            message="It won't be offered for new templates. Templates and procedures that have it keep it, and it can be restored."
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
            message="No template uses this stage definition. This action cannot be undone."
            isDanger
            confirmText="Delete Stage Definition"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No stage definitions yet"
          description="Stage definitions are the steps templates are built from."
          icon={<Icon icon={AppIcon.StageDefinitions} size="lg" />}
          actions={<Button label="Add Your First Stage Definition" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable
          data={rows as DefinitionRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          pagination={{
            page,
            pageSize: PAGE_SIZE,
            totalItems: total,
            onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
          }}
          emptyState={
            <EmptyState
              title="Every stage definition is archived"
              description="Show archived ones to see and restore them."
              isCompact
              actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({archived: true})} />}
            />
          }
        />
      )}
    </Page>
  );
}

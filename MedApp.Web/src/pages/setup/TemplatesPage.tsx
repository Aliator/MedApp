import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import type {ProcedureTemplate} from '../../api/procedures';
import {allTemplatesQuery, useTemplateActions} from '../../api/procedureSetup';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/procedures/SetupCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {saveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';
import {useListSettings} from '../../app/listSettings';

type TemplateRow = ProcedureTemplate & Record<string, unknown>;
type DialogState = {kind: 'archive' | 'delete'; template: ProcedureTemplate} | null;

export interface TemplatesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** "7 stages: Referral received, Consultation, …", for the list's second line. */
export function stagesSummary(template: ProcedureTemplate): string {
  const stages = [...(template.stages ?? [])].sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0));
  if (stages.length === 0) return 'No stages';
  return `${count(stages.length, 'stage', 'stages')}: ${stages.map((s) => s.name).join(', ')}`;
}

/**
 * Templates (admin, decision 17): the ordered stages staff create procedures from. Admins add and edit them on
 * their own page, archive ones no longer wanted (New procedure stops offering them; procedures made from them keep
 * their stages), restore them, and delete ones never used.
 */
export function TemplatesPage({search, onSearchChange}: TemplatesPageProps) {
  const {data = [], isPending, isError} = useQuery(allTemplatesQuery);
  const actions = useTemplateActions();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [error, setError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);
  const open = (kind: 'archive' | 'delete', template: ProcedureTemplate) => {
    setError(null);
    setDialog({kind, template});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'template').message);
    close();
  };
  const target = dialog?.template;

  const columns: DataTableColumn<TemplateRow>[] = [
    {
      key: 'name',
      header: 'Template',
      width: proportional(3, {minWidth: 120}),
      renderCell: (t) => <SetupNameCell name={t.name ?? ''} secondary={stagesSummary(t)} />,
    },
    {
      key: 'procedures',
      header: 'Used by',
      width: pixel(140),
      renderCell: (t) => <Text type="body">{t.procedureCount ? count(t.procedureCount, 'procedure', 'procedures') : 'Not used yet'}</Text>,
    },
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (t) => <SetupStatusToken isArchived={t.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (t) => (
        <TableActions
          actions={[
            {label: 'Edit template', icon: AppIcon.Edit, href: `/admin/templates/${t.id}`},
            t.isArchived
              ? {label: 'Restore template', icon: AppIcon.Restore, onClick: () => actions.restore.mutate(t.id ?? '', {onError: failed('restore')})}
              : {label: 'Archive template', icon: AppIcon.Archive, onClick: () => open('archive', t)},
            {
              label: t.procedureCount ? 'Used by procedures, so it can be archived but not deleted' : 'Delete template',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: (t.procedureCount ?? 0) > 0,
              onClick: () => open('delete', t),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading templates...' : setupSubtitle(total, search, 'template', 'templates');
  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Template">{target.name}</MetadataListItem>
      <MetadataListItem label="Stages">{count(target.stages?.length ?? 0, 'stage', 'stages')}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Templates"
      subtitle={subtitle}
      fill
      message={error ? <AlertMessage message={error} /> : isError && <AlertMessage message="The templates could not be loaded. Please try again." />}
      actions={<Button label="New Template" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/admin/templates/new" />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Template"
            message="New procedures can't be made from it. Procedures already made from it keep their stages, and it can be restored."
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
            message="No procedure has been made from this template. This action cannot be undone."
            isDanger
            confirmText="Delete Template"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No templates yet"
          description="Staff create procedures from templates: an ordered list of stages."
          icon={<Icon icon={AppIcon.Templates} size="lg" />}
          actions={<Button label="Create Your First Template" variant="primary" href="/admin/templates/new" />}
        />
      ) : (
        <DataTable
          data={rows as TemplateRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          // The search: its name, and Active (to begin with) or Archived.
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="templates"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search templates"
              placeholder="Search templates by name, or filter by status..."
              textKey="name"
              favourites={SETUP_FAVOURITES}
              // Each quick filter's name is its token's (Status: is Active), and All's is the empty bar, so it isn't repeated beside the star.
              namesQuickFilters={false}
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
              title="No templates match your search"
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

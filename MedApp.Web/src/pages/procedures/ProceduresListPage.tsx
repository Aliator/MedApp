import {Button} from '@astryxdesign/core/Button';
import type {DateRange} from '@astryxdesign/core/DateRangeInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {patientQuery} from '../../api/patients';
import type {ProcedureSummary} from '../../api/procedures';
import {PAGE_SIZE, procedureSearchQuery} from '../../api/search';
import {SearchDialog, type SearchField, type SearchValues} from '../../components/modals/SearchDialog';
import {Page} from '../../components/pages/Page';
import {
  AssigneesCell,
  DueDateCell,
  PriorityToken,
  ProcedureCell,
  StageProgressCell,
  StatusToken,
  statusLabel,
} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {
  hasProcedureFilters,
  PROCEDURE_SORTS,
  PROCEDURE_STATUSES,
  TASK_PRIORITIES,
  type ProcedureSortColumn,
  type ProcedureStatus,
  type ProceduresListSearch,
  type TaskPriority,
} from '../listSearch';

const SEARCH_FIELDS: SearchField[] = [
  {type: 'text', key: 'q', label: 'Procedure', placeholder: 'Title, template or patient'},
  {type: 'select', key: 'status', label: 'Status', options: PROCEDURE_STATUSES.map((s) => ({value: s, label: statusLabel(s)}))},
  {type: 'select', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p}))},
  {type: 'dateRange', key: 'due', label: 'Due between', allowFuture: true},
  {type: 'checkbox', key: 'overdue', label: 'Overdue only'},
  {type: 'checkbox', key: 'mine', label: 'Assigned to me'},
  {type: 'checkbox', key: 'closed', label: 'Include closed', description: 'Completed, cancelled and archived procedures'},
];

type ProcedureRow = ProcedureSummary & Record<string, unknown>;

export interface ProceduresListPageProps {
  /** The page, sort and filters, from the URL. */
  search: ProceduresListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: ProceduresListSearch) => void;
  /** Show only this patient's procedures (the patient's procedures page). */
  patientId?: string;
}

/**
 * Procedures: every procedure, or one patient's, a page at a time from the API. Sortable, with a search dialog;
 * the page, sort and filters live in the URL. Completed, cancelled and archived ones are hidden unless asked for.
 */
export function ProceduresListPage({search, onSearchChange, patientId}: ProceduresListPageProps) {
  const page = search.page ?? 1;
  const sortColumn: ProcedureSortColumn = search.sort ?? 'Due';
  const ascending = search.asc ?? true;
  const isFiltered = hasProcedureFilters(search);

  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const {data, isPending, isError} = useQuery(
    procedureSearchQuery({
      Page: page,
      Sort: PROCEDURE_SORTS[sortColumn],
      Ascending: ascending,
      Search: search.q,
      PatientId: patientId,
      Statuses: search.status ? [search.status] : undefined,
      Priority: search.priority,
      OverdueOnly: search.overdue,
      AssignedToMe: search.mine,
      IncludeClosed: search.closed,
      DueFrom: search.dueFrom,
      DueTo: search.dueTo,
    }),
  );
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as ProcedureRow[];

  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [draft, setDraft] = useState<SearchValues>({});

  const openSearch = () => {
    setDraft({
      q: search.q ?? '',
      status: search.status ?? '',
      priority: search.priority ?? '',
      overdue: search.overdue ? 'true' : '',
      mine: search.mine ? 'true' : '',
      closed: search.closed ? 'true' : '',
      due: search.dueFrom && search.dueTo ? ({start: search.dueFrom, end: search.dueTo} as DateRange) : null,
    });
    setIsSearchOpen(true);
  };

  /** Keeps the sort; any change to the filters goes back to page 1. */
  const applySearch = (values: SearchValues) => {
    const text = (key: string) => {
      const value = values[key];
      return typeof value === 'string' && value.trim() ? value.trim() : undefined;
    };
    const flag = (key: string) => (text(key) === 'true' ? true : undefined);
    const due = values.due && typeof values.due === 'object' ? values.due : null;
    onSearchChange({
      sort: search.sort,
      asc: search.asc,
      q: text('q'),
      status: text('status') as ProcedureStatus | undefined,
      priority: text('priority') as TaskPriority | undefined,
      overdue: flag('overdue'),
      mine: flag('mine'),
      closed: flag('closed'),
      dueFrom: due?.start,
      dueTo: due?.end,
    });
    setIsSearchOpen(false);
  };

  const clearSearch = () => {
    onSearchChange({sort: search.sort, asc: search.asc});
    setIsSearchOpen(false);
  };

  const columns: DataTableColumn<ProcedureRow>[] = [
    {
      key: 'Procedure',
      header: 'Procedure',
      sortable: true,
      renderCell: (p) =>
        patientId ? (
          <ProcedureCell id={p.id ?? ''} title={p.title ?? ''} secondary={p.templateName ?? ''} />
        ) : (
          <ProcedureCell id={p.id ?? ''} title={p.title ?? ''} patientId={p.patientId ?? ''} patientName={p.patientName ?? ''} />
        ),
    },
    {key: 'Status', header: 'Status', sortable: true, renderCell: (p) => <StatusToken status={p.status ?? ''} />},
    {
      key: 'stage',
      header: 'Current stage',
      renderCell: (p) => (
        <StageProgressCell
          completed={p.completedStageCount ?? 0}
          total={p.stageCount ?? 0}
          currentStageName={p.currentStageName}
          status={p.status ?? ''}
        />
      ),
    },
    {
      key: 'Due',
      header: 'Due',
      sortable: true,
      renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
    },
    {key: 'Priority', header: 'Priority', sortable: true, renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />},
    {key: 'assigned', header: 'Assigned to', renderCell: (p) => <AssigneesCell assignees={p.assignees ?? []} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      renderCell: (p) => <TableActions actions={[{label: 'View procedure', icon: AppIcon.View, href: `/procedures/${p.id}`}]} />,
    },
  ];

  const noun = (n: number) => (n === 1 ? 'procedure' : 'procedures');
  const subtitle = isPending
    ? 'Loading procedures...'
    : isFiltered
      ? `${total} matching ${noun(total)}`
      : `${total} open ${noun(total)}`;
  const patientName = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : null;

  return (
    <Page
      title={patientId ? `Procedures for ${patientName ?? 'patient'}` : 'Procedures'}
      subtitle={subtitle}
      backHref={patientId ? `/patients/${patientId}` : undefined}
      backLabel="Back to patient"
      fill
      actions={
        <>
          {isFiltered && (
            <Button label="Clear Search" variant="ghost" icon={<Icon icon={AppIcon.Cancel} />} onClick={clearSearch} />
          )}
          <Button label="Search" variant="secondary" icon={<Icon icon={AppIcon.Search} />} onClick={openSearch} />
        </>
      }
      dialogs={
        <SearchDialog
          isOpen={isSearchOpen}
          title="Search Procedures"
          fields={SEARCH_FIELDS}
          values={draft}
          onValuesChange={setDraft}
          onSearch={() => applySearch(draft)}
          onClear={clearSearch}
          onClose={() => setIsSearchOpen(false)}
        />
      }>
      {isError && <AlertMessage message="The procedures could not be loaded. Please try again." />}
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={PAGE_SIZE}
        sort={{column: sortColumn, ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as ProcedureSortColumn, asc: sort.ascending})}
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          isFiltered ? (
            <EmptyState
              title="No procedures match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={clearSearch} />}
            />
          ) : (
            <EmptyState
              title={patientId ? 'This patient has no open procedures' : 'No open procedures'}
              description="Completed, cancelled and archived procedures are hidden. Search to include them."
              icon={<Icon icon={AppIcon.Procedures} size="lg" />}
            />
          )
        }
      />
    </Page>
  );
}

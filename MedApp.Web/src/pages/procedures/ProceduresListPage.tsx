import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {useQuery} from '@tanstack/react-query';
import {patientQuery} from '../../api/patients';
import type {ProcedureSummary} from '../../api/procedures';
import {procedureSearchQuery} from '../../api/search';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {
  AssigneesCell,
  DueDateCell,
  PriorityToken,
  PROCEDURE_COLUMN_WIDTH,
  ProcedureActions,
  ProcedureCell,
  StageProgressCell,
  StatusToken,
  statusLabel,
} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {TextCell} from '../../components/tables/TextCell';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField} from '../../components/tables/ListFilterBar';
import {
  choiceValues,
  hasProcedureFilters,
  PROCEDURE_SORTS,
  PROCEDURE_STATUSES,
  procedureStatuses,
  TASK_PRIORITIES,
  type ProcedureSortColumn,
  type ProceduresListSearch,
} from '../listSearch';
import {useListSettings} from '../../app/listSettings';

/** The search's fields, as the API filters procedures, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Procedure'},
  {kind: 'text', key: 'template', label: 'Template'},
  {kind: 'text', key: 'patient', label: 'Patient'},
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: PROCEDURE_STATUSES.map((s) => ({value: s, label: statusLabel(s)})),
    isMultiple: true,
  },
  {kind: 'choice', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p})), isMultiple: true},
  {kind: 'date', key: 'due', label: 'Due', fromKey: 'dueFrom', toKey: 'dueTo'},
  {kind: 'flag', key: 'overdue', label: 'Overdue', operator: 'only'},
  {kind: 'flag', key: 'mine', label: 'Assigned to me', operator: 'only'},
  {kind: 'flag', key: 'closed', label: 'Completed, cancelled and archived', operator: 'included'},
];

/** Quick look-ups: mine, and the ones needing attention. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'Assigned to me', values: {mine: true}},
  {label: 'Overdue', values: {overdue: true}},
  {label: 'Unassigned', values: {status: 'Unassigned'}},
  {label: 'Blocked', values: {status: 'Blocked'}},
  {label: 'In review', values: {status: 'InReview'}},
];

const FIELDS_FOR_ONE_PATIENT = FILTER_FIELDS.filter((field) => field.key !== 'patient');

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
 * Procedures: every procedure, or one patient's, a page at a time from the API. Sortable, with a power search
 * across the top; the page, sort and filters live in the URL. Completed, cancelled and archived ones are hidden unless asked for.
 */
/**
 * The list's page of procedures, for its search and list settings, and the patient on a patient's procedures (the
 * route's loader fetches it too).
 */
export function proceduresListQuery(
  search: ProceduresListSearch,
  {pageSize, startsOnMine}: {pageSize: number; startsOnMine: boolean},
  patientId?: string,
) {
  const mine = search.mine ?? startsOnMine;
  return procedureSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: PROCEDURE_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Title: search.title,
    TemplateName: search.template,
    PatientName: patientId ? undefined : search.patient,
    PatientId: patientId,
    Statuses: procedureStatuses(search),
    Priorities: choiceValues(search.priority, TASK_PRIORITIES),
    OverdueOnly: search.overdue,
    AssignedToMe: mine || undefined,
    IncludeClosed: search.closed,
    DueFrom: search.dueFrom,
    DueTo: search.dueTo,
  });
}

export function ProceduresListPage({search, onSearchChange, patientId}: ProceduresListPageProps) {
  const page = search.page ?? 1;
  const sortColumn: ProcedureSortColumn = search.sort ?? 'Due';
  const ascending = search.asc ?? true;
  // The list starts on "Assigned to me" if Account Settings says so, unless the URL says otherwise (`mine=false`).
  const {pageSize, startsOnMine} = useListSettings();
  const mine = search.mine ?? startsOnMine;
  const isFiltered = hasProcedureFilters({...search, mine: mine || undefined});

  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const {data, isPending, isError} = useQuery(proceduresListQuery(search, {pageSize, startsOnMine}, patientId));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as ProcedureRow[];

  /** Keeps the sort; any change to the filters goes back to page 1. */
  const clearSearch = () => onSearchChange({sort: search.sort, asc: search.asc});

  const allColumns: DataTableColumn<ProcedureRow>[] = [
    {
      key: 'Procedure',
      header: 'Procedure',
      sortable: true,
      width: PROCEDURE_COLUMN_WIDTH.procedure,
      renderCell: (p) => <ProcedureCell id={p.id ?? ''} title={p.title ?? ''} />,
    },
    // One patient's list shows each procedure's template; the full list, its patient (View patient is in the actions).
    patientId
      ? {key: 'templateName', header: 'Template', width: PROCEDURE_COLUMN_WIDTH.template, renderCell: (p) => <TextCell text={p.templateName} />}
      : {key: 'patientName', header: 'Patient', width: PROCEDURE_COLUMN_WIDTH.patient, renderCell: (p) => <TextCell text={p.patientName} />},
    {key: 'Status', header: 'Status', sortable: true, width: PROCEDURE_COLUMN_WIDTH.status, renderCell: (p) => <StatusToken status={p.status ?? ''} />},
    {
      key: 'stage',
      header: 'Current stage',
      width: PROCEDURE_COLUMN_WIDTH.stage,
      renderCell: (p) => (
        <StageProgressCell
          completed={p.completedStageCount ?? 0}
          total={p.stageCount ?? 0}
          currentStageName={p.currentStageName}
        />
      ),
    },
    {
      key: 'Due',
      header: 'Due',
      sortable: true,
      width: PROCEDURE_COLUMN_WIDTH.due,
      renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
    },
    {key: 'Priority', header: 'Priority', sortable: true, width: PROCEDURE_COLUMN_WIDTH.priority, renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />},
    {key: 'assigned', header: 'Assigned to', width: PROCEDURE_COLUMN_WIDTH.assigned, renderCell: (p) => <AssigneesCell assignees={p.assignees ?? []} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: PROCEDURE_COLUMN_WIDTH.actions,
      renderCell: (p) => <ProcedureActions id={p.id ?? ''} patientId={patientId ? undefined : (p.patientId ?? '')} />,
    },
  ];
  // Narrow (a narrow window or a half view), the table drops the current stage and the assignees to fit.
  const isNarrow = useIsNarrow();
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'stage' && c.key !== 'assigned') : allColumns;

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
      message={isError && <AlertMessage message="The procedures could not be loaded. Please try again." />}
      actions={
        patientId && (
          <Button label="New Procedure" variant="primary" icon={<Icon icon={AppIcon.Add} />} href={`/procedures/new?patientId=${patientId}`} />
        )
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        // The search, as a power search across the top of the card: a token per filter, in the URL like the page
        // and sort. Typing searches titles, templates and patients straight away.
        search={
          <ListFilterBar
            // One patient's procedures have no need of a patient filter.
            fields={patientId ? FIELDS_FOR_ONE_PATIENT : FILTER_FIELDS}
            values={{...search, mine: mine || undefined}}
            // Mine kept out of the URL when it's where the list starts anyway, and false there when it starts on it.
            onChange={(filters) =>
              onSearchChange({
                ...search,
                ...filters,
                mine: (filters.mine === true) === startsOnMine ? undefined : filters.mine === true,
                page: undefined,
              })
            }
            list={patientId ? 'patientProcedures' : 'procedures'}
            isUnfiltered={!hasProcedureFilters(search) && search.mine === undefined}
            label="Search procedures"
            placeholder="Search procedures by title, or filter by template, patient, status, priority, due date..."
            textKey="title"
            favourites={FAVOURITES}
          />
        }
        sort={{column: sortColumn, ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as ProcedureSortColumn, asc: sort.ascending})}
        pagination={{
          page,
          pageSize,
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

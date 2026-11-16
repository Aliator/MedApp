import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {useQuery} from '@tanstack/react-query';
import type {ProcedureSummary} from '../../api/procedures';
import {procedureSearchQuery, type ProcedureSearch} from '../../api/search';
import {Page} from '../../components/pages/Page';
import {
  DueDateCell,
  PROCEDURE_COLUMN_WIDTH,
  PriorityToken,
  ProcedureActions,
  ProcedureCell,
  StageProgressCell,
  statusLabel,
} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {
  ListFilterBar,
  type ListFilterFavourite,
  type ListFilterField,
  type ListFilterValues,
} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {
  choiceValues,
  hasTaskFilters,
  OPEN_PROCEDURE_STATUSES,
  TASK_PRIORITIES,
  TASK_SORTS,
  type TaskSortColumn,
  type TasksSearch,
} from '../listSearch';
import {useListSettings} from '../../app/listSettings';

type TaskRow = ProcedureSummary & Record<string, unknown>;

/** How far along the list's tasks are: its progress filter, Not started unless the URL says otherwise. */
type Progress = 'notStarted' | 'started' | 'any';
const progressOf = (search: TasksSearch): Progress => search.progress ?? 'notStarted';

const procedureColumn: DataTableColumn<TaskRow> = {
  key: 'Procedure',
  header: 'Procedure',
  sortable: true,
  width: PROCEDURE_COLUMN_WIDTH.procedure,
  renderCell: (p) => <ProcedureCell id={p.id ?? ''} title={p.title ?? ''} />,
};

/** The patient's name, on one line (truncated); View patient is in the actions. */
const patientColumn: DataTableColumn<TaskRow> = {
  key: 'patientName',
  header: 'Patient',
  width: PROCEDURE_COLUMN_WIDTH.patient,
  renderCell: (p) => <TextCell text={p.patientName} />,
};

const templateColumn: DataTableColumn<TaskRow> = {
  key: 'templateName',
  header: 'Template',
  width: PROCEDURE_COLUMN_WIDTH.template,
  renderCell: (p) => <TextCell text={p.templateName} />,
};

const stageColumn: DataTableColumn<TaskRow> = {
  key: 'stage',
  header: 'Current stage',
  width: PROCEDURE_COLUMN_WIDTH.stage,
  renderCell: (p) => (
    <StageProgressCell
      completed={p.completedStageCount ?? 0}
      total={p.stageCount ?? 0}
      currentStageName={p.currentStageName}
      // There's no status column here, so the cell says when a procedure is blocked or in review.
      status={p.status ?? ''}
    />
  ),
};

const dueColumn: DataTableColumn<TaskRow> = {
  key: 'Due',
  header: 'Due',
  sortable: true,
  width: PROCEDURE_COLUMN_WIDTH.due,
  renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
};

const priorityColumn: DataTableColumn<TaskRow> = {
  key: 'Priority',
  header: 'Priority',
  sortable: true,
  width: PROCEDURE_COLUMN_WIDTH.priority,
  renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />,
};

const actionsColumn: DataTableColumn<TaskRow> = {
  key: 'actions',
  header: 'Actions',
  align: 'end',
  width: PROCEDURE_COLUMN_WIDTH.actions,
  renderCell: (p) => <ProcedureActions id={p.id ?? ''} patientId={p.patientId ?? ''} />,
};

/** Tasks not started show their template (there's no stage to show yet); the others, the stage they're at. */
const columnsFor = (progress: Progress) => [
  procedureColumn,
  patientColumn,
  progress === 'notStarted' ? templateColumn : stageColumn,
  dueColumn,
  priorityColumn,
  actionsColumn,
];

/** What the list says for each progress: its subtitle's words, and when there's nothing to show. */
const PROGRESS_TEXT: Record<Progress, {noun: string; empty: string}> = {
  notStarted: {noun: 'not started', empty: 'Nothing waiting to be started'},
  started: {noun: 'started', empty: 'Nothing in progress'},
  any: {noun: 'to do', empty: 'Nothing assigned to you'},
};

/** The search's fields, as the API filters a user's tasks, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Procedure'},
  {kind: 'text', key: 'template', label: 'Template'},
  {kind: 'text', key: 'patient', label: 'Patient'},
  {
    kind: 'choice',
    key: 'progress',
    label: 'Progress',
    options: [
      {value: 'notStarted', label: 'Not started'},
      {value: 'started', label: 'Started'},
    ],
  },
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: OPEN_PROCEDURE_STATUSES.map((s) => ({value: s, label: statusLabel(s)})),
    isMultiple: true,
  },
  {kind: 'choice', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p})), isMultiple: true},
  {kind: 'date', key: 'due', label: 'Due', fromKey: 'dueFrom', toKey: 'dueTo'},
  {kind: 'flag', key: 'overdue', label: 'Overdue', operator: 'only'},
];

/** The quick look-ups: the tasks not started, those under way, and (started or not) the ones needing attention. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'Not started', values: {progress: 'notStarted'}},
  {label: 'Started', values: {progress: 'started'}},
  {label: 'Overdue', values: {overdue: true}},
  {label: 'Blocked', values: {status: 'Blocked'}},
];

/** The API search behind the list: unfinished procedures assigned to me, with its filters. */
export function taskQuery(search: TasksSearch, pageSize?: number): ProcedureSearch {
  const progress = progressOf(search);
  return {
    AssignedToMe: true,
    Started: progress === 'any' ? undefined : progress === 'started',
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: TASK_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Title: search.title,
    TemplateName: search.template,
    PatientName: search.patient,
    Statuses: choiceValues(search.status, OPEN_PROCEDURE_STATUSES),
    Priorities: choiceValues(search.priority, TASK_PRIORITIES),
    OverdueOnly: search.overdue,
    DueFrom: search.dueFrom,
    DueTo: search.dueTo,
  };
}

/**
 * The bar's values from the URL, and back: the progress filter shows as Not started when the URL has none (the
 * default), and removing it is `any` (both).
 */
const barValues = (search: TasksSearch) => {
  const progress = progressOf(search);
  return {...search, progress: progress === 'any' ? undefined : progress};
};
const fromBar = (values: ListFilterValues): Partial<TasksSearch> => ({
  ...(values as Partial<TasksSearch>),
  progress: values.progress === undefined ? 'any' : values.progress === 'started' ? 'started' : undefined,
});

export interface MyTasksPageProps {
  /** The page, sort and filters, from the URL. */
  search: TasksSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: TasksSearch) => void;
}

/**
 * My Tasks: the signed-in user's unfinished procedures, as a list like the others: a power search across the top
 * (Not started to begin with; Not started and Started are quick filters, not named beside the star, as the bar's
 * token already shows them; a saved search still is), sortable columns and page controls. The
 * same in a half view.
 */
export function MyTasksPage({search, onSearchChange}: MyTasksPageProps) {
  const progress = progressOf(search);
  const page = search.page ?? 1;
  const sortColumn: TaskSortColumn = search.sort ?? 'Due';
  const ascending = search.asc ?? true;
  const isFiltered = hasTaskFilters(search);

  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(procedureSearchQuery(taskQuery(search, pageSize)));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as TaskRow[];

  /** Keeps the sort; back to Not started, with no other filters. */
  const clearSearch = () => onSearchChange({sort: search.sort, asc: search.asc});

  const noun = total === 1 ? 'procedure' : 'procedures';
  const subtitle = isPending
    ? 'Loading...'
    : isFiltered
      ? `${total} matching ${noun}`
      : `${total} ${noun} ${PROGRESS_TEXT[progress].noun}`;

  return (
    <Page
      title="My Tasks"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage message="Your tasks could not be loaded. Please try again." />}>
      <DataTable
        data={rows}
        columns={columnsFor(progress)}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={barValues(search)}
            onChange={(values) => onSearchChange({...search, ...fromBar(values), page: undefined})}
            list="tasks"
            isUnfiltered={!hasTaskFilters(search) && search.progress === undefined}
            label="Search my tasks"
            placeholder="Search my tasks by procedure, or filter by progress, patient, priority, due date..."
            textKey="title"
            favourites={FAVOURITES}
            namesQuickFilters={false}
          />
        }
        sort={{column: sortColumn, ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as TaskSortColumn, asc: sort.ascending})}
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
            <EmptyState title={PROGRESS_TEXT[progress].empty} icon={<Icon icon={AppIcon.Success} size="lg" />} />
          )
        }
      />
    </Page>
  );
}

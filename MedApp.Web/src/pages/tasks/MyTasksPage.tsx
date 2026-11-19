import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import type {PathwaySummary} from '../../api/pathways';
import {pathwaySearchQuery, type PathwaySearch} from '../../api/search';
import {Page} from '../../components/pages/Page';
import {
  DueDateCell,
  PATHWAY_COLUMN_WIDTH,
  PriorityToken,
  PathwayActions,
  PathwayCell,
  StageProgressCell,
  statusLabel,
} from '../../components/pathways/PathwayCells';
import {WaitCell} from '../../components/referrals/WaitingClock';
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
  idChoiceValues,
  OPEN_PATHWAY_STATUSES,
  TASK_PRIORITIES,
  TASK_SORTS,
  TASK_VIEWS,
  type TaskSortColumn,
  type TasksSearch,
  type TaskView,
} from '../listSearch';
import {useListSettings} from '../../app/listSettings';
import {useTeamAndStageFields} from '../pathways/pathwayFilterFields';

type TaskRow = PathwaySummary & Record<string, unknown>;

/** How far along the list's tasks are: its progress filter, Not started unless the URL says otherwise. */
type Progress = 'notStarted' | 'started' | 'any';
const progressOf = (search: TasksSearch): Progress => search.progress ?? 'notStarted';

const pathwayColumn: DataTableColumn<TaskRow> = {
  key: 'Pathway',
  header: 'Pathway',
  sortable: true,
  width: PATHWAY_COLUMN_WIDTH.pathway,
  renderCell: (p) => <PathwayCell id={p.id ?? ''} title={p.title ?? ''} />,
};

/** The patient's name, on one line (truncated); View patient is in the actions. */
const patientColumn: DataTableColumn<TaskRow> = {
  key: 'patientName',
  header: 'Patient',
  width: PATHWAY_COLUMN_WIDTH.patient,
  renderCell: (p) => <TextCell text={p.patientName} />,
};

const templateColumn: DataTableColumn<TaskRow> = {
  key: 'templateName',
  header: 'Template',
  width: PATHWAY_COLUMN_WIDTH.template,
  renderCell: (p) => <TextCell text={p.templateName} />,
};

const stageColumn: DataTableColumn<TaskRow> = {
  key: 'stage',
  header: 'Current stage',
  width: PATHWAY_COLUMN_WIDTH.stage,
  renderCell: (p) => (
    <StageProgressCell
      completed={p.completedStageCount ?? 0}
      skipped={p.skippedStageCount ?? 0}
      total={p.stageCount ?? 0}
      currentStageName={p.currentStageName}
      // There's no status column here, so the cell says when a pathway is on hold or awaiting review.
      status={p.status ?? ''}
    />
  ),
};

const dueColumn: DataTableColumn<TaskRow> = {
  key: 'Due',
  header: 'Target date',
  sortable: true,
  width: PATHWAY_COLUMN_WIDTH.due,
  renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
};

/** The referral's waiting-time clock, for a pathway from a referral; sortable by days waited. */
const waitColumn: DataTableColumn<TaskRow> = {
  key: 'Wait',
  header: 'Wait',
  sortable: true,
  width: PATHWAY_COLUMN_WIDTH.wait,
  renderCell: (p) => <WaitCell clock={p.clock} />,
};

const priorityColumn: DataTableColumn<TaskRow> = {
  key: 'Priority',
  header: 'Priority',
  sortable: true,
  width: PATHWAY_COLUMN_WIDTH.priority,
  renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />,
};

const actionsColumn: DataTableColumn<TaskRow> = {
  key: 'actions',
  header: 'Actions',
  align: 'end',
  width: PATHWAY_COLUMN_WIDTH.actions,
  renderCell: (p) => <PathwayActions id={p.id ?? ''} patientId={p.patientId ?? ''} />,
};

/**
 * Who works the current stage (the relay): the lab, or the pathway's own team; "Not picked up" on a line of its own
 * under the team, so a narrow column cuts off neither.
 */
const withColumn: DataTableColumn<TaskRow> = {
  key: 'with',
  header: 'With',
  width: PATHWAY_COLUMN_WIDTH.template,
  renderCell: (p) => (
    <VStack gap={0}>
      <TextCell text={p.currentStageTeamName} />
      {p.awaitingPickUp && (
        <Text type="supporting" color="secondary" maxLines={1}>
          Not picked up
        </Text>
      )}
    </VStack>
  ),
};

/**
 * Tasks not started show their template (there's no stage to show yet); the others, the stage they're at. A team view
 * shows the stage and who has it.
 */
const columnsFor = (progress: Progress, view: TaskView | undefined) => [
  pathwayColumn,
  patientColumn,
  ...(view ? [stageColumn, withColumn] : [progress === 'notStarted' ? templateColumn : stageColumn]),
  dueColumn,
  waitColumn,
  priorityColumn,
  actionsColumn,
];

/** What the list says for each progress: its subtitle's words, and when there's nothing to show. */
const PROGRESS_TEXT: Record<Progress, {noun: string; empty: string}> = {
  notStarted: {noun: 'not started', empty: 'Nothing waiting to be started'},
  started: {noun: 'started', empty: 'Nothing in progress'},
  any: {noun: 'to do', empty: 'Nothing assigned to you'},
};

/** The team views' words: the bar's and favourites' labels, and the list's when there's nothing in one. */
const VIEW_TEXT: Record<TaskView, {label: string; empty: string}> = {
  ourTurn: {label: 'With my team', empty: 'Nothing is with your team'},
  newForUs: {label: 'To pick up', empty: 'Nothing waiting to be picked up'},
  awaitingLab: {label: 'Awaiting the lab', empty: 'Nothing is with the lab'},
  backFromLab: {label: 'Back from the lab', empty: 'Nothing back from the lab'},
};
const VIEWS = Object.keys(TASK_VIEWS) as TaskView[];

/** The search's fields, as the API filters a user's tasks, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Pathway'},
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
  {kind: 'choice', key: 'view', label: 'Team view', options: VIEWS.map((v) => ({value: v, label: VIEW_TEXT[v].label}))},
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: OPEN_PATHWAY_STATUSES.map((s) => ({value: s, label: statusLabel(s)})),
    isMultiple: true,
  },
  {kind: 'choice', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p})), isMultiple: true},
  {kind: 'date', key: 'due', label: 'Target date', fromKey: 'dueFrom', toKey: 'dueTo'},
  {kind: 'flag', key: 'overdue', label: 'Overdue', operator: 'only'},
  {kind: 'flag', key: 'breached', label: 'Breached', operator: 'only'},
];

/** The quick look-ups: the tasks not started, those under way, and (started or not) the ones needing attention. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'Not started', values: {progress: 'notStarted'}},
  {label: 'Started', values: {progress: 'started'}},
  {label: 'Overdue', values: {overdue: true}},
  {label: 'Breached', values: {breached: true}},
  {label: 'On hold', values: {status: 'Blocked'}},
  // The relay's team views: work passed between the clinic and the lab.
  ...VIEWS.map((view) => ({label: VIEW_TEXT[view].label, values: {view}})),
];

/**
 * The API search behind the list: unfinished pathways assigned to me, with its filters; or, in a team view, the team's
 * pathways in it (whoever they're assigned to, started or not).
 */
export function taskQuery(search: TasksSearch, pageSize?: number): PathwaySearch {
  const progress = progressOf(search);
  if (search.view) return {...taskQuery({...search, view: undefined, progress: 'any'}, pageSize), AssignedToMe: undefined, View: TASK_VIEWS[search.view]};
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
    Statuses: choiceValues(search.status, OPEN_PATHWAY_STATUSES),
    Priorities: choiceValues(search.priority, TASK_PRIORITIES),
    TeamIds: idChoiceValues(search.team),
    StageDefinitionIds: idChoiceValues(search.stage),
    OverdueOnly: search.overdue,
    BreachedOnly: search.breached,
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
  // A team view isn't about my progress, so the progress filter doesn't show with one.
  return {...search, progress: progress === 'any' || search.view ? undefined : progress};
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
 * My Worklist: the signed-in user's unfinished pathways, as a list like the others: a power search across the top
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
  const {data, isPending, isError} = useQuery(pathwaySearchQuery(taskQuery(search, pageSize)));
  const teamAndStage = useTeamAndStageFields(search);
  const fields = useMemo(() => [...FILTER_FIELDS, ...teamAndStage], [teamAndStage]);
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as TaskRow[];

  /** Keeps the sort; back to Not started, with no other filters. */
  const clearSearch = () => onSearchChange({sort: search.sort, asc: search.asc});

  const noun = total === 1 ? 'pathway' : 'pathways';
  const view = search.view;
  const subtitle = isPending
    ? 'Loading...'
    : view
      ? `${total} ${noun}: ${VIEW_TEXT[view].label}`
      : isFiltered
        ? `${total} matching ${noun}`
        : `${total} ${noun} ${PROGRESS_TEXT[progress].noun}`;

  return (
    <Page
      title="My Worklist"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage label="Worklist not loaded" message="Your worklist could not be loaded. Please try again." />}>
      <DataTable
        data={rows}
        columns={columnsFor(progress, view)}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={fields}
            values={barValues(search)}
            onChange={(values) => onSearchChange({...search, ...fromBar(values), page: undefined})}
            list="tasks"
            isUnfiltered={!hasTaskFilters(search) && search.progress === undefined}
            label="Search my worklist"
            placeholder="Search my worklist by pathway, or filter by progress, patient, priority, target date..."
            textKey="title"
            favourites={FAVOURITES}
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
          view && !hasTaskFilters({...search, view: undefined}) ? (
            <EmptyState title={VIEW_TEXT[view].empty} icon={<Icon icon={AppIcon.Success} size="lg" />} />
          ) : isFiltered ? (
            <EmptyState
              title="No pathways match your search"
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

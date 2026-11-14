import {Badge} from '@astryxdesign/core/Badge';
import {Button} from '@astryxdesign/core/Button';
import {ClickableCard} from '@astryxdesign/core/ClickableCard';
import type {DateRange} from '@astryxdesign/core/DateRangeInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {useMediaQuery} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import type {ProcedureSummary} from '../../api/procedures';
import {PAGE_SIZE, procedureSearchQuery, type ProcedureSearch} from '../../api/search';
import {SearchDialog, type SearchField, type SearchValues} from '../../components/modals/SearchDialog';
import {NARROW_WINDOW_QUERY, Page} from '../../components/pages/Page';
import {PageHeader} from '../../components/pages/PageHeader';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {DueDateCell, PriorityToken, ProcedureCell, StageProgressCell} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {
  hasTaskFilters,
  TASK_PRIORITIES,
  TASK_SORTS,
  taskListSearch,
  withTaskListSearch,
  type HomeSearch,
  type TaskListKind,
  type TaskListSearch,
  type TaskPriority,
  type TaskSortColumn,
} from '../listSearch';

type TaskRow = ProcedureSummary & Record<string, unknown>;

const procedureColumn: DataTableColumn<TaskRow> = {
  key: 'Procedure',
  header: 'Procedure',
  sortable: true,
  renderCell: (p) => (
    <ProcedureCell id={p.id ?? ''} title={p.title ?? ''} patientId={p.patientId ?? ''} patientName={p.patientName ?? ''} />
  ),
};

const dueColumn: DataTableColumn<TaskRow> = {
  key: 'Due',
  header: 'Due',
  sortable: true,
  renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
};

const priorityColumn: DataTableColumn<TaskRow> = {
  key: 'Priority',
  header: 'Priority',
  sortable: true,
  renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />,
};

const LISTS: Record<TaskListKind, {title: string; noun: string; columns: DataTableColumn<TaskRow>[]; empty: string}> = {
  notStarted: {
    title: 'Not started',
    noun: 'not started',
    empty: 'Nothing waiting to be started',
    columns: [
      procedureColumn,
      {key: 'template', header: 'Template', renderCell: (p) => <Text type="body">{p.templateName}</Text>},
      dueColumn,
      priorityColumn,
    ],
  },
  started: {
    title: 'Started',
    noun: 'started',
    empty: 'Nothing in progress',
    columns: [
      procedureColumn,
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
      dueColumn,
      priorityColumn,
    ],
  },
};

const SEARCH_FIELDS: SearchField[] = [
  {type: 'text', key: 'q', label: 'Procedure', placeholder: 'Title, template or patient'},
  {type: 'select', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p}))},
  {type: 'checkbox', key: 'overdue', label: 'Overdue only'},
  {type: 'dateRange', key: 'due', label: 'Due between', allowFuture: true},
];

/** The API search behind one list: unfinished procedures assigned to me, started or not, with the list's filters. */
export function taskQuery(kind: TaskListKind, search: TaskListSearch): ProcedureSearch {
  return {
    AssignedToMe: true,
    Started: kind === 'started',
    Page: search.page ?? 1,
    Sort: TASK_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Search: search.q,
    Priority: search.priority,
    OverdueOnly: search.overdue,
    DueFrom: search.dueFrom,
    DueTo: search.dueTo,
  };
}

interface TaskListProps {
  kind: TaskListKind;
  search: TaskListSearch;
  onSearchChange: (search: TaskListSearch) => void;
  /** On a narrow window, the switch to the other list, inside the table's card on one edge. */
  edge?: {side: 'start' | 'end'; content: ReactNode};
}

/** One task list, built like the patients list: count, search dialog, sortable columns and page controls. */
function TaskList({kind, search, onSearchChange, edge}: TaskListProps) {
  const list = LISTS[kind];
  const page = search.page ?? 1;
  const sortColumn: TaskSortColumn = search.sort ?? 'Due';
  const ascending = search.asc ?? true;
  const isFiltered = hasTaskFilters(search);

  const {data, isPending, isError} = useQuery(procedureSearchQuery(taskQuery(kind, search)));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as TaskRow[];

  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [draft, setDraft] = useState<SearchValues>({});

  const openSearch = () => {
    setDraft({
      q: search.q ?? '',
      priority: search.priority ?? '',
      overdue: search.overdue ? 'true' : '',
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
    const due = values.due && typeof values.due === 'object' ? values.due : null;
    onSearchChange({
      sort: search.sort,
      asc: search.asc,
      q: text('q'),
      priority: text('priority') as TaskPriority | undefined,
      overdue: text('overdue') === 'true' ? true : undefined,
      dueFrom: due?.start,
      dueTo: due?.end,
    });
    setIsSearchOpen(false);
  };

  const clearSearch = () => {
    onSearchChange({sort: search.sort, asc: search.asc});
    setIsSearchOpen(false);
  };

  const subtitle = isPending
    ? 'Loading...'
    : isFiltered
      ? `${total} matching ${total === 1 ? 'procedure' : 'procedures'}`
      : `${total} ${total === 1 ? 'procedure' : 'procedures'} ${list.noun}`;

  return (
    <VStack gap={4} height="100%">
      <PageHeader
        level={2}
        title={list.title}
        subtitle={subtitle}
        actions={
          <>
            {isFiltered && (
              <Button label="Clear Search" variant="ghost" icon={<Icon icon={AppIcon.Cancel} />} onClick={clearSearch} />
            )}
            <Button label="Search" variant="secondary" icon={<Icon icon={AppIcon.Search} />} onClick={openSearch} />
          </>
        }
      />
      {isError && <AlertMessage message={`The ${list.noun} procedures could not be loaded. Please try again.`} />}
      <StackItem size="fill">
        <DataTable
          data={rows}
          columns={list.columns}
          idKey="id"
          isLoading={isPending}
          skeletonRows={PAGE_SIZE}
          edge={edge}
          sort={{column: sortColumn, ascending}}
          onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as TaskSortColumn, asc: sort.ascending})}
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
              <EmptyState title={list.empty} icon={<Icon icon={AppIcon.Success} size="lg" />} />
            )
          }
        />
      </StackItem>
      <SearchDialog
        isOpen={isSearchOpen}
        title={`Search ${list.noun} procedures`}
        fields={SEARCH_FIELDS}
        values={draft}
        onValuesChange={setDraft}
        onSearch={() => applySearch(draft)}
        onClear={clearSearch}
        onClose={() => setIsSearchOpen(false)}
      />
    </VStack>
  );
}

/**
 * The switch to the other list on a narrow window: the full height of the table's card, along one edge. It
 * shows that list's name and how many procedures it has (with its own filters).
 */
function ListSwitch({kind, side, search, onClick}: {kind: TaskListKind; side: 'start' | 'end'; search: TaskListSearch; onClick: () => void}) {
  const {data} = useQuery(procedureSearchQuery(taskQuery(kind, search)));
  const title = LISTS[kind].title;

  return (
    <ClickableCard
      label={`Show ${title.toLowerCase()} procedures`}
      onClick={onClick}
      variant="transparent"
      height="100%"
      width="100%"
      padding={1}
      // It sits in the corner of the table's card, above the page controls, so its hover highlight is rounded only
      // at the card's top corner and square elsewhere. ClickableCard has no prop for per-corner radius.
      style={{
        borderRadius: side === 'end' ? '0 var(--radius-container) 0 0' : 'var(--radius-container) 0 0 0',
      }}>
      <VStack gap={3} height="100%" hAlign="center" vAlign="center">
        <Icon icon={side === 'end' ? AppIcon.ChevronRight : AppIcon.ChevronLeft} />
        {/* Vertical text: no Astryx prop for writing mode, so this one layout style is set directly. */}
        <Text type="body" weight="semibold" style={{writingMode: 'vertical-rl'}}>
          {title}
        </Text>
        {data && <Badge label={data.totalCount ?? 0} />}
      </VStack>
    </ClickableCard>
  );
}

export interface TaskBoardProps {
  /** Both lists' page, sort and filters, and which one a narrow window shows, from the URL. */
  search: HomeSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: HomeSearch) => void;
  /** One list at a time, with a switch, rather than both side by side. */
  isNarrow: boolean;
}

/** Home's two task lists: side by side and the same size, or one at a time on a narrow window. */
export function TaskBoard({search, onSearchChange, isNarrow}: TaskBoardProps) {
  const listFor = (kind: TaskListKind, edge?: TaskListProps['edge']) => (
    <TaskList
      kind={kind}
      search={taskListSearch(search, kind)}
      onSearchChange={(next) => onSearchChange(withTaskListSearch(search, kind, next))}
      edge={edge}
    />
  );

  if (!isNarrow) {
    return (
      <TwoColumns start={listFor('notStarted')} end={listFor('started')} />
    );
  }

  // The started list sits to the right of the not started one, so its switch is on the right edge, and back.
  const showing: TaskListKind = search.show === 'started' ? 'started' : 'notStarted';
  const other: TaskListKind = showing === 'started' ? 'notStarted' : 'started';
  const side = other === 'started' ? 'end' : 'start';

  return listFor(showing, {
    side,
    content: (
      <ListSwitch
        kind={other}
        side={side}
        search={taskListSearch(search, other)}
        onClick={() => onSearchChange({...search, show: other === 'started' ? 'started' : undefined})}
      />
    ),
  });
}

export type HomePageProps = Omit<TaskBoardProps, 'isNarrow'>;

/**
 * Home: the signed-in user's task list. Unfinished procedures assigned to them, in two lists that fill the
 * page: those not started yet, and those under way (including blocked and in-review ones).
 */
export function HomePage(props: HomePageProps) {
  const isNarrow = useMediaQuery(NARROW_WINDOW_QUERY, false);

  return (
    <Page title="My tasks" subtitle="Unfinished procedures assigned to you, soonest due first." fill>
      <TaskBoard {...props} isNarrow={isNarrow} />
    </Page>
  );
}

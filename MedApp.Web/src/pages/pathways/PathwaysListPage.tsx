import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import {patientQuery} from '../../api/patients';
import type {PathwaySummary} from '../../api/pathways';
import {allTemplatesQuery} from '../../api/pathwaySetup';
import {pathwaySearchQuery} from '../../api/search';
import {staffQuery} from '../../api/calendar';
import {useCan} from '../../auth/auth';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {
  AssigneesCell,
  DueDateCell,
  PriorityToken,
  PATHWAY_COLUMN_WIDTH,
  PathwayActions,
  PathwayCell,
  StageProgressCell,
  StatusToken,
  statusLabel,
} from '../../components/pathways/PathwayCells';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {WaitCell} from '../../components/referrals/WaitingClock';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {TextCell} from '../../components/tables/TextCell';
import {useClinicPicker} from '../../components/teams/ClinicPicker';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField} from '../../components/tables/ListFilterBar';
import {
  choiceValues,
  hasPathwayFilters,
  idChoiceValues,
  NOBODY,
  PATHWAY_SORTS,
  PATHWAY_STATUSES,
  pathwayStatuses,
  TASK_PRIORITIES,
  type PathwaySortColumn,
  type PathwaysListSearch,
} from '../listSearch';
import {useListSettings} from '../../app/listSettings';
import {useTeamAndStageFields} from './pathwayFilterFields';

/** The search's fields, as the API filters pathways, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Pathway'},
  {kind: 'text', key: 'template', label: 'Template'},
  {kind: 'text', key: 'patient', label: 'Patient'},
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: PATHWAY_STATUSES.map((s) => ({value: s, label: statusLabel(s)})),
    isMultiple: true,
  },
  {kind: 'choice', key: 'priority', label: 'Priority', options: TASK_PRIORITIES.map((p) => ({value: p, label: p})), isMultiple: true},
  {kind: 'date', key: 'due', label: 'Target date', fromKey: 'dueFrom', toKey: 'dueTo'},
  {kind: 'flag', key: 'overdue', label: 'Overdue', operator: 'only'},
  {kind: 'flag', key: 'breached', label: 'Breached', operator: 'only'},
  {kind: 'flag', key: 'mine', label: 'Assigned to me', operator: 'only'},
  {kind: 'flag', key: 'closed', label: 'Discharged, cancelled and archived', operator: 'included'},
];

/**
 * Who a pathway can be filtered as assigned to: nobody, and each member of staff (the calendar's staff list, which
 * anyone signed in can read). A person not in the list (an account since deleted) still shows, as "Selected person".
 */
function assigneeField(users: readonly {userId?: string; username?: string | null}[], current: string | undefined): ListFilterField {
  const options = [
    {value: NOBODY, label: 'Nobody'},
    ...users.map((u) => ({value: (u.userId ?? '').toLowerCase(), label: u.username ?? ''})),
  ];
  if (current && !options.some((o) => o.value === current)) options.push({value: current, label: 'Selected person'});
  return {kind: 'choice', key: 'assignee', label: 'Assigned to', options};
}

/**
 * Which template pathways were made from, by its ID (so a renamed template still finds them all): the templates by
 * their names now. One not in the list still shows, as "Selected template".
 */
function templateField(templates: readonly {id?: string; name?: string | null}[], current: string | undefined): ListFilterField {
  const options = templates.map((t) => ({value: (t.id ?? '').toLowerCase(), label: t.name ?? ''}));
  if (current && !options.some((o) => o.value === current)) options.push({value: current, label: 'Selected template'});
  return {kind: 'choice', key: 'templateId', label: 'From template', options};
}

/** Its tabs: every pathway, mine, and the ones needing attention. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'All', values: {}},
  {label: 'Assigned to me', values: {mine: true}},
  {label: 'Overdue', values: {overdue: true}},
  {label: 'Breached', values: {breached: true}},
  {label: 'Unassigned', values: {status: 'Unassigned'}},
  {label: 'On hold', values: {status: 'Blocked'}},
  {label: 'Awaiting review', values: {status: 'InReview'}},
];

const FIELDS_FOR_ONE_PATIENT = FILTER_FIELDS.filter((field) => field.key !== 'patient');

type PathwayRow = PathwaySummary & Record<string, unknown>;

export interface PathwaysListPageProps {
  /** The page, sort and filters, from the URL. */
  search: PathwaysListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: PathwaysListSearch) => void;
  /** Show only this patient's pathways (the patient's pathways page). */
  patientId?: string;
}

/**
 * Pathways: every pathway, or one patient's, a page at a time from the API. Sortable, with a power search
 * across the top; the page, sort and filters live in the URL. Discharged, cancelled and archived ones are hidden unless asked for.
 * The Wait column is the referral's waiting-time clock ("Day 84 of 126", Breached, Paused), sortable by days waited;
 * a pathway created directly has none. Breached only is a filter, like Overdue only.
 */
/**
 * The list's page of pathways, for its search and list settings, the patient on a patient's pathways, and the clinic
 * picker's team on the full list (the route's loader fetches it too).
 */
export function pathwaysListQuery(
  search: PathwaysListSearch,
  {pageSize, startsOnMine}: {pageSize: number; startsOnMine: boolean},
  patientId?: string,
  teamId?: string,
) {
  const mine = search.mine ?? startsOnMine;
  return pathwaySearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: PATHWAY_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Title: search.title,
    TemplateName: search.template,
    PatientName: patientId ? undefined : search.patient,
    PatientId: patientId,
    Statuses: pathwayStatuses(search),
    Priorities: choiceValues(search.priority, TASK_PRIORITIES),
    OverdueOnly: search.overdue,
    BreachedOnly: search.breached,
    AssignedToMe: mine || undefined,
    AssignedToUserId: search.assignee && search.assignee !== NOBODY ? search.assignee : undefined,
    NobodyAssigned: search.assignee === NOBODY || undefined,
    TemplateId: search.templateId,
    TeamIds: idChoiceValues(search.team),
    TeamId: patientId ? undefined : teamId,
    StageDefinitionIds: idChoiceValues(search.stage),
    IncludeClosed: search.closed,
    DueFrom: search.dueFrom,
    DueTo: search.dueTo,
  });
}

export function PathwaysListPage({search, onSearchChange, patientId}: PathwaysListPageProps) {
  const page = search.page ?? 1;
  const sortColumn: PathwaySortColumn = search.sort ?? 'Due';
  const ascending = search.asc ?? true;
  // The list starts on "Assigned to me" if Account Settings says so, unless the URL says otherwise (`mine=false`).
  const {pageSize, startsOnMine} = useListSettings();
  const mine = search.mine ?? startsOnMine;
  const isFiltered = hasPathwayFilters({...search, mine: mine || undefined});

  // The clinic picker, on the full list only (a patient's pathways are all theirs).
  const clinic = useClinicPicker('pathways', search.clinic, (next) => onSearchChange({...search, clinic: next, page: undefined}));
  const teamId = patientId ? undefined : clinic.teamId;
  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const {data, isPending, isError} = useQuery(pathwaysListQuery(search, {pageSize, startsOnMine}, patientId, teamId));
  // Starting a pathway needs CreatePathways.
  const canCreate = useCan('CreatePathways');
  // Anyone can filter by who is assigned; the bar names them from the staff list.
  const staffList = useQuery(staffQuery).data;
  const templates = useQuery(allTemplatesQuery).data;
  const teamAndStage = useTeamAndStageFields(search);
  const fields = useMemo(
    () => [
      ...(patientId ? FIELDS_FOR_ONE_PATIENT : FILTER_FIELDS),
      assigneeField(staffList ?? [], search.assignee),
      templateField(templates ?? [], search.templateId),
      ...teamAndStage,
    ],
    [patientId, staffList, search.assignee, templates, search.templateId, teamAndStage],
  );
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as PathwayRow[];

  /** Keeps the sort and the clinic; any change to the filters goes back to page 1. */
  const clearSearch = () => onSearchChange({sort: search.sort, asc: search.asc, clinic: search.clinic});

  const allColumns: DataTableColumn<PathwayRow>[] = [
    {
      key: 'Pathway',
      header: 'Pathway',
      sortable: true,
      width: PATHWAY_COLUMN_WIDTH.pathway,
      renderCell: (p) => <PathwayCell id={p.id ?? ''} title={p.title ?? ''} />,
    },
    // One patient's list shows each pathway's template; the full list, its patient (View patient is in the actions).
    patientId
      ? {key: 'templateName', header: 'Template', width: PATHWAY_COLUMN_WIDTH.template, renderCell: (p) => <TextCell text={p.templateName} />}
      : {key: 'patientName', header: 'Patient', width: PATHWAY_COLUMN_WIDTH.patient, renderCell: (p) => <TextCell text={p.patientName} />},
    {key: 'Status', header: 'Status', sortable: true, width: PATHWAY_COLUMN_WIDTH.status, renderCell: (p) => <StatusToken status={p.status ?? ''} />},
    {
      key: 'stage',
      header: 'Current stage',
      width: PATHWAY_COLUMN_WIDTH.stage,
      renderCell: (p) => (
        <StageProgressCell
          completed={p.completedStageCount ?? 0}
          skipped={p.skippedStageCount ?? 0}
          total={p.stageCount ?? 0}
          currentStageName={p.currentStageName}
        />
      ),
    },
    {
      key: 'Due',
      header: 'Target date',
      sortable: true,
      width: PATHWAY_COLUMN_WIDTH.due,
      renderCell: (p) => <DueDateCell dueDate={p.dueDate ?? ''} isOverdue={p.isOverdue ?? false} />,
    },
    {key: 'Wait', header: 'Wait', sortable: true, width: PATHWAY_COLUMN_WIDTH.wait, renderCell: (p) => <WaitCell clock={p.clock} />},
    {key: 'Priority', header: 'Priority', sortable: true, width: PATHWAY_COLUMN_WIDTH.priority, renderCell: (p) => <PriorityToken priority={p.priority ?? ''} />},
    {key: 'assigned', header: 'Assigned to', width: PATHWAY_COLUMN_WIDTH.assigned, renderCell: (p) => <AssigneesCell assignees={p.assignees ?? []} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: PATHWAY_COLUMN_WIDTH.actions,
      renderCell: (p) => <PathwayActions id={p.id ?? ''} patientId={patientId ? undefined : (p.patientId ?? '')} />,
    },
  ];
  // Narrow (a narrow window or a half view), the table drops the current stage and the assignees to fit.
  const isNarrow = useIsNarrow();
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'stage' && c.key !== 'assigned') : allColumns;

  const noun = (n: number) => (n === 1 ? 'pathway' : 'pathways');
  const subtitle = isPending
    ? 'Loading pathways...'
    : isFiltered
      ? `${total} matching ${noun(total)}`
      : `${total} open ${noun(total)}`;
  const patientName = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : null;

  return (
    <Page
      title={patientId ? `Pathways for ${patientName ?? 'patient'}` : 'Pathways'}
      subtitle={subtitle}
      backHref={patientId ? `/patients/${patientId}` : undefined}
      backLabel="Back to patient"
      fill
      banner={patientId && patient.data && <PatientBanner patient={patient.data} />}
      message={
        isError ? (
          <AlertMessage message="The pathways could not be loaded. Please try again." />
        ) : (
          // A patient's pathways carry the patient's banners (restricted, disputed, anonymised).
          patient.data && <PatientFlagBanners patient={patient.data} />
        )
      }
      actions={
        patientId
          ? canCreate && (
              <Button label="New Pathway" variant="primary" icon={<Icon icon={AppIcon.Add} />} href={`/pathways/new?patientId=${patientId}`} />
            )
          : clinic.picker
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
            // One patient's pathways have no need of a patient filter.
            fields={fields}
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
            isUnfiltered={!hasPathwayFilters(search) && search.mine === undefined}
            label="Search pathways"
            placeholder="Search pathways by title, or filter by template, patient, status, stage reached, team..."
            textKey="title"
            favourites={FAVOURITES}
          />
        }
        sort={{column: sortColumn, ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as PathwaySortColumn, asc: sort.ascending})}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          isFiltered ? (
            <EmptyState
              title="No pathways match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={clearSearch} />}
            />
          ) : (
            <EmptyState
              title={patientId ? 'This patient has no open pathways' : 'No open pathways'}
              description="Discharged, cancelled and archived pathways are hidden. Search to include them."
              icon={<Icon icon={AppIcon.Pathways} size="lg" />}
            />
          )
        }
      />
    </Page>
  );
}

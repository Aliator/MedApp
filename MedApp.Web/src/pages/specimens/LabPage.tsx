import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {useQuery} from '@tanstack/react-query';
import {useMemo, useState} from 'react';
import {labTestsQuery} from '../../api/labTests';
import {
  SAMPLE_DUE,
  SAMPLE_TABS,
  SPECIMEN_SOURCES,
  SPECIMEN_STATUSES,
  SPECIMEN_VIEWS,
  sampleDueLabel,
  specimenStatusLabel,
  specimenViewLabel,
  specimenWorklistQuery,
  type SampleDue,
  type SpecimenSource,
  type SpecimenSummary,
} from '../../api/specimens';
import {teamsQuery} from '../../api/teams';
import {useListSettings} from '../../app/listSettings';
import {Page} from '../../components/pages/Page';
import {isLabClockRunning} from '../../api/labClocks';
import {LabClockCell} from '../../components/labCases/LabClock';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {
  OverdueAtLabToken,
  SampleSourceCell,
  SpecimenNumberCell,
  SpecimenPatientCell,
  SpecimenStatusToken,
} from '../../components/specimens/SpecimenCells';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {hasLabFilters, labSortOf, labViewOf, type LabSearch, type LabSortColumn, type LabView} from './labSearch';

type SpecimenRow = SpecimenSummary & Record<string, unknown>;

/** The time each view is about, for its column: when it was taken, sent, received, reported or closed, or expected by. */
const VIEW_TIMES: Record<LabView, {header: string; at: (s: SpecimenSummary) => string | null | undefined}> = {
  ToReceive: {header: 'Taken', at: (s) => s.takenAtUtc},
  ToSend: {header: 'Taken', at: (s) => s.takenAtUtc},
  Expected: {header: 'Sent', at: (s) => s.sentAtUtc},
  InLab: {header: 'Received', at: (s) => s.receivedAtUtc},
  Overdue: {header: 'Taken', at: (s) => s.takenAtUtc},
  RejectedAndLost: {header: 'Closed', at: (s) => s.closedAtUtc},
  Reported: {header: 'Reported', at: (s) => s.reportedAtUtc},
  All: {header: 'Taken', at: (s) => s.takenAtUtc},
};

/** What each view holds, in its empty list. */
const VIEW_DESCRIPTIONS: Record<LabView, string> = {
  ToReceive: 'Taken or on their way, and not yet received by the lab, oldest first.',
  ToSend: 'Taken and not yet sent, oldest first.',
  Expected: 'Sent and not yet received by the lab, oldest first.',
  InLab: 'Received by the lab and not yet reported, oldest first.',
  Overdue: "Not reported by the type's expected days, oldest first.",
  RejectedAndLost: 'Rejected by the lab or lost in the last 30 days, oldest first.',
  Reported: 'Reported in the last 7 days, oldest first.',
  All: 'Every sample, from the clinics and the hospital, newest taken first.',
};

/** The empty view's words. */
const VIEW_EMPTY: Record<LabView, string> = {
  ToReceive: 'Nothing waiting to reach the lab',
  ToSend: 'Nothing waiting to be sent',
  Expected: 'Nothing on its way to the lab',
  InLab: 'Nothing in the lab',
  Overdue: 'Nothing overdue',
  RejectedAndLost: 'Nothing rejected or lost in the last 30 days',
  Reported: 'Nothing reported in the last 7 days',
  All: 'No samples yet',
};

const viewLabel = (view: LabView) => (view === 'All' ? 'All samples' : specimenViewLabel(view));

type Named = {id?: string; name?: string | null; isArchived?: boolean};
const byName = (items: readonly Named[]) =>
  [...items]
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
    .map((item) => ({value: (item.id ?? '').toLowerCase(), label: item.isArchived ? `${item.name} (archived)` : (item.name ?? '')}));

/**
 * The search's fields (MC14): text in the number, the patient, the type, the test or where it came from; the view
 * (removing it shows every sample); and where it came from, the clinic, the test, the status and when it's due.
 */
function filterFields(clinics: readonly Named[], tests: readonly Named[]): ListFilterField[] {
  return [
    {kind: 'text', key: 'q', label: 'Sample, patient, test or source'},
    {kind: 'choice', key: 'view', label: 'Showing', options: SPECIMEN_VIEWS.map((v) => ({value: v, label: specimenViewLabel(v)}))},
    {kind: 'choice', key: 'source', label: 'From', options: SPECIMEN_SOURCES.map((s) => ({value: s, label: s === 'Hospital' ? 'The hospital' : 'A clinic'}))},
    {kind: 'choice', key: 'clinic', label: 'Clinic', options: byName(clinics)},
    {kind: 'choice', key: 'test', label: 'Test', options: byName(tests)},
    {kind: 'choice', key: 'status', label: 'Status', options: SPECIMEN_STATUSES.map((s) => ({value: s, label: specimenStatusLabel(s)}))},
    {kind: 'choice', key: 'due', label: 'Due', options: SAMPLE_DUE.map((d) => ({value: d, label: sampleDueLabel(d)}))},
  ];
}

/** Its tabs (MC14): To receive, In the lab, Reported, Rejected and lost, and All. */
const FAVOURITES: ListFilterFavourite[] = [
  ...SAMPLE_TABS.map((v) => ({label: specimenViewLabel(v), values: {view: v}})),
  {label: 'All', values: {}},
];

/**
 * The view's page of samples, for its filters, sort, page size and text search (the route's loader fetches the same,
 * without a search). A POST, so the search stays out of URLs.
 */
export function labListQuery(view: LabView, search: LabSearch, pageSize: number, text?: string) {
  return specimenWorklistQuery({
    view,
    page: search.page ?? 1,
    pageSize,
    search: text,
    sort: search.sort,
    ascending: search.sort ? (search.asc ?? true) : undefined,
    source: search.source,
    teamId: search.clinic,
    labTestId: search.test,
    status: search.status,
    due: search.due,
  });
}

export interface LabPageProps {
  /** The view, filters, page and sort, from the URL. */
  search: LabSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: LabSearch) => void;
}

const asString = (value: unknown) => (typeof value === 'string' && value ? value : undefined);

/**
 * The samples database (`/lab/samples`, MC14; the Lab page reworked): every sample, from the clinics and the hospital,
 * with where it came from on every row. It opens on its tabs, To receive (taken or on its way), In the lab, Reported (7
 * days), Rejected and lost (30 days) and All; the person can change the tabs (`ListFilterBar`). The menu's Search
 * searches by text (a sample number typed, or scanned from its label by a keyboard scanner, the patient's name or NHS
 * number, the type, the test or the clinic or hospital), and filters by where it came from, the clinic, the test, the
 * status and when it's due (date arithmetic only). Each row is the number with its type (opening the sample), the
 * patient with their NHS number, where it came from (Clinic or Hospital, and its name), the test, the status with
 * Overdue at lab, when it's due and the view's time. Clinic staff see their own team's samples; the lab sees every
 * one (decision 200). The view, filters, page and sort live in the URL; the text searched never does. Every patient
 * shown is recorded as listed.
 */
export function LabPage({search, onSearchChange}: LabPageProps) {
  const view = labViewOf(search);
  const [text, setText] = useState<string>();
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(labListQuery(view, search, pageSize, text));
  const teams = useQuery(teamsQuery(true)).data;
  const tests = useQuery(labTestsQuery(true)).data;
  const fields = useMemo(() => filterFields((teams ?? []).filter((t) => t.kind !== 'Lab'), tests ?? []), [teams, tests]);
  const time = VIEW_TIMES[view];
  const {sort, asc} = labSortOf(search, view);
  const total = data?.totalCount ?? 0;
  const isSearched = Boolean(text) || hasLabFilters(search);

  const barValues: ListFilterValues = {
    q: text,
    view: view === 'All' ? undefined : view,
    source: search.source,
    clinic: search.clinic,
    test: search.test,
    status: search.status,
    due: search.due,
  };
  const fromBar = (values: ListFilterValues) => {
    setText(asString(values.q));
    onSearchChange({
      view: (asString(values.view) as LabView | undefined) ?? 'All',
      source: asString(values.source) as SpecimenSource | undefined,
      clinic: asString(values.clinic),
      test: asString(values.test),
      status: asString(values.status),
      due: asString(values.due) as SampleDue | undefined,
      sort: search.sort,
      asc: search.asc,
    });
  };

  const columns: DataTableColumn<SpecimenRow>[] = [
    {
      key: 'number',
      header: 'Sample',
      sortable: true,
      width: proportional(1.5, {minWidth: 120}),
      renderCell: (s) => <SpecimenNumberCell specimen={s} />,
    },
    {
      key: 'patient',
      header: 'Patient',
      sortable: true,
      width: proportional(2, {minWidth: 120}),
      renderCell: (s) => <SpecimenPatientCell specimen={s} />,
    },
    {
      key: 'source',
      header: 'From',
      sortable: true,
      width: proportional(1.5, {minWidth: 120}),
      renderCell: (s) => <SampleSourceCell specimen={s} />,
    },
    {key: 'test', header: 'Test', sortable: true, width: proportional(1.5, {minWidth: 110}), renderCell: (s) => <TextCell text={s.labTestName ?? '—'} />},
    {
      key: 'status',
      header: 'Status',
      sortable: true,
      width: pixel(156),
      renderCell: (s) => (
        <VStack gap={1} hAlign="start">
          <SpecimenStatusToken status={s.status} />
          {s.isOverdueAtLab && <OverdueAtLabToken />}
        </VStack>
      ),
    },
    {
      key: 'due',
      header: 'Due',
      sortable: true,
      width: pixel(150),
      // In a lab case, its strict clock (MC19); before one, the type's expected days.
      renderCell: (s) =>
        s.isOpen && isLabClockRunning(s.clock) ? <LabClockCell clock={s.clock} /> : <TextCell text={s.isOpen ? formatDateTime(s.expectedByUtc ?? '') : '—'} />,
    },
    {key: 'time', header: time.header, sortable: true, width: pixel(150), renderCell: (s) => <TextCell text={formatDateTime(time.at(s) ?? '')} />},
  ];

  const noun = total === 1 ? 'sample' : 'samples';
  return (
    <Page
      title="Samples"
      subtitle={isPending ? 'Loading samples...' : `${viewLabel(view)}: ${total} ${isSearched ? `matching ${noun}` : noun}`}
      fill
      message={isError && <AlertMessage message="The samples could not be loaded. Please try again." />}>
      <DataTable
        data={(data?.items ?? []) as SpecimenRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={fields}
            values={barValues}
            onChange={fromBar}
            list="lab"
            isUnfiltered={!isSearched && search.view === undefined}
            label="Search samples"
            placeholder="Type or scan a sample number, or search by patient, test or source, or filter by clinic, status or due date..."
            textKey="q"
            favourites={FAVOURITES}
          />
        }
        sort={{column: sort, ascending: asc}}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as LabSortColumn, asc: next.ascending})}
        pagination={{
          page: search.page ?? 1,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title={isSearched ? 'No samples match your search' : VIEW_EMPTY[view]}
            description={isSearched ? undefined : VIEW_DESCRIPTIONS[view]}
            icon={<Icon icon={isSearched ? AppIcon.Search : AppIcon.Lab} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}

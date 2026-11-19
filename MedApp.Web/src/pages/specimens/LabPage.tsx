import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {
  SPECIMEN_VIEWS,
  specimenViewLabel,
  specimenWorklistQuery,
  type SpecimenSummary,
} from '../../api/specimens';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {OverdueAtLabToken, SpecimenNumberCell, SpecimenPatientCell, SpecimenStatusToken} from '../../components/specimens/SpecimenCells';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {labSortOf, labViewOf, type LabSearch, type LabSortColumn, type LabView} from './labSearch';

type SpecimenRow = SpecimenSummary & Record<string, unknown>;

/** The time each view is about, for its column: when it was taken, sent, received, reported or closed, or expected by. */
const VIEW_TIMES: Record<LabView, {header: string; at: (s: SpecimenSummary) => string | null | undefined}> = {
  ToSend: {header: 'Taken', at: (s) => s.takenAtUtc},
  Expected: {header: 'Sent', at: (s) => s.sentAtUtc},
  InLab: {header: 'Received', at: (s) => s.receivedAtUtc},
  Overdue: {header: 'Expected by', at: (s) => s.expectedByUtc},
  RejectedAndLost: {header: 'Closed', at: (s) => s.closedAtUtc},
  Reported: {header: 'Reported', at: (s) => s.reportedAtUtc},
  All: {header: 'Taken', at: (s) => s.takenAtUtc},
};

/** What each view holds, in its empty list. */
const VIEW_DESCRIPTIONS: Record<LabView, string> = {
  ToSend: 'Taken and not yet sent, oldest first.',
  Expected: 'Sent and not yet received by the lab, oldest first.',
  InLab: 'Received by the lab and not yet reported, oldest first.',
  Overdue: "Not reported by the type's expected days, oldest first.",
  RejectedAndLost: 'Rejected by the lab or lost in the last 30 days, oldest first.',
  Reported: 'Reported in the last 7 days, oldest first.',
  All: 'Every specimen, newest taken first.',
};

/** The empty view's words. */
const VIEW_EMPTY: Record<LabView, string> = {
  ToSend: 'Nothing waiting to be sent',
  Expected: 'Nothing on its way to the lab',
  InLab: 'Nothing in the lab',
  Overdue: 'Nothing overdue',
  RejectedAndLost: 'Nothing rejected or lost in the last 30 days',
  Reported: 'Nothing reported in the last 7 days',
  All: 'No specimens yet',
};

const viewLabel = (view: LabView) => (view === 'All' ? 'All specimens' : specimenViewLabel(view));

/** The search's fields: text in the number, the patient or the type, and the view (removing it shows every specimen). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'Specimen, patient or type'},
  {kind: 'choice', key: 'view', label: 'Showing', options: SPECIMEN_VIEWS.map((v) => ({value: v, label: specimenViewLabel(v)}))},
];

/** Its tabs: the worklist's views, as the page's tabs were. */
const FAVOURITES: ListFilterFavourite[] = SPECIMEN_VIEWS.map((v) => ({label: specimenViewLabel(v), values: {view: v}}));

/**
 * The view's page of specimens, for its sort, page size and text search (the route's loader fetches the same,
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
  });
}

export interface LabPageProps {
  /** The view, page and sort, from the URL. */
  search: LabSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: LabSearch) => void;
}

/**
 * The samples worklist (`/lab/samples`, moved from `/lab` for the lab's home in MC13; decision 90). It opens on its
 * tabs, the views of where specimens are: To send, Expected
 * (sent, not received), In the lab (received, not reported), Overdue, Rejected and lost (30 days) and Reported (7
 * days), each oldest first; the person can change the tabs (`ListFilterBar`). The menu's Search searches by text: a
 * specimen number typed, or scanned from its label by a keyboard scanner (which types it and presses Enter), just
 * narrows the list to it, as does a patient's name or NHS number or a type; removing the view shows every specimen.
 * Each row is the number with its type (opening the specimen), the patient with their NHS number, the status with
 * Overdue at lab, and the view's time; every column but the patient's NHS number sorts (no lab column: the external
 * labs list was dropped in MC13, as the centre's own lab does them all). The view, page and sort live in the URL; the text searched never does (it goes in the
 * request's body, as it may be a specimen number), so it's gone on a reload. Every patient shown is recorded as listed.
 */
export function LabPage({search, onSearchChange}: LabPageProps) {
  const canProcess = useCan('ProcessSpecimens');
  const view = labViewOf(search, canProcess);
  const [text, setText] = useState<string>();
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(labListQuery(view, search, pageSize, text));
  const time = VIEW_TIMES[view];
  const {sort, asc} = labSortOf(search, view);
  const total = data?.totalCount ?? 0;

  const barValues: ListFilterValues = {q: text, view: view === 'All' ? undefined : view};
  const fromBar = (values: ListFilterValues) => {
    setText(typeof values.q === 'string' ? values.q : undefined);
    onSearchChange({
      view: typeof values.view === 'string' ? (values.view as LabView) : 'All',
      sort: search.sort,
      asc: search.asc,
    });
  };

  const columns: DataTableColumn<SpecimenRow>[] = [
    {
      key: 'number',
      header: 'Specimen',
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
    {key: 'time', header: time.header, sortable: true, width: pixel(150), renderCell: (s) => <TextCell text={formatDateTime(time.at(s) ?? '')} />},
  ];

  const noun = total === 1 ? 'specimen' : 'specimens';
  return (
    <Page
      title="Samples"
      subtitle={isPending ? 'Loading specimens...' : `${viewLabel(view)}: ${total} ${noun}`}
      fill
      message={isError && <AlertMessage message="The specimens could not be loaded. Please try again." />}>
      <DataTable
        data={(data?.items ?? []) as SpecimenRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={barValues}
            onChange={fromBar}
            list="lab"
            isUnfiltered={!text && search.view === undefined}
            label="Search specimens"
            placeholder="Type or scan a specimen number, or search by patient or type, or filter by view..."
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
            title={text ? 'No specimens match your search' : VIEW_EMPTY[view]}
            description={text ? undefined : VIEW_DESCRIPTIONS[view]}
            icon={<Icon icon={text ? AppIcon.Search : AppIcon.Lab} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}

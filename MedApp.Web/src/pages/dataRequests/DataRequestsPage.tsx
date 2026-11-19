import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {DATA_REQUEST_VIEWS, dataRequestHref, dataRequestViewLabel, type DataRequestSummary, type DataRequestView} from '../../api/dataRequests';
import {useListSettings} from '../../app/listSettings';
import {DataRequestStatusToken, DoneCell, DueCell} from '../../components/dataRequests/DataRequestCells';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {formatShortDate} from '../../utils/dates';
import {
  DEFAULT_DATA_REQUEST_VIEW,
  dataRequestsListQuery,
  dataRequestsSortOf,
  type DataRequestSortColumn,
  type DataRequestsListSearch,
} from './dataRequestsSearch';

type DataRequestRow = DataRequestSummary & Record<string, unknown>;

/** What each view's empty list says. */
const EMPTY: Record<DataRequestView, {title: string; description: string}> = {
  Open: {title: 'No open requests', description: "Requests logged from a patient's page wait here until they're sent, refused or withdrawn."},
  AwaitingReview: {title: 'Nothing to review', description: 'Open requests wait here until a clinician has reviewed what goes in the pack.'},
  Sent: {title: 'Nothing sent yet', description: 'Requests answered with their pack, newest first.'},
  Refused: {title: 'Nothing refused', description: 'Requests refused, with the reason the requester was given.'},
  Withdrawn: {title: 'Nothing withdrawn', description: 'Requests the requester withdrew.'},
  All: {title: 'No data requests', description: "Log a patient's data request from their page, under Privacy."},
};

/** The search's fields: the patient's name, and the view (removing it shows every request: All). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'patient', label: 'Patient'},
  {kind: 'choice', key: 'view', label: 'Showing', options: DATA_REQUEST_VIEWS.filter((v) => v.value !== 'All')},
];

/** Its tabs: the views, as the page's tabs were, All last. */
const FAVOURITES: ListFilterFavourite[] = DATA_REQUEST_VIEWS.map((v) => ({label: v.label, values: v.value === 'All' ? {} : {view: v.value}}));

export interface DataRequestsPageProps {
  /** The view, patient, page and sort, from the URL. */
  search: DataRequestsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: DataRequestsListSearch) => void;
}

/**
 * Data Requests (`/data-requests`): patients' subject access requests, for holders of ManagePrivacy (who log, review
 * and answer them). It opens on its tabs: **Open** (where it starts), **Awaiting review**,
 * **Sent**, **Refused**, **Withdrawn** and **All**; the person can change the tabs, and search by the patient's name
 * (`ListFilterBar`). A page at a time from the API, open ones first by due date, or by the column chosen (the patient,
 * received, due or status). Each row is the patient (linking to their page, with who asked), the request (opening it),
 * when it's due (flagged when overdue), its status, and whether the identity check and the clinician review are done.
 * The view, patient, page and sort live in the URL. Every patient listed is recorded in the activity log by the API.
 */
export function DataRequestsPage({search, onSearchChange}: DataRequestsPageProps) {
  const view = search.view ?? DEFAULT_DATA_REQUEST_VIEW;
  const page = search.page ?? 1;
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(dataRequestsListQuery(view, search, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as DataRequestRow[];
  const isNarrow = useIsNarrow();
  const sort = dataRequestsSortOf(search, view);

  const barValues: ListFilterValues = {patient: search.patient, view: view === 'All' ? undefined : view};
  const fromBar = (values: ListFilterValues) =>
    onSearchChange({
      view: typeof values.view === 'string' ? (values.view as DataRequestView) : 'All',
      patient: typeof values.patient === 'string' ? values.patient : undefined,
      sort: search.sort,
      asc: search.asc,
    });

  const allColumns: DataTableColumn<DataRequestRow>[] = [
    {
      key: 'patient',
      header: 'Patient',
      sortable: true,
      width: proportional(2, {minWidth: 160}),
      renderCell: (r) => (
        <VStack gap={0} hAlign="start">
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={`/patients/${r.patientId}`}>{r.patientName}</Link>
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {r.requestedByPatient ? 'Requested by the patient' : 'Requested on their behalf'}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'received',
      header: 'Request',
      sortable: true,
      width: proportional(1, {minWidth: 150}),
      renderCell: (r) => (
        <Text type="body" maxLines={1}>
          <Link href={dataRequestHref(r.id ?? '')}>{`Received ${formatShortDate(r.receivedOn ?? '')}`}</Link>
        </Text>
      ),
    },
    {key: 'due', header: 'Due', sortable: true, width: proportional(1, {minWidth: 130}), renderCell: (r) => <DueCell dueOn={r.dueOn} isOverdue={r.isOverdue} />},
    {key: 'status', header: 'Status', sortable: true, width: pixel(120), renderCell: (r) => <DataRequestStatusToken status={r.status} />},
    {key: 'identity', header: 'Identity checked', width: pixel(140), renderCell: (r) => <DoneCell isDone={r.isIdentityChecked} />},
    {key: 'reviewed', header: 'Reviewed', width: pixel(120), renderCell: (r) => <DoneCell isDone={r.isReviewed} />},
  ];
  // Narrow (a narrow window or a half view), the table drops the identity check and review to fit.
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'identity' && c.key !== 'reviewed') : allColumns;
  const noun = total === 1 ? 'request' : 'requests';
  const subtitle = isPending ? 'Loading data requests...' : `${dataRequestViewLabel(view)}: ${total} ${noun}`;

  return (
    <Page
      title="Data Requests"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage label="Data requests not loaded" message="The data requests could not be loaded. Please try again." />}>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={barValues}
            onChange={fromBar}
            list="dataRequests"
            isUnfiltered={!search.patient && search.view === undefined}
            label="Search data requests"
            placeholder="Search data requests by the patient's name, or filter by view..."
            textKey="patient"
            favourites={FAVOURITES}
          />
        }
        sort={sort ? {column: sort.sort, ascending: sort.asc} : null}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as DataRequestSortColumn, asc: next.ascending})}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          search.patient ? (
            <EmptyState
              title="No data requests match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, patient: undefined})} />}
            />
          ) : (
            <EmptyState title={EMPTY[view].title} description={EMPTY[view].description} icon={<Icon icon={AppIcon.DataRequests} size="lg" />} />
          )
        }
      />
    </Page>
  );
}

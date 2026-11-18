import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {Tab, TabList} from '@astryxdesign/core/TabList';
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
import {formatShortDate} from '../../utils/dates';
import {DEFAULT_DATA_REQUEST_VIEW, dataRequestsListQuery, type DataRequestsListSearch} from './dataRequestsSearch';

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

const panelId = (view: DataRequestView) => `data-requests-${view}`;

export interface DataRequestsPageProps {
  /** The view and page, from the URL. */
  search: DataRequestsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: DataRequestsListSearch) => void;
}

/**
 * Data Requests (`/data-requests`): patients' subject access requests, for holders of ManagePrivacy (who log and answer
 * them) and of SignOffResults (who review them). Its views are tabs: **Open** (where it starts), **Awaiting review**,
 * **Sent**, **Refused**, **Withdrawn** and **All**, a page at a time from the API, open ones first by due date. Each
 * row is the patient (linking to their page, with who asked), the request (opening it), when it's due (flagged when
 * overdue), its status, and whether the identity check and the clinician review are done. The view and page live in
 * the URL. Every patient listed is recorded in the activity log by the API.
 */
export function DataRequestsPage({search, onSearchChange}: DataRequestsPageProps) {
  const view = search.view ?? DEFAULT_DATA_REQUEST_VIEW;
  const page = search.page ?? 1;
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(dataRequestsListQuery(view, page, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as DataRequestRow[];
  const isNarrow = useIsNarrow();

  const allColumns: DataTableColumn<DataRequestRow>[] = [
    {
      key: 'patient',
      header: 'Patient',
      width: proportional(2, {minWidth: 160}),
      renderCell: (r) => (
        <VStack gap={0} hAlign="start">
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={`/patients/${r.patientId}`}>{r.patientName}</Link>
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {r.requestedByPatient ? 'Asked by the patient' : 'Asked on their behalf'}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'received',
      header: 'Request',
      width: proportional(1, {minWidth: 150}),
      renderCell: (r) => (
        <Text type="body" maxLines={1}>
          <Link href={dataRequestHref(r.id ?? '')}>{`Received ${formatShortDate(r.receivedOn ?? '')}`}</Link>
        </Text>
      ),
    },
    {key: 'due', header: 'Due', width: proportional(1, {minWidth: 130}), renderCell: (r) => <DueCell dueOn={r.dueOn} isOverdue={r.isOverdue} />},
    {key: 'status', header: 'Status', width: pixel(120), renderCell: (r) => <DataRequestStatusToken status={r.status} />},
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
      message={isError && <AlertMessage message="The data requests could not be loaded. Please try again." />}>
      <VStack gap={4} height="100%">
        <TabList value={view} onChange={(next) => onSearchChange({view: next as DataRequestView})} role="tablist" hasDivider>
          {DATA_REQUEST_VIEWS.map(({value, label}) => (
            <Tab key={value} value={value} label={label} panelId={panelId(value)} />
          ))}
        </TabList>
        <StackItem size="fill">
          <VStack id={panelId(view)} role="tabpanel" aria-label={dataRequestViewLabel(view)} height="100%">
            <DataTable
              data={rows}
              columns={columns}
              idKey="id"
              isLoading={isPending}
              skeletonRows={pageSize}
              pagination={{
                page,
                pageSize,
                totalItems: total,
                onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
              }}
              emptyState={
                <EmptyState title={EMPTY[view].title} description={EMPTY[view].description} icon={<Icon icon={AppIcon.DataRequests} size="lg" />} />
              }
            />
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}

import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {
  ACCESS_ACTIONS,
  FILE_ACTIONS,
  ACCOUNT_ACTIONS,
  ACTIVITY_ACTIONS,
  activityActionLabel,
  activitySearchQuery,
  describeActivity,
  SIGN_IN_ACTIONS,
  type ActivityEntry,
} from '../../api/activity';
import {useListSettings} from '../../app/listSettings';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {dayFromToday, ListFilterBar, type ListFilterFavourite, type ListFilterField} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {choiceValues, hasActivityFilters, type ActivityListSearch} from '../listSearch';

type EntryRow = ActivityEntry & Record<string, unknown>;

export interface ActivityLogPageProps {
  search: ActivityListSearch;
  onSearchChange: (search: ActivityListSearch) => void;
}

/** The search's fields, as the API filters the log, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'User or address'},
  {kind: 'choice', key: 'action', label: 'Action', options: ACTIVITY_ACTIONS.map((a) => ({value: a, label: activityActionLabel(a)})), isMultiple: true},
  {kind: 'date', key: 'when', label: 'When', fromKey: 'from', toKey: 'to'},
];

/** Its tabs: every entry (where it opens), recent entries, and the kinds most looked for. */
const favourites = (): ListFilterFavourite[] => [
  {label: 'All', values: {}},
  {label: 'Today', values: {from: dayFromToday()}},
  {label: 'Last 7 days', values: {from: dayFromToday({days: -6})}},
  {label: 'Record access', values: {action: ACCESS_ACTIONS.join(',')}},
  {label: 'Files', values: {action: FILE_ACTIONS.join(',')}},
  {label: 'Sign-ins', values: {action: SIGN_IN_ACTIONS.join(',')}},
  {label: 'Refused sign-ins', values: {action: 'SignInFailed'}},
  {label: 'Account changes', values: {action: ACCOUNT_ACTIONS.join(',')}},
];

/** The log's page of entries, for its search and page size (the route's loader fetches it too). */
export function activityListQuery(search: ActivityListSearch, pageSize: number) {
  return activitySearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Ascending: search.asc ?? false,
    Search: search.q,
    Actions: choiceValues(search.action, ACTIVITY_ACTIONS),
    PatientId: search.patientId,
    From: search.from,
    To: search.to,
  });
}

/**
 * The activity log (admin): who viewed, searched or changed patients, signed in or out (and failed to), and changed
 * accounts and roles, newest first, a page at a time. Its power search filters by user or address, actions and dates;
 * a patient's access report opens it on just that patient. Entries hold IDs, not patient details, and name each
 * patient as they are now.
 */
export function ActivityLogPage({search, onSearchChange}: ActivityLogPageProps) {
  const {pageSize} = useListSettings();
  const page = search.page ?? 1;
  const ascending = search.asc ?? false;
  const isFiltered = hasActivityFilters(search);
  const {data, isPending, isError} = useQuery(activityListQuery(search, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as EntryRow[];
  const patientName = search.patientId ? rows[0]?.patientName : null;

  /** Keeps the order; clears the filters and the patient. */
  const clearSearch = () => onSearchChange({asc: search.asc});

  const columns: DataTableColumn<EntryRow>[] = [
    {
      key: 'When',
      header: 'When',
      sortable: true,
      width: pixel(150),
      renderCell: (e) => <TextCell text={e.atUtc ? formatDateTime(e.atUtc) : ''} />,
    },
    {
      key: 'username',
      header: 'Who',
      width: proportional(1, {minWidth: 88}),
      renderCell: (e) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold" maxLines={1}>
            {e.username || '—'}
          </Text>
          {e.ipAddress && (
            <Text type="supporting" color="secondary" maxLines={1}>
              {e.ipAddress}
            </Text>
          )}
        </VStack>
      ),
    },
    {key: 'action', header: 'Action', width: pixel(160), renderCell: (e) => <TextCell text={activityActionLabel(e.action)} />},
    {
      key: 'about',
      header: 'About',
      width: proportional(2, {minWidth: 96}),
      renderCell: (e) =>
        e.patientId ? (
          <VStack gap={0}>
            <Text type="body" maxLines={1}>
              <Link href={`/patients/${e.patientId}`}>{e.patientName}</Link>
            </Text>
            {e.pathwayId && (
              <Text type="supporting" maxLines={1}>
                <Link href={`/pathways/${e.pathwayId}`}>Pathway</Link>
              </Text>
            )}
          </VStack>
        ) : (
          <TextCell text={e.targetUsername ?? ''} />
        ),
    },
    {
      key: 'details',
      header: 'Details',
      width: proportional(3, {minWidth: 120}),
      renderCell: (e) => <TextCell text={describeActivity(e)} maxLines={2} />,
    },
  ];

  const noun = total === 1 ? 'entry' : 'entries';
  const subtitle = isPending
    ? 'Loading the activity log...'
    : patientName
      ? `${total} ${noun} for ${patientName}`
      : `${total} ${noun}${isFiltered ? ' found' : ''}`;

  return (
    <Page
      title="Activity Log"
      subtitle={subtitle}
      backHref={search.patientId ? `/patients/${search.patientId}` : undefined}
      backLabel="Back to the patient"
      fill
      message={isError && <AlertMessage label="Activity log not loaded" message="The activity log could not be loaded. Please try again." />}>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        // The search, keeping the patient the log is opened on (it's not one of the bar's filters).
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={search}
            onChange={(values) => onSearchChange({...search, ...values, page: undefined})}
            list="activity"
            label="Search the activity log"
            placeholder="Search by user or address, or filter by action and date..."
            textKey="q"
            favourites={favourites()}
          />
        }
        sort={{column: 'When', ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, asc: sort.ascending || undefined})}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title={isFiltered ? 'No entries match your search' : 'Nothing in the activity log yet'}
            description={
              isFiltered
                ? 'Try different terms or dates, or clear the search.'
                : 'Views of and changes to patients, sign-ins and account changes are recorded here.'
            }
            isCompact
            actions={isFiltered ? <Button label="Clear Search" variant="secondary" onClick={clearSearch} /> : undefined}
          />
        }
      />
    </Page>
  );
}

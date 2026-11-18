import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {useQuery} from '@tanstack/react-query';
import {referralSearchQuery, type ReferralSummary} from '../../api/referrals';
import {useListSettings} from '../../app/listSettings';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {PriorityToken} from '../../components/pathways/PathwayCells';
import {
  LetterCell,
  ReceivedCell,
  REFERRAL_COLUMN_WIDTH,
  ReferralActions,
  ReferralPatientCell,
  ReferralStatusToken,
} from '../../components/referrals/ReferralCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {
  REFERRAL_SORTS,
  referralViewOf,
  viewStatuses,
  type ReferralSortColumn,
  type ReferralsListSearch,
  type ReferralView,
} from './referralsSearch';

/** The search's fields: the patient's name, and the view (removing it shows every referral). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'patient', label: 'Patient'},
  {
    kind: 'choice',
    key: 'view',
    label: 'Showing',
    options: [
      {value: 'awaiting', label: 'Awaiting triage'},
      {value: 'closed', label: 'Closed'},
    ],
  },
];

/** The views: the inbox, the closed ones, and all of them. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'Awaiting triage', values: {view: 'awaiting'}},
  {label: 'Closed', values: {view: 'closed'}},
  {label: 'All', values: {}},
];

/** The bar's values from the URL: the view shows as Awaiting triage when the URL has none (the inbox). */
function barValues(search: ReferralsListSearch): ListFilterValues {
  const view = referralViewOf(search);
  return {patient: search.patient, view: view === 'all' ? undefined : view};
}

/** The search from the bar's values: a removed view is `all`, and the inbox is left out of the URL. */
function fromBar(values: ListFilterValues, search: ReferralsListSearch): ReferralsListSearch {
  const view: ReferralView = values.view === undefined ? 'all' : values.view === 'closed' ? 'closed' : 'awaiting';
  return {
    sort: search.sort,
    asc: search.asc,
    patient: typeof values.patient === 'string' ? values.patient : undefined,
    view: view === 'awaiting' ? undefined : view,
  };
}

/** What the subtitle counts: "12 referrals awaiting triage", "3 closed referrals", "15 referrals", "2 matching referrals". */
function countWords(view: ReferralView, total: number, isSearched: boolean): string {
  const noun = total === 1 ? 'referral' : 'referrals';
  if (isSearched) return `${total} matching ${noun}`;
  if (view === 'awaiting') return `${total} ${noun} awaiting triage`;
  return view === 'closed' ? `${total} closed ${noun}` : `${total} ${noun}`;
}

type ReferralRow = ReferralSummary & Record<string, unknown>;

/** The list's page of referrals, for its search and page size (the route's loader fetches the same). */
export function referralsListQuery(search: ReferralsListSearch, pageSize: number) {
  return referralSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: REFERRAL_SORTS[search.sort ?? 'Received'],
    Ascending: search.asc ?? true,
    Statuses: viewStatuses(referralViewOf(search)),
    Search: search.patient,
  });
}

export interface ReferralsListPageProps {
  /** The view, page, sort and search, from the URL. */
  search: ReferralsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: ReferralsListSearch) => void;
}

/**
 * Referrals: the inbox of referrals awaiting triage, longest wait first (by received date, oldest first), and the
 * closed ones or all of them, a page at a time from the API. Each row is the patient (with their NHS number), the
 * specialty asked for, when it was received and how long ago, the priority the referrer asked for, the practice,
 * whether the letter is attached, and the status; the patient's name opens the referral. Register Referral starts a
 * new one. The view, page, sort and the patient's name searched for live in the URL.
 */
export function ReferralsListPage({search, onSearchChange}: ReferralsListPageProps) {
  const page = search.page ?? 1;
  const sortColumn: ReferralSortColumn = search.sort ?? 'Received';
  const ascending = search.asc ?? true;
  const view = referralViewOf(search);
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(referralsListQuery(search, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as ReferralRow[];

  const allColumns: DataTableColumn<ReferralRow>[] = [
    {
      key: 'Patient',
      header: 'Patient',
      sortable: true,
      width: REFERRAL_COLUMN_WIDTH.patient,
      renderCell: (r) => <ReferralPatientCell id={r.id ?? ''} patientName={r.patientName ?? ''} nhsNumber={r.patientNhsNumber} />,
    },
    {key: 'Specialty', header: 'Specialty', sortable: true, width: REFERRAL_COLUMN_WIDTH.specialty, renderCell: (r) => <TextCell text={r.specialtyName} />},
    {
      key: 'Received',
      header: 'Received',
      sortable: true,
      width: REFERRAL_COLUMN_WIDTH.received,
      renderCell: (r) => (
        <ReceivedCell receivedOn={r.receivedOn ?? ''} daysSinceReceived={r.daysSinceReceived ?? 0} isOpen={r.status === 'AwaitingTriage'} />
      ),
    },
    {
      key: 'Priority',
      header: 'Priority asked for',
      sortable: true,
      width: REFERRAL_COLUMN_WIDTH.priority,
      renderCell: (r) => <PriorityToken priority={r.requestedPriority ?? ''} />,
    },
    {key: 'practice', header: 'Practice', width: REFERRAL_COLUMN_WIDTH.practice, renderCell: (r) => <TextCell text={r.referrerPractice} />},
    {key: 'letter', header: 'Letter', width: REFERRAL_COLUMN_WIDTH.letter, renderCell: (r) => <LetterCell hasLetter={r.hasLetter ?? false} />},
    {key: 'status', header: 'Status', width: REFERRAL_COLUMN_WIDTH.status, renderCell: (r) => <ReferralStatusToken status={r.status} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: REFERRAL_COLUMN_WIDTH.actions,
      renderCell: (r) => <ReferralActions id={r.id ?? ''} patientId={r.patientId ?? ''} />,
    },
  ];
  // Narrow (a narrow window or a half view), the table drops the practice and the letter to fit.
  const isNarrow = useIsNarrow();
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'practice' && c.key !== 'letter') : allColumns;

  const subtitle = isPending ? 'Loading referrals...' : countWords(view, total, Boolean(search.patient));

  return (
    <Page
      title="Referrals"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage message="The referrals could not be loaded. Please try again." />}
      actions={
        // TODO(useCan): show only to holders of RegisterReferrals once `useCan` is on main; the API refuses others (403).
        <Button label="Register Referral" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/referrals/new" />
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={barValues(search)}
            onChange={(values) => onSearchChange(fromBar(values, search))}
            list="referrals"
            isUnfiltered={!search.patient && search.view === undefined}
            label="Search referrals"
            placeholder="Search referrals by the patient's name, or show the closed ones..."
            textKey="patient"
            favourites={FAVOURITES}
          />
        }
        sort={{column: sortColumn, ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as ReferralSortColumn, asc: sort.ascending})}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          search.patient ? (
            <EmptyState
              title="No referrals match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, patient: undefined})} />}
            />
          ) : (
            <EmptyState
              title={view === 'awaiting' ? 'No referrals are awaiting triage' : view === 'closed' ? 'No closed referrals' : 'No referrals yet'}
              description={view === 'awaiting' ? 'Registered referrals wait here until they are triaged.' : undefined}
              icon={<Icon icon={AppIcon.Referrals} size="lg" />}
            />
          )
        }
      />
    </Page>
  );
}

import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import {referralSearchQuery, type ReferralSummary} from '../../api/referrals';
import {specialtiesQuery} from '../../api/specialties';
import {useCan} from '../../auth/auth';
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
import {ReviewOverdueToken, WaitCell} from '../../components/referrals/WaitingClock';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {formatShortDate} from '../../utils/dates';
import {
  REFERRAL_SORTS,
  referralViewOf,
  sortOf,
  viewFilters,
  type ReferralSortColumn,
  type ReferralsListSearch,
  type ReferralView,
} from './referralsSearch';
import {idChoiceValues} from '../listSearch';
import {specialtyField} from '../pathways/pathwayFilterFields';

/** The search's fields: the patient's name, and the view (removing it shows every referral). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'patient', label: 'Patient'},
  {
    kind: 'choice',
    key: 'view',
    label: 'Showing',
    options: [
      {value: 'awaiting', label: 'Awaiting triage'},
      {value: 'waiting', label: 'Waiting list'},
      {value: 'paused', label: 'Paused clocks'},
      {value: 'closed', label: 'Closed'},
    ],
  },
];

/** The views: the inbox, the waiting list, the paused clocks, the closed ones, and all of them. */
const FAVOURITES: ListFilterFavourite[] = [
  {label: 'Awaiting triage', values: {view: 'awaiting'}},
  {label: 'Waiting list', values: {view: 'waiting'}},
  {label: 'Paused clocks', values: {view: 'paused'}},
  {label: 'Closed', values: {view: 'closed'}},
  {label: 'All', values: {}},
];

/** A view's name in the bar's values ('all' when it was removed). */
const VIEW_FROM_BAR: Record<string, ReferralView> = {awaiting: 'awaiting', waiting: 'waiting', paused: 'paused', closed: 'closed'};

/** The bar's values from the URL: the view shows as Awaiting triage when the URL has none (the inbox). */
function barValues(search: ReferralsListSearch): ListFilterValues {
  const view = referralViewOf(search);
  return {patient: search.patient, view: view === 'all' ? undefined : view, specialty: search.specialty};
}

/** The search from the bar's values: a removed view is `all`, and the inbox is left out of the URL. */
function fromBar(values: ListFilterValues, search: ReferralsListSearch): ReferralsListSearch {
  const view: ReferralView = values.view === undefined ? 'all' : (VIEW_FROM_BAR[String(values.view)] ?? 'awaiting');
  return {
    sort: search.sort,
    asc: search.asc,
    patient: typeof values.patient === 'string' ? values.patient : undefined,
    view: view === 'awaiting' ? undefined : view,
    specialty: typeof values.specialty === 'string' ? values.specialty : undefined,
  };
}

/**
 * What the subtitle counts: "12 referrals awaiting triage", "40 referrals on the waiting list", "3 paused clocks", "3
 * closed referrals", "15 referrals", "2 matching referrals".
 */
function countWords(view: ReferralView, total: number, isSearched: boolean): string {
  const noun = total === 1 ? 'referral' : 'referrals';
  if (isSearched) return `${total} matching ${noun}`;
  if (view === 'awaiting') return `${total} ${noun} awaiting triage`;
  if (view === 'waiting') return `${total} ${noun} on the waiting list`;
  if (view === 'paused') return `${total} paused ${total === 1 ? 'clock' : 'clocks'}`;
  return view === 'closed' ? `${total} closed ${noun}` : `${total} ${noun}`;
}

const EMPTY_TITLES: Record<ReferralView, string> = {
  awaiting: 'No referrals are awaiting triage',
  waiting: 'Nobody is on the waiting list',
  paused: 'No clocks are paused',
  closed: 'No closed referrals',
  all: 'No referrals yet',
};

const EMPTY_DESCRIPTIONS: Partial<Record<ReferralView, string>> = {
  awaiting: 'Registered referrals wait here until they are triaged.',
  waiting: "Accepted referrals wait here until their clock stops, when the pathway's clock-stop stage is finished or the pathway ends.",
  paused: "A clock paused for the patient's own delay shows here, by its review date.",
};

type ReferralRow = ReferralSummary & Record<string, unknown>;

/** The list's page of referrals, for its search and page size (the route's loader fetches the same). */
export function referralsListQuery(search: ReferralsListSearch, pageSize: number) {
  const {sort, asc} = sortOf(search);
  return referralSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: REFERRAL_SORTS[sort],
    Ascending: asc,
    ...viewFilters(referralViewOf(search)),
    Search: search.patient,
    SpecialtyIds: idChoiceValues(search.specialty),
  });
}

export interface ReferralsListPageProps {
  /** The view, page, sort and search, from the URL. */
  search: ReferralsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: ReferralsListSearch) => void;
}

/**
 * Referrals: the inbox of referrals awaiting triage, longest wait first; the waiting list (accepted, the clock not yet
 * stopped), longest wait first; the paused clocks, by review date, past ones flagged (hazard H-42); and the closed ones
 * or all of them, a page at a time from the API. Each row is the patient (with their NHS number), the specialty asked
 * for, when it was received, the wait ("Day 84 of 126", Breached, Paused), the priority the referrer asked for, the
 * practice, whether the letter is attached, and the status; the patient's name opens the referral. The waiting list
 * adds the target date, and the paused clocks the review date. Orders are by dates only (decision 50). Register
 * Referral starts a new one. The view, page, sort and the patient's name searched for live in the URL.
 */
export function ReferralsListPage({search, onSearchChange}: ReferralsListPageProps) {
  const page = search.page ?? 1;
  const {sort: sortColumn, asc: ascending} = sortOf(search);
  const view = referralViewOf(search);
  const {pageSize} = useListSettings();
  const canRegister = useCan('RegisterReferrals');
  const {data, isPending, isError} = useQuery(referralsListQuery(search, pageSize));
  const specialties = useQuery(specialtiesQuery(true)).data;
  const fields = useMemo(() => [...FILTER_FIELDS, specialtyField(specialties ?? [], search.specialty)], [specialties, search.specialty]);
  const isSearched = Boolean(search.patient || search.specialty);
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
        <ReceivedCell
          receivedOn={r.receivedOn ?? ''}
          daysSinceReceived={r.daysSinceReceived ?? 0}
          isOpen={r.status === 'AwaitingTriage'}
          hasClock={Boolean(r.clock)}
        />
      ),
    },
    {key: 'Wait', header: 'Wait', sortable: true, width: REFERRAL_COLUMN_WIDTH.wait, renderCell: (r) => <WaitCell clock={r.clock} />},
    ...(view === 'waiting'
      ? [
          {
            key: 'Target',
            header: 'Target date',
            sortable: true,
            width: REFERRAL_COLUMN_WIDTH.received,
            renderCell: (r: ReferralRow) => <Text type="body">{r.clock?.targetOn ? formatShortDate(r.clock.targetOn) : '—'}</Text>,
          },
        ]
      : []),
    ...(view === 'paused'
      ? [
          {
            key: 'Review',
            header: 'Review date',
            sortable: true,
            width: REFERRAL_COLUMN_WIDTH.received,
            renderCell: (r: ReferralRow) => (
              <VStack gap={1} hAlign="start">
                <Text type="body">{r.clock?.pauseReviewOn ? formatShortDate(r.clock.pauseReviewOn) : '—'}</Text>
                {r.clock?.isReviewOverdue && <ReviewOverdueToken />}
              </VStack>
            ),
          },
        ]
      : []),
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

  const subtitle = isPending ? 'Loading referrals...' : countWords(view, total, isSearched);

  return (
    <Page
      title="Referrals"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage message="The referrals could not be loaded. Please try again." />}
      actions={
        canRegister && (
          <Button label="Register Referral" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/referrals/new" />
        )
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={fields}
            values={barValues(search)}
            onChange={(values) => onSearchChange(fromBar(values, search))}
            list="referrals"
            isUnfiltered={!isSearched && search.view === undefined}
            label="Search referrals"
            placeholder="Search referrals by the patient's name, or filter by specialty, or show the waiting list..."
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
          isSearched ? (
            <EmptyState
              title="No referrals match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, patient: undefined, specialty: undefined})} />}
            />
          ) : (
            <EmptyState
              title={EMPTY_TITLES[view]}
              description={EMPTY_DESCRIPTIONS[view]}
              icon={<Icon icon={AppIcon.Referrals} size="lg" />}
            />
          )
        }
      />
    </Page>
  );
}

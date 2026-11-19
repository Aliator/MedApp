import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {letterHref, letterKindWords, LETTER_VIEWS, letterViewLabel, type LetterSummary} from '../../api/letters';
import {useListSettings} from '../../app/listSettings';
import {useCurrentUser} from '../../auth/auth';
import {LetterStatusToken} from '../../components/letters/LetterCells';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {useClinicPicker} from '../../components/teams/ClinicPicker';
import {formatDateTime} from '../../utils/dates';
import {defaultLetterView, lettersListQuery, lettersSortOf, type LetterSortColumn, type LettersListSearch, type LettersView} from './lettersSearch';

type LetterRow = LetterSummary & Record<string, unknown>;

/** What each view's empty list says. */
const EMPTY: Record<LettersView, {title: string; description: string}> = {
  MyDrafts: {title: 'No drafts', description: 'Letters you start from a pathway or a referral wait here until you submit them.'},
  AwaitingMyApproval: {title: 'Nothing to approve', description: 'Letters submitted to you for approval wait here.'},
  ReturnedToMe: {title: 'Nothing returned', description: 'Letters an approver returns to you with a comment wait here.'},
  ToSend: {title: 'Nothing to send', description: 'Approved letters wait here until every recipient has been sent theirs.'},
  Sent: {title: 'Nothing sent yet', description: 'Letters sent to every recipient, newest first.'},
  All: {title: 'No letters yet', description: "Letters are started from a pathway's or a referral's page."},
};

/** The search's fields: the patient's name, and the view (removing it shows every letter). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'patient', label: 'Patient'},
  {kind: 'choice', key: 'view', label: 'Showing', options: LETTER_VIEWS},
];

/** Its tabs: the views, as the page's tabs were. */
const FAVOURITES: ListFilterFavourite[] = LETTER_VIEWS.map((v) => ({label: v.label, values: {view: v.value}}));

const viewLabel = (view: LettersView) => (view === 'All' ? 'All letters' : letterViewLabel(view));

/** The view's latest step, for its Latest column: when it was approved, submitted or last changed. */
function latestStep(letter: LetterSummary): string {
  if (letter.withdrawnAtUtc) return `Withdrawn ${formatDateTime(letter.withdrawnAtUtc)}`;
  if (letter.approvedAtUtc) return `Approved ${formatDateTime(letter.approvedAtUtc)}`;
  if (letter.submittedAtUtc && letter.status === 'AwaitingApproval') return `Submitted ${formatDateTime(letter.submittedAtUtc)}`;
  return `Updated ${formatDateTime(letter.lastUpdated ?? '')}`;
}

export interface LettersPageProps {
  /** The view, patient, page and sort, from the URL. */
  search: LettersListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: LettersListSearch) => void;
}

/**
 * Letters (`/letters`, decision 108). It opens on its tabs, the views **My drafts**, **Awaiting my approval**,
 * **Returned to me**, **To send** (approved, with a recipient not yet sent) and **Sent**; the person can change the
 * tabs, and search by the patient's name (`ListFilterBar`; removing the view shows every letter). Each row is the
 * patient (linking to the letter), what it is (its kind and template), its author and approver, its status and its
 * latest step; the patient, the letter, the status and the latest step sort. The view, patient, page and sort live in
 * the URL; without a view it opens on the one the person most likely wants (`defaultLetterView`). Letters are started
 * from a pathway's or referral's page.
 */
export function LettersPage({search, onSearchChange}: LettersPageProps) {
  const user = useCurrentUser();
  const view = search.view ?? defaultLetterView(user.permissions);
  const page = search.page ?? 1;
  const {pageSize} = useListSettings();
  const clinic = useClinicPicker('letters', search.clinic, (next) => onSearchChange({...search, clinic: next, page: undefined}));
  const {data, isPending, isError} = useQuery(lettersListQuery(view, search, pageSize, clinic.teamId));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as LetterRow[];
  const isNarrow = useIsNarrow();
  const sort = lettersSortOf(search, view);

  const barValues: ListFilterValues = {patient: search.patient, view: view === 'All' ? undefined : view};
  const fromBar = (values: ListFilterValues) =>
    onSearchChange({
      view: typeof values.view === 'string' ? (values.view as LettersView) : 'All',
      patient: typeof values.patient === 'string' ? values.patient : undefined,
      sort: search.sort,
      asc: search.asc,
      clinic: search.clinic,
    });

  const allColumns: DataTableColumn<LetterRow>[] = [
    {
      key: 'patient',
      header: 'Patient',
      sortable: true,
      width: proportional(2, {minWidth: 160}),
      renderCell: (l) => (
        <VStack gap={0} hAlign="start">
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={letterHref(l.id ?? '')}>{l.patientName}</Link>
          </Text>
          {l.pathwayTitle && (
            <Text type="supporting" color="secondary" maxLines={1}>
              {l.pathwayTitle}
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'letter',
      header: 'Letter',
      sortable: true,
      width: proportional(2, {minWidth: 160}),
      renderCell: (l) => (
        <VStack gap={0} hAlign="start">
          <Text type="body" maxLines={1}>
            {letterKindWords(l.kind)}
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {l.templateName}
          </Text>
        </VStack>
      ),
    },
    {key: 'author', header: 'Author', width: proportional(1, {minWidth: 110}), renderCell: (l) => <TextCell text={l.authorUsername} />},
    {key: 'approver', header: 'Approver', width: proportional(1, {minWidth: 110}), renderCell: (l) => <TextCell text={l.approverUsername ?? '—'} />},
    {key: 'status', header: 'Status', sortable: true, width: pixel(150), renderCell: (l) => <LetterStatusToken letter={l} />},
    {key: 'latest', header: 'Latest', sortable: true, width: proportional(1, {minWidth: 170}), renderCell: (l) => <TextCell text={latestStep(l)} />},
  ];
  // Narrow (a narrow window or a half view), the table drops the author and approver to fit.
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'author' && c.key !== 'approver') : allColumns;
  const noun = total === 1 ? 'letter' : 'letters';
  const subtitle = isPending ? 'Loading letters...' : `${viewLabel(view)}: ${total} ${noun}`;

  return (
    <Page
      title="Letters"
      subtitle={subtitle}
      fill
      actions={clinic.picker}
      message={isError && <AlertMessage label="Letters not loaded" message="The letters could not be loaded. Please try again." />}>
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
            list="letters"
            isUnfiltered={!search.patient && search.view === undefined}
            label="Search letters"
            placeholder="Search letters by the patient's name, or filter by view..."
            textKey="patient"
            favourites={FAVOURITES}
          />
        }
        sort={sort ? {column: sort.sort, ascending: sort.asc} : null}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as LetterSortColumn, asc: next.ascending})}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          search.patient ? (
            <EmptyState
              title="No letters match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, patient: undefined})} />}
            />
          ) : (
            <EmptyState title={EMPTY[view].title} description={EMPTY[view].description} icon={<Icon icon={AppIcon.Letters} size="lg" />} />
          )
        }
      />
    </Page>
  );
}

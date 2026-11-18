import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {Tab, TabList} from '@astryxdesign/core/TabList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {letterHref, letterKindWords, LETTER_VIEWS, letterViewLabel, type LetterSummary, type LetterView} from '../../api/letters';
import {useListSettings} from '../../app/listSettings';
import {useCurrentUser} from '../../auth/auth';
import {LetterStatusToken} from '../../components/letters/LetterCells';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {defaultLetterView, lettersListQuery, type LettersListSearch} from './lettersSearch';

type LetterRow = LetterSummary & Record<string, unknown>;

/** What each view's empty list says. */
const EMPTY: Record<LetterView, {title: string; description: string}> = {
  MyDrafts: {title: 'No drafts', description: 'Letters you start from a pathway or a referral wait here until you submit them.'},
  AwaitingMyApproval: {title: 'Nothing to approve', description: 'Letters submitted to you for approval wait here.'},
  ReturnedToMe: {title: 'Nothing returned', description: 'Letters an approver returns to you with a comment wait here.'},
  ToSend: {title: 'Nothing to send', description: 'Approved letters wait here until every recipient has been sent theirs.'},
  Sent: {title: 'Nothing sent yet', description: 'Letters sent to every recipient, newest first.'},
};

/** The view's latest step, for its Dates column: when it was approved, submitted or last changed. */
function latestStep(letter: LetterSummary): string {
  if (letter.withdrawnAtUtc) return `Withdrawn ${formatDateTime(letter.withdrawnAtUtc)}`;
  if (letter.approvedAtUtc) return `Approved ${formatDateTime(letter.approvedAtUtc)}`;
  if (letter.submittedAtUtc && letter.status === 'AwaitingApproval') return `Submitted ${formatDateTime(letter.submittedAtUtc)}`;
  return `Updated ${formatDateTime(letter.lastUpdated ?? '')}`;
}

const panelId = (view: LetterView) => `letters-${view}`;

export interface LettersPageProps {
  /** The view and page, from the URL. */
  search: LettersListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: LettersListSearch) => void;
}

/**
 * Letters (`/letters`, decision 108): the views **My drafts**, **Awaiting my approval**, **Returned to me**, **To send**
 * (approved, with a recipient not yet sent) and **Sent**, as tabs, a page at a time from the API, in its order. Each row
 * is the patient (linking to the letter), what it is (its kind and template), its author and approver, its status and
 * its latest step. The view and page live in the URL; without a view it opens on the one the person most likely wants
 * (`defaultLetterView`). Letters are started from a pathway's or referral's page.
 */
export function LettersPage({search, onSearchChange}: LettersPageProps) {
  const user = useCurrentUser();
  const view = search.view ?? defaultLetterView(user.permissions);
  const page = search.page ?? 1;
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(lettersListQuery(view, page, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as LetterRow[];
  const isNarrow = useIsNarrow();

  const allColumns: DataTableColumn<LetterRow>[] = [
    {
      key: 'patient',
      header: 'Patient',
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
      key: 'kind',
      header: 'Letter',
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
    {key: 'status', header: 'Status', width: pixel(150), renderCell: (l) => <LetterStatusToken letter={l} />},
    {key: 'dates', header: 'Latest', width: proportional(1, {minWidth: 170}), renderCell: (l) => <TextCell text={latestStep(l)} />},
  ];
  // Narrow (a narrow window or a half view), the table drops the author and approver to fit.
  const columns = isNarrow ? allColumns.filter((c) => c.key !== 'author' && c.key !== 'approver') : allColumns;
  const noun = total === 1 ? 'letter' : 'letters';
  const subtitle = isPending ? 'Loading letters...' : `${letterViewLabel(view)}: ${total} ${noun}`;

  return (
    <Page title="Letters" subtitle={subtitle} fill message={isError && <AlertMessage message="The letters could not be loaded. Please try again." />}>
      <VStack gap={4} height="100%">
        <TabList value={view} onChange={(next) => onSearchChange({view: next as LetterView})} role="tablist" hasDivider>
          {LETTER_VIEWS.map(({value, label}) => (
            <Tab key={value} value={value} label={label} panelId={panelId(value)} />
          ))}
        </TabList>
        <StackItem size="fill">
          <VStack id={panelId(view)} role="tabpanel" aria-label={letterViewLabel(view)} height="100%">
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
              emptyState={<EmptyState title={EMPTY[view].title} description={EMPTY[view].description} icon={<Icon icon={AppIcon.Letters} size="lg" />} />}
            />
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}

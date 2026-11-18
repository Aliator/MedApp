import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {
  ATTACHMENT_AUDIT_ACTIONS,
  AUDIT_ACTIONS,
  auditActionLabel,
  auditSearchQuery,
  describeAuditDetails,
  isPatientFileEntry,
  type AuditEntry,
} from '../../api/audit';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {dayFromToday, ListFilterBar, type ListFilterFavourite, type ListFilterField} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {choiceValues, hasAuditFilters, type AuditListSearch} from '../listSearch';
import {useListSettings} from '../../app/listSettings';

type EntryRow = AuditEntry & Record<string, unknown>;

export interface AuditLogPageProps {
  search: AuditListSearch;
  onSearchChange: (search: AuditListSearch) => void;
}

/** The search's fields, as the API filters the log, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'Pathway, stage or user'},
  {kind: 'choice', key: 'action', label: 'Action', options: AUDIT_ACTIONS.map((a) => ({value: a, label: auditActionLabel(a)})), isMultiple: true},
  {kind: 'date', key: 'when', label: 'When', fromKey: 'from', toKey: 'to'},
];

/** Its tabs: every entry (where it opens), recent entries, and the kinds of change most looked for. */
const favourites = (): ListFilterFavourite[] => [
  {label: 'All', values: {}},
  {label: 'Today', values: {from: dayFromToday()}},
  {label: 'Last 7 days', values: {from: dayFromToday({days: -6})}},
  {label: 'Status changes', values: {action: 'StatusChanged'}},
  {label: 'Assignments', values: {action: 'Assigned,Unassigned'}},
  {label: 'Deletions', values: {action: 'Deleted'}},
  {label: 'Files', values: {action: ATTACHMENT_AUDIT_ACTIONS.join(',')}},
];

/**
 * The pathway audit log (admin, decision 16): every change to every pathway, newest first, a page at a time.
 * Its power search filters by pathway, stage or user, actions and dates, and a pathway's View Audit Log opens it
 * on just that pathway. Entries outlive their pathways, so a deleted pathway's history is still here.
 */
/** The log's page of entries, for its search and page size (the route's loader fetches it too). */
export function auditListQuery(search: AuditListSearch, pageSize: number) {
  return auditSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Ascending: search.asc ?? false,
    Search: search.q,
    Actions: choiceValues(search.action, AUDIT_ACTIONS),
    PathwayId: search.pathwayId,
    From: search.from,
    To: search.to,
  });
}

export function AuditLogPage({search, onSearchChange}: AuditLogPageProps) {
  const {pageSize} = useListSettings();
  const page = search.page ?? 1;
  const ascending = search.asc ?? false;
  const isFiltered = hasAuditFilters(search);
  const {data, isPending, isError} = useQuery(auditListQuery(search, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as EntryRow[];
  const pathwayTitle = search.pathwayId ? rows[0]?.pathwayTitle : null;

  /** Keeps the order; clears the filters and the pathway. */
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
      key: 'pathway',
      header: 'Pathway',
      width: proportional(2, {minWidth: 96}),
      renderCell: (e) => (
        <VStack gap={0}>
          {isPatientFileEntry(e) ? (
            // A patient's own file has no pathway to link to.
            <Text type="body" weight="semibold" maxLines={1}>
              <Link href={`/patients/${e.patientId}`}>Patient file</Link>
            </Text>
          ) : (
            <Text type="body" weight="semibold" maxLines={1}>
              <Link href={`/pathways/${e.patientPathwayId}`}>{e.pathwayTitle}</Link>
            </Text>
          )}
          {e.stageName && (
            <Text type="supporting" color="secondary" maxLines={1}>
              {e.stageName}
            </Text>
          )}
        </VStack>
      ),
    },
    {key: 'action', header: 'Action', width: pixel(160), renderCell: (e) => <TextCell text={auditActionLabel(e.action)} />},
    {key: 'username', header: 'By', width: proportional(1, {minWidth: 72}), renderCell: (e) => <TextCell text={e.username} />},
    {
      key: 'details',
      header: 'Details',
      width: proportional(3, {minWidth: 120}),
      renderCell: (e) => <TextCell text={describeAuditDetails(e)} maxLines={2} />,
    },
  ];

  const noun = total === 1 ? 'entry' : 'entries';
  const subtitle = isPending
    ? 'Loading the audit log...'
    : pathwayTitle
      ? `${total} ${noun} for ${pathwayTitle}`
      : `${total} ${noun}${isFiltered ? ' found' : ''}`;

  return (
    <Page
      title="Audit Log"
      subtitle={subtitle}
      backHref={search.pathwayId ? `/pathways/${search.pathwayId}` : undefined}
      backLabel="Back to the pathway"
      fill
      message={isError && <AlertMessage message="The audit log could not be loaded. Please try again." />}
>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        // The search, keeping the pathway the log is opened on (it's not one of the bar's filters).
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={search}
            onChange={(values) => onSearchChange({...search, ...values, page: undefined})}
            list="audit"
            label="Search the audit log"
            placeholder="Search by pathway, stage or user, or filter by action and date..."
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
            title={isFiltered ? 'No entries match your search' : 'Nothing in the audit log yet'}
            description={isFiltered ? 'Try different terms or dates, or clear the search.' : 'Changes to pathways are recorded here.'}
            isCompact
            actions={isFiltered ? <Button label="Clear Search" variant="secondary" onClick={clearSearch} /> : undefined}
          />
        }
      />
    </Page>
  );
}

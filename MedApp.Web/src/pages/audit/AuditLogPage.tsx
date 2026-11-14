import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {AUDIT_ACTIONS, auditActionLabel, auditSearchQuery, describeAuditDetails, type AuditEntry} from '../../api/audit';
import {PAGE_SIZE} from '../../api/search';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {dayFromToday, ListFilterBar, type ListFilterFavourite, type ListFilterField} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {choiceValues, hasAuditFilters, type AuditListSearch} from '../listSearch';

type EntryRow = AuditEntry & Record<string, unknown>;

export interface AuditLogPageProps {
  search: AuditListSearch;
  onSearchChange: (search: AuditListSearch) => void;
}

/** The search's fields, as the API filters the log, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'Procedure, stage or user'},
  {kind: 'choice', key: 'action', label: 'Action', options: AUDIT_ACTIONS.map((a) => ({value: a, label: auditActionLabel(a)})), isMultiple: true},
  {kind: 'date', key: 'when', label: 'When', fromKey: 'from', toKey: 'to'},
];

/** Quick look-ups: recent entries, and the kinds of change most looked for. */
const favourites = (): ListFilterFavourite[] => [
  {label: 'Today', values: {from: dayFromToday()}},
  {label: 'Last 7 days', values: {from: dayFromToday({days: -6})}},
  {label: 'Status changes', values: {action: 'StatusChanged'}},
  {label: 'Assignments', values: {action: 'Assigned,Unassigned'}},
  {label: 'Deletions', values: {action: 'Deleted'}},
];

/**
 * The procedure audit log (admin, decision 16): every change to every procedure, newest first, a page at a time.
 * Its power search filters by procedure, stage or user, actions and dates, and a procedure's View Audit Log opens it
 * on just that procedure. Entries outlive their procedures, so a deleted procedure's history is still here.
 */
export function AuditLogPage({search, onSearchChange}: AuditLogPageProps) {
  const page = search.page ?? 1;
  const ascending = search.asc ?? false;
  const isFiltered = hasAuditFilters(search);
  const {data, isPending, isError} = useQuery(
    auditSearchQuery({
      Page: page,
      Ascending: ascending,
      Search: search.q,
      Actions: choiceValues(search.action, AUDIT_ACTIONS),
      ProcedureId: search.procedureId,
      From: search.from,
      To: search.to,
    }),
  );
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as EntryRow[];
  const procedureTitle = search.procedureId ? rows[0]?.procedureTitle : null;

  /** Keeps the order; clears the filters and the procedure. */
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
      key: 'procedure',
      header: 'Procedure',
      width: proportional(2, {minWidth: 96}),
      renderCell: (e) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={`/procedures/${e.patientProcedureId}`}>{e.procedureTitle}</Link>
          </Text>
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
    : procedureTitle
      ? `${total} ${noun} for ${procedureTitle}`
      : `${total} ${noun}${isFiltered ? ' found' : ''}`;

  return (
    <Page
      title="Audit Log"
      subtitle={subtitle}
      backHref={search.procedureId ? `/procedures/${search.procedureId}` : undefined}
      backLabel="Back to the procedure"
      fill
      message={isError && <AlertMessage message="The audit log could not be loaded. Please try again." />}
>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        // The search, keeping the procedure the log is opened on (it's not one of the bar's filters).
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={search}
            onChange={(values) => onSearchChange({...search, ...values, page: undefined})}
            label="Search the audit log"
            placeholder="Search by procedure, stage or user, or filter by action and date..."
            textKey="q"
            favourites={favourites()}
          />
        }
        sort={{column: 'When', ascending}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, asc: sort.ascending || undefined})}
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title={isFiltered ? 'No entries match your search' : 'Nothing in the audit log yet'}
            description={isFiltered ? 'Try different terms or dates, or clear the search.' : 'Changes to procedures are recorded here.'}
            isCompact
            actions={isFiltered ? <Button label="Clear Search" variant="secondary" onClick={clearSearch} /> : undefined}
          />
        }
      />
    </Page>
  );
}

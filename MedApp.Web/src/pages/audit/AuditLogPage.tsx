import {Button} from '@astryxdesign/core/Button';
import type {DateRange} from '@astryxdesign/core/DateRangeInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {AUDIT_ACTIONS, auditActionLabel, auditSearchQuery, describeAuditDetails, type AuditAction, type AuditEntry} from '../../api/audit';
import {PAGE_SIZE} from '../../api/search';
import {SearchDialog, type SearchValues} from '../../components/modals/SearchDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {hasAuditFilters, type AuditListSearch} from '../listSearch';

type EntryRow = AuditEntry & Record<string, unknown>;

export interface AuditLogPageProps {
  search: AuditListSearch;
  onSearchChange: (search: AuditListSearch) => void;
}

const SEARCH_FIELDS = [
  {type: 'text' as const, key: 'q', label: 'Procedure, stage or user', placeholder: 'e.g. Bronchoscopy or k.ashford'},
  {
    type: 'select' as const,
    key: 'action',
    label: 'Action',
    placeholder: 'Any action',
    options: AUDIT_ACTIONS.map((a) => ({value: a, label: auditActionLabel(a)})),
  },
  {type: 'dateRange' as const, key: 'when', label: 'Between'},
];

/**
 * The procedure audit log (admin, decision 16): every change to every procedure, newest first, a page at a time.
 * It's searchable by procedure, stage or user, action and date range, and a procedure's View Audit Log opens it
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
      Action: search.action,
      ProcedureId: search.procedureId,
      From: search.from,
      To: search.to,
    }),
  );
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [draft, setDraft] = useState<SearchValues>({});
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as EntryRow[];
  const procedureTitle = search.procedureId ? rows[0]?.procedureTitle : null;

  const openSearch = () => {
    setDraft({
      q: search.q ?? '',
      action: search.action ?? '',
      when: search.from && search.to ? ({start: search.from, end: search.to} as DateRange) : null,
    });
    setIsSearchOpen(true);
  };
  /** Keeps the order and the procedure; any change to the filters goes back to page 1. */
  const applySearch = (values: SearchValues) => {
    const q = typeof values.q === 'string' && values.q.trim() ? values.q.trim() : undefined;
    const action = typeof values.action === 'string' && values.action ? (values.action as AuditAction) : undefined;
    const when = values.when && typeof values.when === 'object' ? values.when : null;
    onSearchChange({asc: search.asc, procedureId: search.procedureId, q, action, from: when?.start, to: when?.end});
    setIsSearchOpen(false);
  };
  const clearSearch = () => {
    onSearchChange({asc: search.asc});
    setIsSearchOpen(false);
  };

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
      actions={
        <>
          {isFiltered && <Button label="Clear Search" variant="ghost" icon={<Icon icon={AppIcon.Cancel} />} onClick={clearSearch} />}
          <Button label="Search" variant="secondary" icon={<Icon icon={AppIcon.Search} />} onClick={openSearch} />
        </>
      }
      dialogs={
        <SearchDialog
          isOpen={isSearchOpen}
          title="Search the Audit Log"
          fields={SEARCH_FIELDS}
          values={draft}
          onValuesChange={setDraft}
          onSearch={() => applySearch(draft)}
          onClear={clearSearch}
          onClose={() => setIsSearchOpen(false)}
        />
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
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

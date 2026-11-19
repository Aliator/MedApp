import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {LAB_CASE_VIEWS, labCaseSearchQuery, labCaseViewLabel, type LabCaseSummary, type LabCaseView} from '../../api/labCases';
import {labCaseStatusLabel} from '../../api/hospitals';
import {useListSettings} from '../../app/listSettings';
import {Page} from '../../components/pages/Page';
import {LabClockCell} from '../../components/labCases/LabClock';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SampleSourceToken, SpecimenPatientCell} from '../../components/specimens/SpecimenCells';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {labCaseViewOf, type LabCaseSort, type LabCasesSearch} from './labCasesSearch';

type Row = LabCaseSummary & Record<string, unknown>;

const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'Case, patient, test or source'},
  {kind: 'choice', key: 'view', label: 'Showing', options: LAB_CASE_VIEWS.filter((v) => v !== 'All').map((v) => ({value: v, label: labCaseViewLabel(v)}))},
  {kind: 'choice', key: 'source', label: 'From', options: [{value: 'Clinic', label: 'A clinic'}, {value: 'Hospital', label: 'The hospital'}]},
];

/** Its tabs: Open, At risk, Overdue, To report, Reported, Repeat requested and All. */
const FAVOURITES: ListFilterFavourite[] = LAB_CASE_VIEWS.map((v) => ({label: labCaseViewLabel(v), values: v === 'All' ? {view: 'All'} : {view: v}}));

const EMPTY: Record<LabCaseView, string> = {
  Open: 'No open cases',
  AtRisk: 'No case is at risk',
  Overdue: 'No case is overdue',
  ToReport: 'Nothing to report',
  Reported: 'No reported cases',
  RepeatRequested: 'No repeat samples asked for',
  All: 'No lab cases yet',
};

/** The tab's page of cases, for its filters, sort and page size, and the text searched (in the body, never the URL). */
export function labCasesListQuery(search: LabCasesSearch, pageSize: number, text?: string) {
  return labCaseSearchQuery({
    view: labCaseViewOf(search),
    source: search.source,
    page: search.page ?? 1,
    pageSize,
    search: text,
    sort: search.sort,
    ascending: search.sort ? (search.asc ?? true) : undefined,
  });
}

export interface LabCasesPageProps {
  search: LabCasesSearch;
  onSearchChange: (search: LabCasesSearch) => void;
}

const asString = (value: unknown) => (typeof value === 'string' && value ? value : undefined);

/**
 * Lab Cases (`/lab/cases`, MC15): the lab's work, a case per request, from the clinics and the hospital. Tabs: Open
 * (soonest due first), At risk and Overdue (MC19's clocks), To report (every step done, "No more work" recorded),
 * Reported, Repeat requested and All. The Due column is the case's clock: the countdown, the due date (the sooner of the
 * lab's target and the hospital's date) and a flag when at risk or overdue.
 * Each row is the case number with its test (opening the case), the patient with their NHS number, where it came from,
 * what it needs next and when it was opened. Search by case, patient, NHS number, test or source (in the body, never
 * the URL). The lab sees every case, a clinic its own; every patient shown is recorded as listed. Lists order by times
 * only (decision 191).
 */
export function LabCasesPage({search, onSearchChange}: LabCasesPageProps) {
  const view = labCaseViewOf(search);
  const [text, setText] = useState<string>();
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(labCasesListQuery(search, pageSize, text));
  const total = data?.totalCount ?? 0;
  // Open work is in due order (MC19), soonest first; the rest newest first.
  const dueOrder = view === 'Open' || view === 'AtRisk' || view === 'Overdue' || view === 'ToReport';
  const sort = search.sort ?? (dueOrder ? 'due' : 'opened');
  const asc = search.sort ? (search.asc ?? true) : dueOrder;

  const fromBar = (values: ListFilterValues) => {
    setText(asString(values.q));
    onSearchChange({
      view: asString(values.view) as LabCaseView | undefined,
      source: asString(values.source) as LabCasesSearch['source'],
      sort: search.sort,
      asc: search.asc,
    });
  };

  const columns: DataTableColumn<Row>[] = [
    {
      key: 'number',
      header: 'Lab case',
      sortable: true,
      width: proportional(1.3, {minWidth: 120}),
      renderCell: (c) => (
        <VStack gap={0} hAlign="start">
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={`/lab/cases/${c.id}`}>{c.number}</Link>
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {c.labTestName}
          </Text>
        </VStack>
      ),
    },
    {key: 'patient', header: 'Patient', sortable: true, width: proportional(2, {minWidth: 120}), renderCell: (c) => <SpecimenPatientCell specimen={c} />},
    {
      key: 'source',
      header: 'From',
      sortable: true,
      width: proportional(1.5, {minWidth: 120}),
      renderCell: (c) => (
        <VStack gap={1} hAlign="start">
          <SampleSourceToken source={c.source} />
          <Text type="supporting" color="secondary" maxLines={1}>
            {c.sourceName || '—'}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'next',
      header: 'Next',
      width: proportional(1.5, {minWidth: 120}),
      renderCell: (c) => <TextCell text={c.status === 'Open' ? (c.nextName ?? '—') : labCaseStatusLabel(c.status)} />,
    },
    {key: 'due', header: 'Due', sortable: true, width: pixel(150), renderCell: (c) => <LabClockCell clock={c.clock} />},
    {key: 'opened', header: 'Opened', sortable: true, width: pixel(150), renderCell: (c) => <TextCell text={formatDateTime(c.openedAtUtc ?? '')} />},
  ];

  const noun = total === 1 ? 'case' : 'cases';
  return (
    <Page
      title="Lab Cases"
      subtitle={isPending ? 'Loading lab cases...' : `${labCaseViewLabel(view)}: ${total} ${text || search.source ? `matching ${noun}` : noun}`}
      fill
      message={isError && <AlertMessage message="The lab cases could not be loaded. Please try again." />}>
      <DataTable
        data={(data?.items ?? []) as Row[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={{q: text, view, source: search.source}}
            onChange={fromBar}
            list="lab-cases"
            isUnfiltered={!text && !search.source && search.view === undefined}
            label="Search lab cases"
            placeholder="Search by case, patient, NHS number, test or source..."
            textKey="q"
            favourites={FAVOURITES}
          />
        }
        sort={{column: sort, ascending: asc}}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as LabCaseSort, asc: next.ascending})}
        pagination={{
          page: search.page ?? 1,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title={text ? 'No cases match your search' : EMPTY[view]}
            description={text ? undefined : 'Each sample the lab receives is worked in a lab case: its steps, its outcomes and its report.'}
            icon={<Icon icon={text ? AppIcon.Search : AppIcon.LabCases} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}

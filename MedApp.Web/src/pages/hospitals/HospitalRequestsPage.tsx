import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {
  hospitalRequestSearchQuery,
  hospitalsQuery,
  LAB_REQUEST_PRIORITIES,
  type HospitalRequestSummary,
} from '../../api/hospitals';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {LabRequestPriorityToken} from '../../components/hospitals/HospitalRequestCells';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SpecimenPatientCell} from '../../components/specimens/SpecimenCells';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {formatDate, formatDateTime} from '../../utils/dates';
import type {HospitalRequestsSearch, HospitalRequestSort} from './hospitalRequestsSearch';

type Row = HospitalRequestSummary & Record<string, unknown>;

const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'q', label: 'Case, patient or number'},
  {kind: 'choice', key: 'priority', label: 'Priority', options: LAB_REQUEST_PRIORITIES.map((p) => ({value: p.value, label: p.label}))},
];

/** The page of requests for the address's hospital, page and sort, and the text searched (in the body, never the URL). */
export function hospitalRequestsListQuery(search: HospitalRequestsSearch, pageSize: number, text?: string, priority?: string) {
  return hospitalRequestSearchQuery({
    hospitalId: search.hospital,
    priority,
    page: search.page ?? 1,
    pageSize,
    search: text,
    sort: search.sort,
    ascending: search.sort ? (search.asc ?? true) : undefined,
  });
}

export interface HospitalRequestsPageProps {
  search: HospitalRequestsSearch;
  onSearchChange: (search: HospitalRequestsSearch) => void;
}

/**
 * Hospital Requests (`/hospital-requests`, MC18): the requests a hospital sent the lab, each with its lab case, newest
 * registered first. Each row is the case number (opening the request), the patient with their NHS number, the
 * hospital's own patient number, the hospital, the priority as the request gave it (and a cancer pathway), and when the
 * report is needed by. Search by case, patient, NHS or hospital number (in the request's body, never the URL), filter
 * by priority, and narrow to one hospital. The lab registers new ones (`ProcessSpecimens`); the cases are the lab's, so
 * anyone outside it sees none. Every patient shown is recorded as listed.
 */
export function HospitalRequestsPage({search, onSearchChange}: HospitalRequestsPageProps) {
  const navigate = useNavigate();
  const canRegister = useCan('ProcessSpecimens');
  const [text, setText] = useState<string>();
  const [priority, setPriority] = useState<string>();
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(hospitalRequestsListQuery(search, pageSize, text, priority));
  const hospitals = useQuery(hospitalsQuery(true));
  const total = data?.totalCount ?? 0;
  const sort = search.sort ?? 'registered';
  const asc = search.sort ? (search.asc ?? true) : false;

  const fromBar = (values: ListFilterValues) => {
    setText(typeof values.q === 'string' ? values.q : undefined);
    setPriority(typeof values.priority === 'string' ? values.priority : undefined);
    onSearchChange({...search, page: undefined});
  };

  const columns: DataTableColumn<Row>[] = [
    {
      key: 'case',
      header: 'Lab case',
      sortable: true,
      width: proportional(1.2, {minWidth: 120}),
      renderCell: (r) => (
        <Text type="body" weight="semibold" maxLines={1}>
          <Link href={`/hospital-requests/${r.id}`}>{r.labCaseNumber}</Link>
        </Text>
      ),
    },
    {key: 'patient', header: 'Patient', sortable: true, width: proportional(2, {minWidth: 120}), renderCell: (r) => <SpecimenPatientCell specimen={r} />},
    {key: 'number', header: 'Hospital number', width: proportional(1, {minWidth: 100}), renderCell: (r) => <TextCell text={r.hospitalPatientNumber} />},
    {key: 'hospital', header: 'Hospital', sortable: true, width: proportional(1.5, {minWidth: 100}), renderCell: (r) => <TextCell text={r.hospitalName} maxLines={2} />},
    {
      key: 'priority',
      header: 'Priority',
      width: pixel(150),
      renderCell: (r) => (
        <VStack gap={1} hAlign="start">
          <LabRequestPriorityToken priority={r.priority} />
          {r.isCancerPathway && (
            <Text type="supporting" color="secondary">
              Cancer pathway
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'needed',
      header: 'Report needed by',
      sortable: true,
      width: pixel(150),
      renderCell: (r) => <TextCell text={r.reportNeededBy ? formatDate(r.reportNeededBy) : 'No date given'} />,
    },
    {key: 'registered', header: 'Registered', sortable: true, width: pixel(150), renderCell: (r) => <TextCell text={formatDateTime(r.registeredAtUtc ?? '')} />},
  ];

  const noun = total === 1 ? 'request' : 'requests';
  const hospitalName = hospitals.data?.find((h) => h.id === search.hospital)?.name;
  return (
    <Page
      title="Hospital Requests"
      subtitle={isPending ? 'Loading requests...' : `${total} ${noun}${hospitalName ? ` from ${hospitalName}` : ''}`}
      fill
      message={isError && <AlertMessage message="The hospital requests could not be loaded. Please try again." />}
      actions={
        <HStack gap={2} vAlign="center">
          <AnchoredSelector
            label="Hospital"
            isLabelHidden
            options={[{value: '', label: 'All hospitals'}, ...(hospitals.data ?? []).map((h) => ({value: h.id ?? '', label: h.name ?? ''}))]}
            value={search.hospital ?? ''}
            onChange={(hospital: string) => onSearchChange({...search, page: undefined, hospital: hospital || undefined})}
            width={220}
            containerStyle={NO_STATUS_LINE}
          />
          {canRegister && (
            <Button label="Register Request" icon={<Icon icon={AppIcon.Add} />} onClick={() => void navigate({to: '/hospital-requests/new'})} />
          )}
        </HStack>
      }>
      <DataTable
        data={(data?.items ?? []) as Row[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={{q: text, priority}}
            onChange={fromBar}
            list="hospital-requests"
            isUnfiltered={!text && !priority}
            label="Search hospital requests"
            placeholder="Search by case, patient, NHS or hospital number, or filter by priority..."
            textKey="q"
          />
        }
        sort={{column: sort, ascending: asc}}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as HospitalRequestSort, asc: next.ascending})}
        pagination={{
          page: search.page ?? 1,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title={text || priority ? 'No requests match your search' : 'No hospital requests yet'}
            description={text || priority ? undefined : 'Requests the lab registers from a hospital show here, newest first.'}
            icon={<Icon icon={text ? AppIcon.Search : AppIcon.Lab} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}

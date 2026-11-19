import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import type {Patient} from '../../api/patients';
import {patientSearchQuery} from '../../api/search';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {AvatarCell} from '../../components/tables/AvatarCell';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {dayFromToday, ListFilterBar, type ListFilterField} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import {useClinicPicker} from '../../components/teams/ClinicPicker';
import {ageOn, formatShortDate} from '../../utils/dates';
import {formatNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {hasPatientFilters, PATIENT_SORTS, type PatientSortColumn, type PatientsListSearch} from '../listSearch';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';

/** The search's fields, as the API filters patients, kept in the URL under these keys. */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'first', label: 'First name'},
  {kind: 'text', key: 'last', label: 'Last name'},
  {kind: 'text', key: 'email', label: 'Email'},
  {kind: 'date', key: 'dob', label: 'Date of birth', fromKey: 'dobFrom', toKey: 'dobTo'},
  {kind: 'text', key: 'nhs', label: 'NHS number'},
];

type PatientRow = Patient & Record<string, unknown>;

export interface PatientsListPageProps {
  /** The page, sort and filters, from the URL. */
  search: PatientsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: PatientsListSearch) => void;
}

/**
 * Patients list (replaces Blazor's PatientsTable): one page of patients from the API, sortable, with a power
 * search and links to view, edit and add. The page, sort and filters live in the URL.
 */
/**
 * The list's page of patients, for its search, page size and the clinic picker's team (patients with a pathway,
 * referral or appointment there). The route's loader fetches it too.
 */
export function patientsListQuery(search: PatientsListSearch, pageSize: number, teamId?: string) {
  return patientSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: PATIENT_SORTS[search.sort ?? 'Patients'],
    Ascending: search.asc ?? true,
    FirstName: search.first,
    LastName: search.last,
    Email: search.email,
    DateOfBirthFrom: search.dobFrom,
    DateOfBirthTo: search.dobTo,
    // Spaces mean nothing in an NHS number (the API ignores them too), so they aren't sent.
    NhsNumber: search.nhs ? normaliseNhsNumber(search.nhs) : undefined,
    TeamId: teamId,
  });
}

export function PatientsListPage({search, onSearchChange}: PatientsListPageProps) {
  // Adding and editing patients needs EditPatients.
  const canAdd = useCan('EditPatients');
  const {pageSize} = useListSettings();
  const page = search.page ?? 1;
  const sortColumn: PatientSortColumn = search.sort ?? 'Patients';
  const ascending = search.asc ?? true;
  const clinic = useClinicPicker('patients', search.clinic, (next) => onSearchChange({...search, clinic: next, page: undefined}));
  const isFiltered = hasPatientFilters(search);

  const {data, isPending, isError} = useQuery(patientsListQuery(search, pageSize, clinic.teamId));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as PatientRow[];

  /** Keeps the sort and the clinic; any change to the filters goes back to page 1. */
  const clearSearch = () => onSearchChange({sort: search.sort, asc: search.asc, clinic: search.clinic});

  const columns: DataTableColumn<PatientRow>[] = [
    {
      key: 'Patients',
      header: 'Patients',
      sortable: true,
      renderCell: (p) => <AvatarCell primaryText={`${p.firstName} ${p.lastName}`} secondaryText={`ID: ${p.id?.slice(0, 8)}`} />,
    },
    {
      key: 'DateOfBirth',
      header: 'Date of Birth',
      sortable: true,
      renderCell: (p) => (
        <HStack gap={2} vAlign="center">
          <Text type="body">{formatShortDate(p.dateOfBirth ?? '')}</Text>
          <Text type="supporting" color="secondary">
            {ageOn(p.dateOfBirth ?? '')} yrs
          </Text>
        </HStack>
      ),
    },
    {
      key: 'nhsNumber',
      header: 'NHS Number',
      renderCell: (p) => (
        <Text type="body" hasTabularNumbers>
          {p.nhsNumber ? formatNhsNumber(p.nhsNumber) : '—'}
        </Text>
      ),
    },
    {
      key: 'Email',
      header: 'Email',
      sortable: true,
      renderCell: (p) => (
        <Text type="body" maxLines={1}>
          <Link href={`mailto:${p.email}`}>{p.email}</Link>
        </Text>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      renderCell: (p) => (
        <TableActions
          actions={[
            {label: 'View patient', icon: AppIcon.View, href: `/patients/${p.id}`},
            ...(canAdd ? [{label: 'Edit patient', icon: AppIcon.Edit, href: `/patients/${p.id}/edit`}] : []),
            {label: 'Pathways', icon: AppIcon.Pathways, href: `/patients/${p.id}/pathways`},
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending
    ? 'Loading patients...'
    : isFiltered
      ? `${total} matching ${total === 1 ? 'patient' : 'patients'}`
      : `${total} ${total === 1 ? 'patient' : 'patients'} ${clinic.teamName ? `in ${clinic.teamName}` : 'registered'}`;

  const noPatientsAtAll = !isPending && !isError && !isFiltered && !clinic.teamId && total === 0;

  return (
    <Page
      title="Patients"
      subtitle={subtitle}
      fill
      message={isError && <AlertMessage label="Patients not loaded" message="The patients could not be loaded. Please try again." />}
      actions={
        <HStack gap={2} vAlign="center">
          {clinic.picker}
          {canAdd && <Button label="Add Patient" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/patients/add" />}
        </HStack>
      }>
      {noPatientsAtAll ? (
        <EmptyState
          title="No patients yet"
          description="Get started by adding your first patient to the system."
          icon={<Icon icon={AppIcon.Patients} size="lg" />}
          actions={canAdd && <Button label="Add Your First Patient" variant="primary" href="/patients/add" />}
        />
      ) : (
        <DataTable
          data={rows}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          skeletonRows={pageSize}
          // The search, as a power search across the top of the card: a token per filter, in the URL like the
          // page and sort.
          search={
            <ListFilterBar
              fields={FILTER_FIELDS}
              values={search}
              onChange={(filters) => onSearchChange({...search, ...filters, page: undefined})}
              list="patients"
              label="Search patients"
              placeholder="Search patients by first name, last name, email, date of birth or NHS number..."
              favourites={[
                {label: 'All', values: {}},
                {label: 'Under 18', values: {dobFrom: dayFromToday({years: -18, days: 1})}},
                {label: '65 and over', values: {dobTo: dayFromToday({years: -65})}},
              ]}
            />
          }
          sort={{column: sortColumn, ascending}}
          onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as PatientSortColumn, asc: sort.ascending})}
          pagination={{
            page,
            pageSize,
            totalItems: total,
            onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
          }}
          emptyState={
            <EmptyState
              title="No patients match your search"
              description="Try different search terms, or clear the search to see every patient."
              isCompact
              actions={<Button label="Clear Search" variant="secondary" onClick={clearSearch} />}
            />
          }
        />
      )}
    </Page>
  );
}

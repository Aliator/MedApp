import {Button} from '@astryxdesign/core/Button';
import type {DateRange} from '@astryxdesign/core/DateRangeInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import type {Patient} from '../../api/patients';
import {PAGE_SIZE, patientSearchQuery} from '../../api/search';
import {SearchDialog, type SearchField, type SearchValues} from '../../components/modals/SearchDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {AvatarCell} from '../../components/tables/AvatarCell';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {ageOn, formatShortDate} from '../../utils/dates';
import {hasPatientFilters, PATIENT_SORTS, type PatientSortColumn, type PatientsListSearch} from '../listSearch';

const SEARCH_FIELDS: SearchField[] = [
  {type: 'text', key: 'first', label: 'First Name', placeholder: 'First name'},
  {type: 'text', key: 'last', label: 'Last Name', placeholder: 'Last name'},
  {type: 'text', key: 'email', label: 'Email', placeholder: 'Email address'},
  {type: 'dateRange', key: 'dob', label: 'Date of birth'},
];

type PatientRow = Patient & Record<string, unknown>;

export interface PatientsListPageProps {
  /** The page, sort and filters, from the URL. */
  search: PatientsListSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: PatientsListSearch) => void;
}

/**
 * Patients list (replaces Blazor's PatientsTable): one page of patients from the API, sortable, with a search
 * dialog and links to view, edit and add. The page, sort and filters live in the URL.
 */
export function PatientsListPage({search, onSearchChange}: PatientsListPageProps) {
  const page = search.page ?? 1;
  const sortColumn: PatientSortColumn = search.sort ?? 'Patients';
  const ascending = search.asc ?? true;
  const isFiltered = hasPatientFilters(search);

  const {data, isPending, isError} = useQuery(
    patientSearchQuery({
      Page: page,
      Sort: PATIENT_SORTS[sortColumn],
      Ascending: ascending,
      FirstName: search.first,
      LastName: search.last,
      Email: search.email,
      DateOfBirthFrom: search.dobFrom,
      DateOfBirthTo: search.dobTo,
    }),
  );
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as PatientRow[];

  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [draft, setDraft] = useState<SearchValues>({});

  const openSearch = () => {
    setDraft({
      first: search.first ?? '',
      last: search.last ?? '',
      email: search.email ?? '',
      dob: search.dobFrom && search.dobTo ? ({start: search.dobFrom, end: search.dobTo} as DateRange) : null,
    });
    setIsSearchOpen(true);
  };

  /** Keeps the sort; any change to the filters goes back to page 1. */
  const applySearch = (values: SearchValues) => {
    const text = (key: string) => {
      const value = values[key];
      return typeof value === 'string' && value.trim() ? value.trim() : undefined;
    };
    const dob = values.dob && typeof values.dob === 'object' ? values.dob : null;
    onSearchChange({
      sort: search.sort,
      asc: search.asc,
      first: text('first'),
      last: text('last'),
      email: text('email'),
      dobFrom: dob?.start,
      dobTo: dob?.end,
    });
    setIsSearchOpen(false);
  };

  const clearSearch = () => {
    onSearchChange({sort: search.sort, asc: search.asc});
    setIsSearchOpen(false);
  };

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
      key: 'Email',
      header: 'Email',
      sortable: true,
      renderCell: (p) => <Link href={`mailto:${p.email}`}>{p.email}</Link>,
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      renderCell: (p) => (
        <TableActions
          actions={[
            {label: 'View patient', icon: AppIcon.View, href: `/patients/${p.id}`},
            {label: 'Edit patient', icon: AppIcon.Edit, href: `/patients/${p.id}/edit`},
            {label: 'Procedures', icon: AppIcon.Procedures, href: `/patients/${p.id}/procedures`},
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending
    ? 'Loading patients...'
    : isFiltered
      ? `${total} matching ${total === 1 ? 'patient' : 'patients'}`
      : `${total} ${total === 1 ? 'patient' : 'patients'} registered`;

  const noPatientsAtAll = !isPending && !isError && !isFiltered && total === 0;

  return (
    <Page
      title="Patients"
      subtitle={subtitle}
      fill
      actions={
        <>
          {isFiltered && (
            <Button label="Clear Search" variant="ghost" icon={<Icon icon={AppIcon.Cancel} />} onClick={clearSearch} />
          )}
          <Button label="Search" variant="secondary" icon={<Icon icon={AppIcon.Search} />} onClick={openSearch} />
          <Button label="Add Patient" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/patients/add" />
        </>
      }
      dialogs={
        <SearchDialog
          isOpen={isSearchOpen}
          title="Search Patients"
          fields={SEARCH_FIELDS}
          values={draft}
          onValuesChange={setDraft}
          onSearch={() => applySearch(draft)}
          onClear={clearSearch}
          onClose={() => setIsSearchOpen(false)}
        />
      }>
      {isError && <AlertMessage message="The patients could not be loaded. Please try again." />}
      {noPatientsAtAll ? (
        <EmptyState
          title="No patients yet"
          description="Get started by adding your first patient to the system."
          icon={<Icon icon={AppIcon.Patients} size="lg" />}
          actions={<Button label="Add Your First Patient" variant="primary" href="/patients/add" />}
        />
      ) : (
        <DataTable
          data={rows}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          skeletonRows={PAGE_SIZE}
          sort={{column: sortColumn, ascending}}
          onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as PatientSortColumn, asc: sort.ascending})}
          pagination={{
            page,
            pageSize: PAGE_SIZE,
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

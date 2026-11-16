import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {appointmentsQuery, type Appointment, type AppointmentSearch} from '../../api/appointments';
import {procedureQuery} from '../../api/procedures';
import {useListSettings} from '../../app/listSettings';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {Page} from '../../components/pages/Page';
import {DueDateCell} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterField} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {ukToday} from '../../utils/calendarSlots';
import {formatShortDate, ukDay} from '../../utils/dates';
import {NEEDS_BOOKING_SORTS, type NeedsBookingSearch, type NeedsBookingSortColumn} from './calendarSearch';

type Row = Appointment & Record<string, unknown>;

/** The API search behind the list: Requested appointments, soonest procedure due date first by default. */
export function needsBookingQuery(search: NeedsBookingSearch, pageSize?: number): AppointmentSearch {
  return {
    Statuses: ['Requested'],
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: NEEDS_BOOKING_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Search: search.patient,
  };
}

const FILTER_FIELDS: ListFilterField[] = [{kind: 'text', key: 'patient', label: 'Patient'}];

export interface NeedsBookingPageProps {
  search: NeedsBookingSearch;
  onSearchChange: (search: NeedsBookingSearch) => void;
}

/**
 * Needs booking (docs/calendar-plan.md, decision 29): the appointments asked for but not yet booked (Requested), as a
 * list like the others, soonest procedure due date first, with overdue procedures flagged. Each row books its
 * appointment in `BookAppointmentDialog`, suggesting the procedure's assignee when it has exactly one.
 */
export function NeedsBookingPage({search, onSearchChange}: NeedsBookingPageProps) {
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(appointmentsQuery(needsBookingQuery(search, pageSize)));
  const [booking, setBooking] = useState<Appointment | null>(null);
  // The procedure of the appointment being booked, for its assignee (fetched when the dialog opens).
  const procedure = useQuery({...procedureQuery(booking?.procedureId ?? ''), enabled: !!booking?.procedureId});
  const assignees = procedure.data?.assignments ?? [];
  const today = ukToday();

  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as Row[];
  const isFiltered = search.patient !== undefined;

  const columns: DataTableColumn<Row>[] = [
    {
      key: 'Patient',
      header: 'Patient',
      sortable: true,
      width: proportional(1.5, {minWidth: 72}),
      renderCell: (a) => (
        <Text type="body" weight="semibold" maxLines={2}>
          <Link href={`/patients/${a.patientId}`}>{a.patientName ?? ''}</Link>
        </Text>
      ),
    },
    {
      key: 'for',
      header: 'For',
      width: proportional(2, {minWidth: 96}),
      renderCell: (a) =>
        a.procedureId ? (
          <VStack gap={0.5}>
            <Text type="body" maxLines={1}>
              <Link href={`/procedures/${a.procedureId}`}>{a.procedureTitle ?? ''}</Link>
            </Text>
            {a.stageName && (
              <Text type="supporting" color="secondary" maxLines={1}>
                {a.stageName}
              </Text>
            )}
          </VStack>
        ) : (
          <TextCell text="Appointment" />
        ),
    },
    {key: 'visit', header: 'Visit', width: pixel(132), renderCell: (a) => <VisitRequirementToken requirement={a.visitRequirement} />},
    {
      key: 'Due',
      header: 'Due',
      sortable: true,
      width: pixel(112),
      renderCell: (a) => (a.procedureDueDate ? <DueDateCell dueDate={a.procedureDueDate} isOverdue={a.procedureDueDate < today} /> : <TextCell text="—" />),
    },
    {
      key: 'Requested',
      header: 'Requested',
      sortable: true,
      width: proportional(1, {minWidth: 96}),
      renderCell: (a) => (
        <VStack gap={0.5}>
          <Text type="body" maxLines={1}>
            {a.isAutomatic ? 'Automatic' : (a.createdByUsername ?? '')}
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {a.createdAtUtc ? formatShortDate(ukDay(a.createdAtUtc)) : ''}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(104),
      renderCell: (a) => (
        <VStack hAlign="end">
          <Button label="Book" size="sm" variant="primary" icon={<Icon icon={AppIcon.Book} />} onClick={() => setBooking(a)} />
        </VStack>
      ),
    },
  ];

  const noun = total === 1 ? 'appointment' : 'appointments';
  const subtitle = isPending ? 'Loading...' : isFiltered ? `${total} matching ${noun}` : `${total} ${noun} to book`;

  return (
    <Page
      title="Needs Booking"
      subtitle={subtitle}
      fill
      backHref="/calendar"
      backLabel="Back to the calendar"
      message={isError && <AlertMessage message="The appointments could not be loaded. Please try again." />}
      dialogs={
        <BookAppointmentDialog
          isOpen={booking !== null}
          appointment={booking ?? undefined}
          suggestedStaffUserId={assignees.length === 1 ? assignees[0].userId : undefined}
          onClose={() => setBooking(null)}
        />
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={search}
            onChange={(values) => onSearchChange({...search, patient: values.patient as string | undefined, page: undefined})}
            list="needs-booking"
            label="Search appointments that need booking"
            placeholder="Search by patient..."
            textKey="patient"
          />
        }
        sort={{column: search.sort ?? 'Due', ascending: search.asc ?? true}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as NeedsBookingSortColumn, asc: sort.ascending})}
        pagination={{
          page: search.page ?? 1,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          isFiltered ? (
            <EmptyState
              title="No appointments match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({sort: search.sort, asc: search.asc})} />}
            />
          ) : (
            <EmptyState title="Nothing needs booking" icon={<Icon icon={AppIcon.Success} size="lg" />} />
          )
        }
      />
    </Page>
  );
}

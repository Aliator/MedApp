import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {Section} from '@astryxdesign/core/Section';
import {SegmentedControl, SegmentedControlItem} from '@astryxdesign/core/SegmentedControl';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type CSSProperties} from 'react';
import {appointmentsQuery, didNotAttendSearch, type Appointment, type AppointmentSearch} from '../../api/appointments';
import {pathwayQuery} from '../../api/pathways';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {AppointmentDialog} from '../../components/calendar/AppointmentDialog';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {DnaFollowUpDialog, dnaFollowUpWords} from '../../components/calendar/DnaFollowUpDialog';
import {Page} from '../../components/pages/Page';
import {DueDateCell} from '../../components/pathways/PathwayCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterField} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {useClinicPicker} from '../../components/teams/ClinicPicker';
import {ukToday} from '../../utils/calendarSlots';
import {formatAppointmentTime, formatShortDate, ukDay} from '../../utils/dates';
import {REQUIRES_APPOINTMENT_SORTS, type RequiresAppointmentSearch, type RequiresAppointmentSortColumn} from './calendarSearch';

/** The list switch's unselected choice in the primary text colour: the secondary one fails contrast on its background (as the Calendar's View control). */
const PRIMARY_TEXT = {'--color-text-secondary': 'var(--color-text-primary)'} as CSSProperties;

type Row = Appointment & Record<string, unknown>;

/** The API search behind the list: Requested appointments, soonest pathway target date first by default. */
export function requiresAppointmentQuery(search: RequiresAppointmentSearch, pageSize?: number): AppointmentSearch {
  return {
    Statuses: ['Requested'],
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: REQUIRES_APPOINTMENT_SORTS[search.sort ?? 'Due'],
    Ascending: search.asc ?? true,
    Search: search.patient,
  };
}

/**
 * The search behind whichever list the page shows: the appointments to book, or the Did not attend list (oldest
 * first); narrowed to the clinic picker's team, if one is picked.
 */
export function requiresAppointmentPageQuery(search: RequiresAppointmentSearch, pageSize?: number, teamId?: string): AppointmentSearch {
  const query =
    search.view === 'didNotAttend'
      ? didNotAttendSearch({page: search.page, pageSize, patient: search.patient})
      : requiresAppointmentQuery(search, pageSize);
  return teamId ? {...query, TeamId: teamId} : query;
}

/** What the Did not attend list is, said above it. */
export const DID_NOT_ATTEND_INTRO =
  'Appointments marked Did not attend with no later appointment. They stay here until another is booked or someone records No further appointment.';

/** "Missed on this pathway: 2", or for an appointment not for a pathway, among the patient's others not for one. */
export const missedWords = (a: Pick<Appointment, 'pathwayId' | 'missedOnPathwayCount'>) =>
  `${a.pathwayId ? 'Missed on this pathway' : 'Missed, not for a pathway'}: ${a.missedOnPathwayCount ?? 1}`;

const FILTER_FIELDS: ListFilterField[] = [{kind: 'text', key: 'patient', label: 'Patient'}];

export interface RequiresAppointmentPageProps {
  search: RequiresAppointmentSearch;
  onSearchChange: (search: RequiresAppointmentSearch) => void;
}

/**
 * Requires Appointment (docs/calendar-plan.md, decision 29): the appointments asked for but not yet booked (Requested), as a
 * list like the others, soonest pathway target date first, with overdue pathways flagged. Each row books its
 * appointment in `BookAppointmentDialog`, suggesting the pathway's assignee when it has exactly one; a row whose
 * patient's record is restricted or anonymised can't be booked, and says so. It has no side nav item: it's reached from
 * the Calendar page's "Requires Appointment (N)" link, and its Back button returns there.
 *
 * Its **Did not attend** list (`view=didNotAttend` in the URL, chosen in the header; centre plan decision 132) shows the
 * appointments marked Did not attend with no later appointment for the same stage or pathway, oldest first: who, when
 * it was, what for, how many were missed there, and the follow-up recorded so far. Each can have a follow-up recorded
 * (`DnaFollowUpDialog`) or be opened (`AppointmentDialog`, to book another). Recording one never changes the pathway.
 */
export function RequiresAppointmentPage({search, onSearchChange}: RequiresAppointmentPageProps) {
  const {pageSize} = useListSettings();
  const isDidNotAttend = search.view === 'didNotAttend';
  const clinic = useClinicPicker('appointments', search.clinic, (next) => onSearchChange({...search, clinic: next, page: undefined}));
  const {data, isPending, isError} = useQuery(appointmentsQuery(requiresAppointmentPageQuery(search, pageSize, clinic.teamId)));
  const [booking, setBooking] = useState<Appointment | null>(null);
  const [opened, setOpened] = useState<Appointment | null>(null);
  const [followingUp, setFollowingUp] = useState<Appointment | null>(null);
  // Booking needs BookAppointments; without it, the list only shows what's waiting.
  const canBook = useCan('BookAppointments');
  // The pathway of the appointment being booked, for its assignee (fetched when the dialog opens).
  const pathway = useQuery({...pathwayQuery(booking?.pathwayId ?? ''), enabled: !!booking?.pathwayId});
  const assignees = pathway.data?.assignments ?? [];
  const today = ukToday();

  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as Row[];
  const isFiltered = search.patient !== undefined;

  const patientColumn: DataTableColumn<Row> = {
    key: 'Patient',
    header: 'Patient',
    sortable: !isDidNotAttend,
    width: proportional(1.5, {minWidth: 72}),
    renderCell: (a) => (
      <Text type="body" weight="semibold" maxLines={2}>
        <Link href={`/patients/${a.patientId}`}>{a.patientName ?? ''}</Link>
      </Text>
    ),
  };

  // Booking, for whoever can book.
  const actionsColumn: DataTableColumn<Row> = {
    key: 'actions',
    header: 'Actions',
    align: 'end',
    width: pixel(104),
    // Not when the patient's record is restricted or anonymised (the API's `canBook`); the patient's page says why.
    renderCell: (a) => (
      <VStack hAlign="end">
        {a.canBook ? (
          <Button label="Book" size="sm" variant="primary" icon={<Icon icon={AppIcon.Book} />} onClick={() => setBooking(a)} />
        ) : (
          <Text type="supporting" color="secondary" maxLines={2}>
            Can’t be booked
          </Text>
        )}
      </VStack>
    ),
  };
  const columns: DataTableColumn<Row>[] = [
    patientColumn,
    {
      key: 'for',
      header: 'For',
      width: proportional(2, {minWidth: 96}),
      renderCell: (a) =>
        a.pathwayId ? (
          <VStack gap={0.5}>
            <Text type="body" maxLines={1}>
              <Link href={`/pathways/${a.pathwayId}`}>{a.pathwayTitle ?? ''}</Link>
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
      renderCell: (a) => (a.pathwayDueDate ? <DueDateCell dueDate={a.pathwayDueDate} isOverdue={a.pathwayDueDate < today} /> : <TextCell text="—" />),
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
    ...(canBook ? [actionsColumn] : []),
  ];

  // The Did not attend list: when it was, what for (with how many were missed there), the follow-up so far, and actions.
  const didNotAttendColumns: DataTableColumn<Row>[] = [
    patientColumn,
    {
      key: 'when',
      header: 'Missed',
      width: proportional(1.5, {minWidth: 96}),
      renderCell: (a) => (
        <VStack gap={0.5}>
          <Text type="body" maxLines={2}>
            {a.startUtc ? formatAppointmentTime(a.startUtc, a.endUtc) : ''}
          </Text>
          {a.staffUsername && (
            <Text type="supporting" color="secondary" maxLines={1}>
              {`With ${a.staffUsername}`}
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'for',
      header: 'For',
      width: proportional(2, {minWidth: 96}),
      renderCell: (a) => (
        <VStack gap={0.5}>
          {a.pathwayId ? (
            <Text type="body" maxLines={1}>
              <Link href={`/pathways/${a.pathwayId}`}>{a.stageName ? `${a.pathwayTitle ?? ''}: ${a.stageName}` : (a.pathwayTitle ?? '')}</Link>
            </Text>
          ) : (
            <Text type="body" maxLines={1}>
              Appointment
            </Text>
          )}
          <Text type="supporting" color="secondary" maxLines={1}>
            {missedWords(a)}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'followUp',
      header: 'Follow-up',
      width: proportional(1.5, {minWidth: 96}),
      renderCell: (a) => <TextCell text={dnaFollowUpWords(a) ?? 'None yet'} />,
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(240),
      // Each button names its patient, so a screen reader tells the rows apart.
      renderCell: (a) => (
        <HStack gap={2} hAlign="end" wrap="wrap">
          {canBook && a.canRecordDnaFollowUp && (
            <Button
              label={`Record Follow-up for ${a.patientName ?? 'the patient'}`}
              size="sm"
              variant="primary"
              icon={<Icon icon={AppIcon.DnaFollowUp} />}
              onClick={() => setFollowingUp(a)}>
              Record Follow-up
            </Button>
          )}
          <Button label={`Open the appointment for ${a.patientName ?? 'the patient'}`} size="sm" variant="secondary" onClick={() => setOpened(a)}>
            Open
          </Button>
        </HStack>
      ),
    },
  ];

  const noun = total === 1 ? 'appointment' : 'appointments';
  const subtitle = isPending
    ? 'Loading...'
    : isFiltered
      ? `${total} matching ${noun}`
      : isDidNotAttend
        ? `${total} missed ${noun} to follow up`
        : `${total} ${noun} to book`;

  // Switching lists starts at the first page, keeping the patient search; the Did not attend list has no sort.
  const listSwitch = (
    <SegmentedControl
      label="List"
      style={PRIMARY_TEXT}
      value={isDidNotAttend ? 'didNotAttend' : 'toBook'}
      onChange={(value) => onSearchChange({view: value === 'didNotAttend' ? 'didNotAttend' : undefined, patient: search.patient, clinic: search.clinic})}>
      <SegmentedControlItem value="toBook" label="To book" />
      <SegmentedControlItem value="didNotAttend" label="Did not attend" />
    </SegmentedControl>
  );

  return (
    <Page
      title="Requires Appointment"
      subtitle={subtitle}
      fill
      backHref="/calendar"
      backLabel="Back to the calendar"
      actions={
        <HStack gap={2} vAlign="center" wrap="wrap">
          {clinic.picker}
          {listSwitch}
        </HStack>
      }
      message={isError && <AlertMessage message="The appointments could not be loaded. Please try again." />}
      dialogs={
        <>
          <BookAppointmentDialog
            isOpen={booking !== null}
            appointment={booking ?? undefined}
            suggestedStaffUserId={assignees.length === 1 ? assignees[0].userId : undefined}
            onClose={() => setBooking(null)}
          />
          <AppointmentDialog appointment={opened} onClose={() => setOpened(null)} />
          <DnaFollowUpDialog isOpen={followingUp !== null} appointment={followingUp} onClose={() => setFollowingUp(null)} />
        </>
      }>
      <DataTable
        data={rows}
        columns={isDidNotAttend ? didNotAttendColumns : columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        top={
          isDidNotAttend && (
            <Section variant="muted" padding={3} dividers={['bottom']}>
              <Text type="body">{DID_NOT_ATTEND_INTRO}</Text>
            </Section>
          )
        }
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={search}
            onChange={(values) => onSearchChange({...search, patient: values.patient as string | undefined, page: undefined})}
            // The key saved searches are stored under, from when the list was called Needs Booking: renaming it would lose them.
            list={isDidNotAttend ? 'did-not-attend' : 'needs-booking'}
            label={isDidNotAttend ? 'Search missed appointments' : 'Search appointments to book'}
            placeholder="Search by patient..."
            textKey="patient"
          />
        }
        sort={isDidNotAttend ? null : {column: search.sort ?? 'Due', ascending: search.asc ?? true}}
        onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: sort.column as RequiresAppointmentSortColumn, asc: sort.ascending})}
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
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({view: search.view, sort: search.sort, asc: search.asc, clinic: search.clinic})} />}
            />
          ) : isDidNotAttend ? (
            <EmptyState title="No missed appointments waiting for a follow-up" icon={<Icon icon={AppIcon.Success} size="lg" />} />
          ) : (
            <EmptyState title="Nothing requires an appointment" icon={<Icon icon={AppIcon.Success} size="lg" />} />
          )
        }
      />
    </Page>
  );
}

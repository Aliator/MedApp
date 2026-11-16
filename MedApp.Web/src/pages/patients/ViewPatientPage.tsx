import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {patientAppointmentsQuery, type Appointment} from '../../api/appointments';
import {ApiError} from '../../api/errors';
import {patientQuery, type Patient} from '../../api/patients';
import {NO_TIMELINE_FILTERS, type TimelineFilters} from '../../api/timeline';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AppointmentList} from '../../components/procedures/AppointmentList';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PatientTimeline} from '../../components/procedures/PatientTimeline';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewCard} from '../../components/views/ViewCard';
import {CardSection} from '../../components/views/CardSection';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate, parseUtc} from '../../utils/dates';

/**
 * A patient's details (replaces Blazor's ViewPatient) beside their timeline: everything that happened to their
 * procedures, with what's due. Like the procedure page, the two cards fill the page and each scrolls on its own; a
 * narrow window or a half view has one card of two folding sections instead (see PatientView). An unknown patient
 * goes back to the list, as in Blazor.
 */
export function ViewPatientPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  return <PatientView id={id} isNarrow={isNarrow} />;
}

export interface PatientViewProps {
  id: string;
  /** A narrow window or half view: one card with Details and Timeline sections that fold, instead of two cards. */
  isNarrow: boolean;
}

/** The patient page's content, laid out for the window (see ViewPatientPage). */
export function PatientView({id, isNarrow}: PatientViewProps) {
  const navigate = useNavigate();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  // Held here, so they carry over when the window switches between one card and two.
  const [filters, setFilters] = useState<TimelineFilters>(NO_TIMELINE_FILTERS);
  const [open, setOpen] = useState({details: true, timeline: true});
  const [isBooking, setIsBooking] = useState(false);
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const timeline = <PatientTimeline patientId={id} filters={filters} onFiltersChange={setFilters} />;

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient..."
      fill={patient !== undefined}
      message={
        error &&
        !(error instanceof ApiError && error.status === 404) && <AlertMessage message="The patient could not be loaded. Please try again." />
      }
      dialogs={
        patient && (
          <BookAppointmentDialog
            isOpen={isBooking}
            onClose={() => setIsBooking(false)}
            patient={{id: patient.id ?? '', name: `${patient.firstName} ${patient.lastName}`}}
          />
        )
      }>
      {patient &&
        (isNarrow ? (
          // One card: the details, then the timeline, each folding; they scroll together.
          <FoldingCard
            footerActions={<Actions patient={patient} onBook={() => setIsBooking(true)} />}
            sections={[
              {
                key: 'details',
                title: 'Details',
                isOpen: open.details,
                onToggle: () => toggle('details'),
                children: (
                  <Section variant="transparent" padding={6}>
                    <VStack gap={6}>
                      <PatientDetails patient={patient} />
                    </VStack>
                  </Section>
                ),
              },
              {key: 'timeline', title: 'Timeline', isOpen: open.timeline, onToggle: () => toggle('timeline'), children: timeline},
            ]}
          />
        ) : (
          // The details card beside the timeline's.
          <TwoColumns
            start={
              <ViewCard fill footerActions={<Actions patient={patient} onBook={() => setIsBooking(true)} />}>
                <PatientDetails patient={patient} />
              </ViewCard>
            }
            end={
              <PanelCard title="Timeline" isBodyPadded={false}>
                {timeline}
              </PanelCard>
            }
          />
        ))}
    </Page>
  );
}

/** Book Appointment, Procedures and Edit Patient. */
function Actions({patient, onBook}: {patient: Patient; onBook: () => void}) {
  return (
    <>
      <Button label="Book Appointment" variant="secondary" icon={<Icon icon={AppIcon.Book} />} onClick={onBook} />
      <Button label="Procedures" variant="secondary" icon={<Icon icon={AppIcon.Procedures} />} href={`/patients/${patient.id}/procedures`} />
      <Button label="Edit Patient" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} href={`/patients/${patient.id}/edit`} />
    </>
  );
}

/** How many past appointments the patient page lists, under the upcoming ones. */
export const PAST_APPOINTMENTS_SHOWN = 3;

const startTime = (a: Appointment) => (a.startUtc ? parseUtc(a.startUtc).getTime() : 0);
const lastChange = (a: Appointment) => (a.startUtc ? startTime(a) : parseUtc(a.lastUpdated ?? '').getTime() || 0);

/**
 * The patient's appointments, in two parts: what's still to happen (Booked, soonest first, then Needs booking), then
 * the last few that are over (attended, missed or cancelled), newest first. Each opens on the Calendar.
 */
export function splitAppointments(appointments: readonly Appointment[]) {
  const upcoming = [
    ...appointments.filter((a) => a.status === 'Booked').sort((a, b) => startTime(a) - startTime(b)),
    ...appointments.filter((a) => a.status === 'Requested'),
  ];
  const past = appointments.filter((a) => a.status !== 'Booked' && a.status !== 'Requested').sort((a, b) => lastChange(b) - lastChange(a));
  return {upcoming, past: past.slice(0, PAST_APPOINTMENTS_SHOWN), morePast: Math.max(0, past.length - PAST_APPOINTMENTS_SHOWN)};
}

function PatientAppointments({patientId}: {patientId: string}) {
  const {data, isPending, isError} = useQuery(patientAppointmentsQuery(patientId));
  if (isError) return <AlertMessage message="The appointments could not be loaded. Please try again." />;
  if (isPending)
    return (
      <Text type="body" color="secondary">
        Loading appointments...
      </Text>
    );
  const {upcoming, past, morePast} = splitAppointments(data.items ?? []);
  if (upcoming.length === 0 && past.length === 0)
    return (
      <Text type="body" color="secondary">
        No appointments yet.
      </Text>
    );
  return (
    <VStack gap={4}>
      {upcoming.length > 0 ? (
        <AppointmentList appointments={upcoming} showProcedure label="Upcoming appointments" />
      ) : (
        <Text type="body" color="secondary">
          Nothing booked or waiting to be booked.
        </Text>
      )}
      {past.length > 0 && (
        <VStack gap={2}>
          {/* Cancelled ones may have been for a later day, so not just "Earlier". */}
          <Heading level={3}>Past and cancelled</Heading>
          <AppointmentList appointments={past} showProcedure label="Past and cancelled appointments" />
          {morePast > 0 && (
            <Text type="supporting" color="secondary">
              {`And ${morePast} more: the timeline has them all.`}
            </Text>
          )}
        </VStack>
      )}
    </VStack>
  );
}

/** Who the patient is, how to contact them, their appointments, and their record's ID. */
function PatientDetails({patient}: {patient: Patient}) {
  const age = patient.dateOfBirth ? ageOn(patient.dateOfBirth) : null;
  return (
    <>
      <ViewSection title="Personal Information">
        <MetadataListItem label="First Name">{patient.firstName}</MetadataListItem>
        <MetadataListItem label="Last Name">{patient.lastName}</MetadataListItem>
        <MetadataListItem label="Date of Birth">{formatDate(patient.dateOfBirth ?? '')}</MetadataListItem>
        <MetadataListItem label="Age">{age !== null ? `${age} ${age === 1 ? 'year' : 'years'}` : '—'}</MetadataListItem>
      </ViewSection>
      <ViewSection title="Contact Information" useGrid={false}>
        <MetadataListItem label="Email Address">
          <Link href={`mailto:${patient.email}`}>{patient.email}</Link>
        </MetadataListItem>
      </ViewSection>
      <CardSection title="Appointments">
        <PatientAppointments patientId={patient.id ?? ''} />
      </CardSection>
      <ViewSection title="Record Information" useGrid={false}>
        <MetadataListItem label="Patient ID">
          <Text type="code">{patient.id}</Text>
        </MetadataListItem>
      </ViewSection>
    </>
  );
}

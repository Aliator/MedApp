import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
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
import {patientQuery, useSetPatientFlag, whyCantChange, type Patient} from '../../api/patients';
import {NO_TIMELINE_FILTERS, type TimelineFilters} from '../../api/timeline';
import {can, useCurrentUser} from '../../auth/auth';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {Attachments} from '../../components/attachments/Attachments';
import {useAttachmentsState} from '../../components/attachments/useAttachmentsState';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AppointmentList} from '../../components/pathways/AppointmentList';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AnonymiseDialog} from '../../components/patients/AnonymiseDialog';
import {PatientAccessReport} from '../../components/patients/PatientAccessReport';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {PatientFlagDialog, type PatientFlagChange} from '../../components/patients/PatientFlagDialog';
import {PatientTimeline} from '../../components/pathways/PatientTimeline';
import {PatientReferrals} from '../../components/referrals/PatientReferrals';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {FoldingCard} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate, parseUtc} from '../../utils/dates';
import {reminderChannelLabel} from '../../api/patientMessages';
import {formatMobile} from '../../utils/contactDetails';
import {formatNhsNumber} from '../../utils/nhsNumber';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/**
 * A patient's details (replaces Blazor's ViewPatient), referrals and files, beside their timeline: everything that happened to
 * their pathways, with what's due. Like the pathway page, the two cards fill the page and each scrolls on its
 * own; a narrow window or a half view has one card of three folding sections instead (see PatientView). An unknown
 * patient goes back to the list, as in Blazor.
 *
 * Banners say when the record is restricted, disputed or anonymised (when its files can't be added or deleted
 * either). Admins also get its privacy actions (export, restrict, mark disputed, anonymise) and, under the details
 * and files, its access report: who has accessed the record, and when.
 */
export function ViewPatientPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  return <PatientView id={id} isNarrow={isNarrow} />;
}

export interface PatientViewProps {
  id: string;
  /** A narrow window or half view: one card with Details, Files and Timeline sections that fold, instead of two cards. */
  isNarrow: boolean;
}

/** The patient page's content, laid out for the window (see ViewPatientPage). */
export function PatientView({id, isNarrow}: PatientViewProps) {
  const navigate = useNavigate();
  const me = useCurrentUser();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  // Held here, so they carry over when the window switches between one card and two.
  const [filters, setFilters] = useState<TimelineFilters>(NO_TIMELINE_FILTERS);
  const [open, setOpen] = useState({details: true, referrals: true, files: true, timeline: true});
  // Files on their way, held here so they carry on when the window switches between one card and two.
  const attachments = useAttachmentsState();
  const [isBooking, setIsBooking] = useState(false);
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const timeline = <PatientTimeline patientId={id} filters={filters} onFiltersChange={setFilters} />;
  const actions = patient && <Actions patient={patient} onBook={() => setIsBooking(true)} onProblem={setProblem} />;
  // Everything attached to the patient, anywhere; what's added here is attached to the patient themselves. Nothing
  // can be added or deleted while the record is restricted or anonymised.
  const isLocked = patient !== undefined && whyCantChange(patient) !== null;
  const files = <Attachments target={{patientId: id}} canChange={!isLocked} isLocked={isLocked} state={attachments} />;
  const details = patient && <PatientDetails patient={patient} />;
  // Their referrals, open ones first, and Register Referral (for holders of RegisterReferrals, not while the record can't change).
  const referrals = <PatientReferrals patientId={id} canRegister={!isLocked && can(me, 'RegisterReferrals')} />;
  // For holders of ViewAudit, who has accessed the record: under the details (and, in the wide layout, the files).
  const accessReport = can(me, 'ViewAudit') && <PatientAccessReport patientId={id} />;

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient..."
      fill={patient !== undefined}
      banner={patient && <PatientBanner patient={patient} />}
      message={
        error && !(error instanceof ApiError && error.status === 404) ? (
          <AlertMessage message="The patient could not be loaded. Please try again." />
        ) : (
          patient && (
            <VStack gap={2} hAlign="stretch">
              {problem.message && <AlertMessage message={problem.message} />}
              <PatientFlagBanners patient={patient} />
            </VStack>
          )
        )
      }
      dialogs={patient && <BookAppointmentDialog isOpen={isBooking} onClose={() => setIsBooking(false)} patient={{...patient, id}} />}>
      {patient &&
        (isNarrow ? (
          // One card: the details, the files, then the timeline, each folding; they scroll together.
          <FoldingCard
            footerActions={actions}
            sections={[
              {
                key: 'details',
                title: 'Details',
                isOpen: open.details,
                onToggle: () => toggle('details'),
                children: (
                  <Section variant="transparent" padding={6}>
                    <VStack gap={6}>
                      {details}
                      {accessReport}
                    </VStack>
                  </Section>
                ),
              },
              {
                key: 'referrals',
                title: 'Referrals',
                isOpen: open.referrals,
                onToggle: () => toggle('referrals'),
                children: (
                  <Section variant="transparent" padding={6}>
                    {referrals}
                  </Section>
                ),
              },
              {
                key: 'files',
                title: 'Files',
                isOpen: open.files,
                onToggle: () => toggle('files'),
                children: (
                  <Section variant="transparent" padding={6}>
                    {files}
                  </Section>
                ),
              },
              {key: 'timeline', title: 'Timeline', isOpen: open.timeline, onToggle: () => toggle('timeline'), children: timeline},
            ]}
          />
        ) : (
          // The details card, with the files under them, beside the timeline's.
          <TwoColumns
            start={
              <ViewCard fill footerActions={actions}>
                {details}
                <CardSection title="Referrals">{referrals}</CardSection>
                <CardSection title="Files">{files}</CardSection>
                {accessReport}
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

/**
 * Book Appointment (BookAppointments), Pathways and Edit Patient (EditPatients), not booking or editing while the
 * record is restricted or anonymised, when it can't be changed; and the Privacy menu, for holders of ManagePrivacy or
 * ExportPatientRecords.
 */
function Actions({patient, onBook, onProblem}: {patient: Patient; onBook: () => void; onProblem: (problem: SaveProblem) => void}) {
  const me = useCurrentUser();
  const canChange = whyCantChange(patient) === null;
  return (
    <>
      {(can(me, 'ManagePrivacy') || can(me, 'ExportPatientRecords')) && <PrivacyActions patient={patient} onProblem={onProblem} />}
      {canChange && can(me, 'BookAppointments') && (
        <Button label="Book Appointment" variant="secondary" icon={<Icon icon={AppIcon.Book} />} onClick={onBook} />
      )}
      <Button label="Pathways" variant="secondary" icon={<Icon icon={AppIcon.Pathways} />} href={`/patients/${patient.id}/pathways`} />
      {canChange && can(me, 'EditPatients') && (
        <Button label="Edit Patient" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} href={`/patients/${patient.id}/edit`} />
      )}
    </>
  );
}

/** How many past appointments the patient page lists, under the upcoming ones. */
export const PAST_APPOINTMENTS_SHOWN = 3;

const startTime = (a: Appointment) => (a.startUtc ? parseUtc(a.startUtc).getTime() : 0);
const lastChange = (a: Appointment) => (a.startUtc ? startTime(a) : parseUtc(a.lastUpdated ?? '').getTime() || 0);

/**
 * The patient's appointments, in two parts: what's still to happen (Booked, soonest first, then Requires appointment), then
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
        <AppointmentList appointments={upcoming} showPathway label="Upcoming appointments" />
      ) : (
        <Text type="body" color="secondary">
          Nothing booked or waiting to be booked.
        </Text>
      )}
      {past.length > 0 && (
        <VStack gap={2}>
          {/* Cancelled ones may have been for a later day, so not just "Earlier". */}
          <Heading level={3}>Past and cancelled</Heading>
          <AppointmentList appointments={past} showPathway label="Past and cancelled appointments" />
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

/** An address a line at a time, its postcode last; "Not given" when there's none. */
function AddressLines({lines, postcode}: {lines?: readonly string[] | null; postcode?: string | null}) {
  const parts = [...(lines ?? []), postcode ?? ''].filter((part) => part.trim());
  if (parts.length === 0) return 'Not given';
  return (
    <VStack gap={0}>
      {parts.map((part, index) => (
        <Text key={index} type="body">
          {part}
        </Text>
      ))}
    </VStack>
  );
}

/** Who the patient is, how to contact them, their GP practice, their appointments, and their record's ID. */
function PatientDetails({patient}: {patient: Patient}) {
  const age = patient.dateOfBirth ? ageOn(patient.dateOfBirth) : null;
  return (
    <>
      <ViewSection title="Personal Information">
        <MetadataListItem label="First Name">{patient.firstName}</MetadataListItem>
        <MetadataListItem label="Last Name">{patient.lastName}</MetadataListItem>
        <MetadataListItem label="Date of Birth">{formatDate(patient.dateOfBirth ?? '')}</MetadataListItem>
        <MetadataListItem label="Age">{age !== null ? `${age} ${age === 1 ? 'year' : 'years'}` : '—'}</MetadataListItem>
        <MetadataListItem label="NHS Number">{patient.nhsNumber ? formatNhsNumber(patient.nhsNumber) : 'Not given'}</MetadataListItem>
      </ViewSection>
      <ViewSection title="Contact Information" useGrid={false}>
        <MetadataListItem label="Email Address">
          <Link href={`mailto:${patient.email}`}>{patient.email}</Link>
        </MetadataListItem>
        <MetadataListItem label="Mobile">{patient.mobilePhone ? formatMobile(patient.mobilePhone) : 'Not given'}</MetadataListItem>
        <MetadataListItem label="Address">
          <AddressLines lines={patient.addressLines} postcode={patient.postcode} />
        </MetadataListItem>
        <MetadataListItem label="Reminders">{reminderChannelLabel(patient.reminderChannel)}</MetadataListItem>
      </ViewSection>
      <ViewSection title="GP Practice" useGrid={false}>
        {patient.practice ? (
          <>
            <MetadataListItem label="Practice">
              {patient.practice.isArchived ? `${patient.practice.name ?? ''} (archived)` : patient.practice.name}
            </MetadataListItem>
            <MetadataListItem label="ODS Code">{patient.practice.odsCode || 'Not given'}</MetadataListItem>
            <MetadataListItem label="Address">
              <AddressLines lines={patient.practice.addressLines} postcode={patient.practice.postcode} />
            </MetadataListItem>
            {patient.practice.phone && <MetadataListItem label="Phone">{patient.practice.phone}</MetadataListItem>}
          </>
        ) : (
          <MetadataListItem label="Practice">Not given</MetadataListItem>
        )}
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

/**
 * The privacy actions for the record (each for the permission it needs), behind one Privacy button: export everything held (a subject access
 * request), restrict or lift the restriction, mark or clear a dispute, and anonymise. Each asks to confirm first.
 */
function PrivacyActions({patient, onProblem}: {patient: Patient; onProblem: (problem: SaveProblem) => void}) {
  const id = patient.id ?? '';
  // Exporting (ExportPatientRecords), and the rest (ManagePrivacy): each person sees the ones their roles allow.
  const me = useCurrentUser();
  const canExport = can(me, 'ExportPatientRecords');
  const canManagePrivacy = can(me, 'ManagePrivacy');
  const setFlag = useSetPatientFlag(id);
  const [change, setChange] = useState<PatientFlagChange | null>(null);
  // Why the flag change failed: shown in its dialog, which stays open with the reason as typed.
  const [flagProblem, setFlagProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [isAnonymising, setIsAnonymising] = useState(false);
  const [dialogKey, setDialogKey] = useState(0);

  const open = (next: PatientFlagChange) => {
    onProblem(NO_PROBLEM);
    setFlagProblem(NO_PROBLEM);
    setDialogKey((k) => k + 1);
    setChange(next);
  };

  const confirmFlag = (reason: string, confirmOpenPathways: boolean) => {
    if (!change) return;
    setFlagProblem(NO_PROBLEM);
    setFlag.mutate(
      {...change, reason, confirmOpenPathways},
      {
        onSuccess: () => setChange(null),
        onError: (e) => setFlagProblem(saveProblem(e, 'save')),
      },
    );
  };

  const exportItem: DropdownMenuItemData = {
    id: 'export',
    label: 'Export Record',
    description: 'Everything held, for a subject access request',
    icon: AppIcon.View,
    href: `/patients/${id}/export`,
  };
  const privacyItems: DropdownMenuItemData[] = [
    patient.isRestricted
      ? {id: 'restrict', label: 'Lift Restriction', icon: AppIcon.Lock, onClick: () => open({flag: 'restriction', set: false})}
      : {
          id: 'restrict',
          label: 'Restrict Record',
          description: 'Nothing can be changed until it’s lifted',
          icon: AppIcon.Lock,
          isDisabled: patient.isAnonymised === true,
          onClick: () => open({flag: 'restriction', set: true}),
        },
    patient.isDisputed
      ? {id: 'dispute', label: 'Clear Dispute', icon: AppIcon.Info, onClick: () => open({flag: 'dispute', set: false})}
      : {
          id: 'dispute',
          label: 'Mark as Disputed',
          description: 'The patient disputes something in the record',
          icon: AppIcon.Info,
          isDisabled: patient.isAnonymised === true,
          onClick: () => open({flag: 'dispute', set: true}),
        },
    {
      id: 'anonymise',
      label: 'Anonymise Record',
      description: patient.isAnonymised ? 'Already anonymised' : patient.isRestricted ? 'Lift the restriction first' : 'Can’t be undone',
      icon: AppIcon.Delete,
      variant: 'destructive',
      isDisabled: patient.isAnonymised === true || patient.isRestricted === true,
      onClick: () => {
        onProblem(NO_PROBLEM);
        setDialogKey((k) => k + 1);
        setIsAnonymising(true);
      },
    },
  ];
  const items = [...(canExport ? [exportItem] : []), ...(canManagePrivacy ? privacyItems : [])];

  return (
    <>
      <DropdownMenu items={items} alignment="end" button={{label: 'Privacy', variant: 'secondary', icon: <Icon icon={AppIcon.Shield} />}} />
      <PatientFlagDialog
        key={`flag-${dialogKey}`}
        patientId={id}
        change={change}
        errors={flagProblem.validation}
        message={flagProblem.message ?? (flagProblem.validation?.general.length ? flagProblem.validation.general.join(' ') : null)}
        isProcessing={setFlag.isPending}
        onConfirm={confirmFlag}
        onCancel={() => setChange(null)}
      />
      <AnonymiseDialog
        key={`anonymise-${dialogKey}`}
        patient={isAnonymising ? {id, firstName: patient.firstName, lastName: patient.lastName, dateOfBirth: patient.dateOfBirth} : null}
        onDone={() => setIsAnonymising(false)}
        onCancel={() => setIsAnonymising(false)}
      />
    </>
  );
}

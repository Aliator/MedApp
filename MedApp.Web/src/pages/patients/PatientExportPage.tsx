import {mdtStatusWords} from '../../api/mdt';
import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useEffect, type ReactNode} from 'react';
import {activityActionLabel, describeActivity} from '../../api/activity';
import {appointmentStatusLabel, dnaFollowUpLabel} from '../../api/appointments';
import {auditActionLabel, describeAuditDetails} from '../../api/audit';
import {ApiError} from '../../api/errors';
import {describeNoteDeletionReason} from '../../api/noteDeletion';
import {messageKindLabel, messageOutcome, reminderChannelLabel, type ExportedPatientMessage} from '../../api/patientMessages';
import {patientExportQuery, type PatientExport} from '../../api/patients';
import {pauseReasonWords, referralFieldWords, referralStatusLabel, rejectionReasonWords, stopReasonWords} from '../../api/referrals';
import {describeSkipReason} from '../../api/stageSkips';
import {rejectionReasonLabel, specimenStatusLabel} from '../../api/specimens';
import {Page} from '../../components/pages/Page';
import {statusLabel} from '../../components/pathways/PathwayCells';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {RichText} from '../../components/shared/RichText';
import {PanelHeader} from '../../components/views/PanelCard';
import {sameMoment} from '../../utils/calendarSlots';
import {formatAppointmentTime, formatDate, formatDateTime} from '../../utils/dates';
import {formatFileSize} from '../../utils/fileSize';
import {formatMobile, oneLineAddress} from '../../utils/contactDetails';
import {formatNhsNumber} from '../../utils/nhsNumber';

/** While printing, only the export shows: the side nav and the page's buttons are left off the paper. */
const PRINT_CSS = '@media print { .astryx-side-nav, button { display: none !important; } }';

/** The export's file name: the patient's ID and the day, never their name. */
export const exportFileName = (data: PatientExport) =>
  `patient-${data.patient?.id ?? 'record'}-${(data.exportedAtUtc ?? '').slice(0, 10) || 'export'}.json`;

type ExportedNote = NonNullable<NonNullable<PatientExport['pathways']>[number]['notes']>[number];
type ExportedDeletionRequest = NonNullable<PatientExport['noteDeletionRequests']>[number];

/** Said beside a note deleted as written on the wrong patient: its text is most likely someone else's. */
export const ANOTHER_PATIENTS_DETAILS = "Another patient's details: check before release";

/** A deleted note's line: "Deleted by admin, 17 Nov 2026, 10:32. Reason: Wrong patient: belongs to the other pathway." */
export function deletedNoteText(note: Pick<ExportedNote, 'deletedAtUtc' | 'deletedBy' | 'deletionReason' | 'deletionDetail'>): string {
  const reason = describeNoteDeletionReason(note.deletionReason, note.deletionDetail);
  return `Deleted by ${note.deletedBy ?? 'an admin'}, ${formatDateTime(note.deletedAtUtc ?? '')}.${reason ? ` Reason: ${reason}` : ''}`;
}

type ExportedStage = NonNullable<NonNullable<PatientExport['pathways']>[number]['stages']>[number];

/**
 * A skipped stage's line: "Skipped 3 Sep 2026, 09:00 by l.northcott. Reason: Patient declined", with the detail
 * after the reason ("Other: seen at another hospital"), and "Another branch was chosen" for a branch not taken.
 */
export function skippedStageText(stage: Pick<ExportedStage, 'skippedAtUtc' | 'skippedBy' | 'skipReason' | 'skipDetail'>): string {
  const reason = describeSkipReason(stage.skipReason, stage.skipDetail);
  return `Skipped ${formatDateTime(stage.skippedAtUtc ?? '')} by ${stage.skippedBy ?? 'unknown'}.${reason ? ` Reason: ${reason}` : ''}`;
}

/** Where a request to delete a note stands: waiting, or what an admin decided, by whom and when. */
export function deletionRequestOutcome(request: Pick<ExportedDeletionRequest, 'status' | 'decidedBy' | 'decidedAtUtc'>): string {
  const by = request.decidedBy ?? 'an admin';
  const at = request.decidedAtUtc ? `, ${formatDateTime(request.decidedAtUtc)}` : '';
  switch (request.status) {
    case 'Approved':
      return `Note deleted by ${by}${at}.`;
    case 'Declined':
      return `Note kept by ${by}${at}.`;
    case 'Closed':
      return `Closed without a decision${at}.`;
    default:
      return 'Waiting for an admin to decide.';
  }
}

/** Saves the export as a JSON file, through a link to it in memory (nothing is sent anywhere). */
export function downloadExport(data: PatientExport) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], {type: 'application/json'}));
  const link = document.createElement('a');
  link.href = url;
  link.download = exportFileName(data);
  link.click();
  URL.revokeObjectURL(url);
}

const when = (iso: string | null | undefined) => (iso ? formatDateTime(iso) : '—');

type ExportedAppointment = NonNullable<PatientExport['appointments']>[number];
type ExportedAppointmentChange = NonNullable<PatientExport['appointmentHistory']>[number];

/** An appointment's time and member of staff: "Mon 23 Nov 2026, 10:00 to 10:30 with j.smith"; empty with no time. */
function appointmentSlot(startUtc: string | null | undefined, endUtc: string | null | undefined, staff: string | null | undefined): string {
  if (!startUtc) return staff ? `With ${staff}` : '';
  return `${formatAppointmentTime(startUtc, endUtc)}${staff ? ` with ${staff}` : ''}`;
}

/** An appointment in a line: its status and, once booked, when and with whom ("Booked: Mon 23 Nov 2026, …"). */
export function describeExportedAppointment(appointment: ExportedAppointment): string {
  const slot = appointmentSlot(appointment.startUtc, appointment.endUtc, appointment.staffUsername);
  const status = appointmentStatusLabel(appointment.status);
  return slot ? `${status}: ${slot}` : status;
}

/**
 * Who asked for, booked and recorded an appointment, in words: "requested … by …, booked … by …, outcome recorded …
 * by …". One recorded after the event (a walk-in) was "recorded after the event … by …", not booked, and was asked for
 * only if it was a visit waiting in Requires Appointment (made before it was recorded), as `AppointmentDialog` says.
 */
export function appointmentHistoryWords(a: ExportedAppointment): string {
  const isWalkIn = a.isRecordedAfterEvent === true;
  const wasRequested = !isWalkIn || !sameMoment(a.createdAtUtc, a.bookedAtUtc);
  return [
    wasRequested ? `requested ${when(a.createdAtUtc)} ${a.isAutomatic ? 'automatically' : `by ${a.createdBy}`}` : null,
    a.bookedAtUtc ? `${isWalkIn ? 'recorded after the event' : 'booked'} ${when(a.bookedAtUtc)} by ${a.bookedBy}` : null,
    a.outcomeAtUtc ? `outcome recorded ${when(a.outcomeAtUtc)} by ${a.outcomeBy}` : null,
  ]
    .filter(Boolean)
    .join(', ');
}

/** A missed appointment's follow-up, in words: "Follow-up: Rebook, recorded 24 Nov 2026, 10:00 by k.ashford". */
export function dnaFollowUpExportWords(a: Pick<ExportedAppointment, 'dnaFollowUp' | 'dnaFollowUpAtUtc' | 'dnaFollowUpBy'>): string | null {
  if (!a.dnaFollowUp) return null;
  return `Follow-up: ${dnaFollowUpLabel(a.dnaFollowUp)}, recorded ${when(a.dnaFollowUpAtUtc)}${a.dnaFollowUpBy ? ` by ${a.dnaFollowUpBy}` : ''}`;
}

/** A message to the patient in a line: "Reminder by Text to 07700 900123". */
export function exportedMessageTitle(m: Pick<ExportedPatientMessage, 'kind' | 'channel' | 'destination'>): string {
  const to = m.destination ? ` to ${m.channel === 'Text' ? formatMobile(m.destination) : m.destination}` : '';
  return `${messageKindLabel(m.kind)} by ${m.channel ?? ''}${to}`;
}

/** What became of a message: "Delivered, sent 21 Nov 2026, 09:00, status from 21 Nov 2026, 09:01". */
export function exportedMessageOutcome(m: ExportedPatientMessage): string {
  return [
    messageOutcome(m),
    m.sentAtUtc ? `sent ${when(m.sentAtUtc)}` : null,
    m.statusAtUtc && !sameMoment(m.statusAtUtc, m.sentAtUtc) ? `status from ${when(m.statusAtUtc)}` : null,
  ]
    .filter(Boolean)
    .join(', ');
}

const APPOINTMENT_ACTIONS: Record<string, string> = {
  Requested: 'Appointment requested',
  Booked: 'Appointment booked',
  Rescheduled: 'Appointment rescheduled',
  Attended: 'Marked Attended',
  DidNotAttend: 'Marked Did not attend',
  Cancelled: 'Appointment cancelled',
  NotesUpdated: 'Appointment notes changed',
  RecordedAfterEvent: 'Recorded after the event',
  EnteredInError: 'Marked entered in error',
  DnaFollowUpRecorded: 'Did not attend follow-up recorded',
};

/** An appointment history action in words: "DidNotAttend" → "Marked Did not attend". */
export const appointmentChangeLabel = (action: string | null | undefined) => APPOINTMENT_ACTIONS[action ?? ''] ?? action ?? '';

function parseDetails(details: string | null | undefined): Record<string, unknown> {
  try {
    const value: unknown = JSON.parse(details ?? '{}');
    return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

const text = (value: unknown) => (typeof value === 'string' ? value.trim() : '');
const quoted = (value: unknown) => (text(value) ? `"${text(value)}"` : 'nothing');

/**
 * What an appointment history entry's details say, in words: the time it moved from, the outcome it corrected, why it
 * was cancelled or marked entered in error, the outcome recorded after the event (and that it was waiting in Requires
 * Appointment), or the notes before and after (the export gives everything, notes and reasons included).
 */
export function describeAppointmentChange(entry: Pick<ExportedAppointmentChange, 'action' | 'details'>): string {
  const d = parseDetails(entry.details);
  switch (entry.action) {
    case 'Requested':
      return d.automatic === true ? 'Requested automatically, as the stage involves a visit' : '';
    case 'Rescheduled': {
      const before = appointmentSlot(text(d.previousStartUtc) || null, text(d.previousEndUtc) || null, text(d.previousStaffUsername) || null);
      return before ? `From ${before}` : '';
    }
    case 'Attended':
    case 'DidNotAttend':
      return d.previousStatus === 'Attended' || d.previousStatus === 'DidNotAttend' ? `Was marked ${appointmentStatusLabel(d.previousStatus)}` : '';
    case 'Cancelled':
      return [d.automatic === true ? 'Cancelled automatically' : null, text(d.reason) || null].filter(Boolean).join(': ');
    case 'RecordedAfterEvent':
      return [text(d.outcome) ? appointmentStatusLabel(text(d.outcome)) : null, d.previousStatus === 'Requested' ? 'was in Requires Appointment' : null]
        .filter(Boolean)
        .join(', ');
    case 'EnteredInError':
      return text(d.reason);
    case 'DnaFollowUpRecorded':
      return [text(d.decision) ? dnaFollowUpLabel(text(d.decision)) : null, text(d.detail) || null].filter(Boolean).join(': ');
    case 'NotesUpdated': {
      const change = `${quoted(d.from)} → ${quoted(d.to)}`;
      return change.charAt(0).toUpperCase() + change.slice(1);
    }
    default:
      return '';
  }
}

type ExportedReferral = NonNullable<PatientExport['referrals']>[number];

/** A referral in words: "the referral to Dermatology received 3 November 2026". */
export const referralName = (referral: Pick<ExportedReferral, 'specialtyName' | 'receivedOn'>) =>
  `the referral to ${referral.specialtyName}${referral.receivedOn ? ` received ${formatDate(referral.receivedOn)}` : ''}`;

const capitalised = (words: string) => words.charAt(0).toUpperCase() + words.slice(1);

const REFERRAL_ACTIONS: Record<string, string> = {
  Registered: 'Referral registered',
  DetailsEdited: 'Referral details edited',
  Withdrawn: 'Referral withdrawn',
  EnteredInError: 'Referral marked entered in error',
  ClockPaused: 'Waiting-time clock paused',
  ClockResumed: 'Waiting-time clock resumed',
  ClockStopped: 'Waiting-time clock stopped',
  TargetChanged: 'Waiting-time target changed',
};

/** A referral history action in words: "EnteredInError" → "Referral marked entered in error". */
export const referralChangeLabel = (action: string | null | undefined) => REFERRAL_ACTIONS[action ?? ''] ?? `Referral: ${action ?? ''}`;

const count = (value: unknown) => (typeof value === 'number' ? value : null);
const days = (value: number) => `${value} ${value === 1 ? 'day' : 'days'}`;

/**
 * What a referral history entry's details say, in words: the specialty and priority it was registered with, the
 * fields an edit changed (with the specialty it moved from), the status it moved to, or what the waiting-time clock
 * did (a pause's reason and review date, the days a pause lasted, why it stopped and how long it had waited, a target
 * changed). The details hold structured values only: the reason and the details given are on the referral itself, above.
 */
export function describeReferralChange(details: string | null | undefined): string {
  const d = parseDetails(details);
  const fields = Array.isArray(d.changedFields) ? d.changedFields.filter((f): f is string => typeof f === 'string') : [];
  const fromDays = count(d.fromDays);
  const toDays = count(d.toDays);
  const pausedDays = count(d.pausedDays);
  const daysWaited = count(d.daysWaited);
  const targetDays = count(d.targetDays);
  return [
    text(d.specialtyName) ? `Specialty: ${text(d.specialtyName)}` : null,
    text(d.previousSpecialtyName) ? `was ${text(d.previousSpecialtyName)}` : null,
    text(d.requestedPriority) ? `Priority asked for: ${text(d.requestedPriority)}` : null,
    fields.length ? `Changed: ${fields.map(referralFieldWords).join(', ')}` : null,
    text(d.toStatus) ? `Now ${referralStatusLabel(text(d.toStatus)).toLowerCase()}` : null,
    text(d.pauseReason) ? `Reason: ${pauseReasonWords(text(d.pauseReason))}` : null,
    text(d.reviewOn) ? `review ${formatDate(text(d.reviewOn))}` : null,
    pausedDays != null ? `paused ${days(pausedDays)}` : null,
    d.automatic === true ? 'resumed automatically, as the clock stopped' : null,
    text(d.stopReason) ? `Why: ${stopReasonWords(text(d.stopReason))}` : null,
    daysWaited != null ? `waited ${days(daysWaited)}${targetDays != null ? ` of ${targetDays}` : ''}` : null,
    d.breached === true ? 'breached' : null,
    toDays != null ? `Target ${fromDays != null ? `${days(fromDays)} → ` : ''}${days(toDays)}` : null,
  ]
    .filter(Boolean)
    .join(', ');
}

/**
 * A referral's waiting-time clock: its target, when the wait counted from, its target date, the days paused, and
 * whether and why it stopped; then each pause, with its reason, detail, review date, and who paused and resumed it.
 */
function ReferralClock({referral}: {referral: ExportedReferral}) {
  const pauses = referral.pauses ?? [];
  return (
    <VStack gap={3} hAlign="stretch">
      <Heading level={4}>Waiting-time clock</Heading>
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Target">{days(referral.targetDays ?? 0)}</MetadataListItem>
        <MetadataListItem label="Wait Counted From">{referral.waitStartOn ? formatDate(referral.waitStartOn) : '—'}</MetadataListItem>
        <MetadataListItem label="Target Date">{referral.targetOn ? formatDate(referral.targetOn) : '—'}</MetadataListItem>
        <MetadataListItem label="Days Paused">{days(referral.pausedDays ?? 0)}</MetadataListItem>
        <MetadataListItem label="Stopped">
          {referral.stoppedOn ? `${formatDate(referral.stoppedOn)}: ${stopReasonWords(referral.stopReason) || 'no reason recorded'}` : 'Not stopped'}
        </MetadataListItem>
      </MetadataList>
      {pauses.length === 0 ? (
        <Text type="body" color="secondary">
          Never paused.
        </Text>
      ) : (
        <List hasDividers edge="bleed" aria-label={`Pauses of ${referralName(referral)}`}>
          {pauses.map((pause, index) => (
            <ListItem
              key={`${pause.pausedOn}-${index}`}
              align="first-line"
              label={
                <Text type="body" weight="semibold">
                  {`Paused ${pause.pausedOn ? formatDate(pause.pausedOn) : ''} by ${pause.pausedBy}: ${pauseReasonWords(pause.reason)}`}
                </Text>
              }
              description={
                <VStack gap={1}>
                  {pause.detail && <Text type="supporting">{pause.detail}</Text>}
                  <Text type="supporting" color="secondary">
                    {[
                      pause.reviewOn ? `Review ${formatDate(pause.reviewOn)}` : null,
                      pause.resumedOn ? `resumed ${formatDate(pause.resumedOn)}${pause.resumedBy ? ` by ${pause.resumedBy}` : ''}` : 'not resumed',
                    ]
                      .filter(Boolean)
                      .join('; ')}
                  </Text>
                </VStack>
              }
            />
          ))}
        </List>
      )}
    </VStack>
  );
}

/** One titled part of the export, as a card. */
function ExportSection({title, children}: {title: string; children: ReactNode}) {
  return (
    <Card padding={0} width="100%">
      <VStack gap={0}>
        <PanelHeader title={title} />
        <Section variant="transparent" padding={6}>
          {children}
        </Section>
      </VStack>
    </Card>
  );
}

const nothing = (text: string) => (
  <Text type="body" color="secondary">
    {text}
  </Text>
);

/**
 * A subject access export (admin): everything MedApp holds about a patient (their details, pathways with stages,
 * notes and assignments, their appointments, the files attached to them (their details, deleted ones too, but never
 * their contents), the history of changes to them, who has accessed the record, and notifications about them),
 * readable on screen, printable, and downloadable as JSON. The page shows every field the JSON holds, so what's checked
 * on screen is what's given; staff appear by username only (no account IDs or network addresses). Opening it is recorded in the activity log, once: it's
 * fetched once per visit, and the download and print use that copy. Before it's given to the patient, a clinician
 * should check it for anything that meets the serious harm test, and for other people's details.
 */
export function PatientExportPage({id}: {id: string}) {
  const {data, error, isPending} = useQuery(patientExportQuery(id));
  const queryClient = useQueryClient();
  // The export holds the whole clinical record: it's dropped from the cache as soon as the page closes. Only once
  // nothing shows it, a moment later: in development StrictMode runs this clean-up and the page's effects again at
  // once, and dropping it then would fetch it (and record the export in the activity log) a second time.
  useEffect(
    () => () => {
      const {queryKey} = patientExportQuery(id);
      queueMicrotask(() => queryClient.removeQueries({queryKey, predicate: (query) => query.getObserversCount() === 0}));
    },
    [queryClient, id],
  );
  const isMissing = error instanceof ApiError && error.status === 404;

  return (
    <Page
      title="Patient Record Export"
      subtitle={data ? `Exported ${when(data.exportedAtUtc)} by ${data.exportedBy}` : undefined}
      backHref={`/patients/${id}`}
      backLabel="Back to patient"
      banner={
        data?.patient && (
          // The export says when a flag was set rather than whether it is, as its details below do.
          <PatientBanner
            patient={{
              ...data.patient,
              isRestricted: Boolean(data.patient.restrictedAtUtc),
              isDisputed: Boolean(data.patient.disputedAtUtc),
              isAnonymised: Boolean(data.patient.anonymisedAtUtc),
            }}
          />
        )
      }
      isLoading={isPending}
      loadingMessage="Gathering everything held about this patient..."
      message={
        error ? (
          <AlertMessage message={isMissing ? 'Patient not found. They may have been deleted.' : 'The export could not be made. Please try again.'} />
        ) : (
          data && (
            <AlertMessage
              status="info"
              message="Before giving this to the patient, have a clinician check it for anything that could cause serious harm, and for other people's details."
            />
          )
        )
      }
      actions={
        // A fragment, so the header wraps the buttons among themselves when they don't fit side by side.
        data && (
          <>
            <Button label="Print" variant="secondary" icon={<Icon icon={AppIcon.View} />} onClick={() => window.print()} />
            <Button label="Download JSON" variant="primary" icon={<Icon icon={AppIcon.Save} />} onClick={() => downloadExport(data)} />
          </>
        )
      }>
      <style data-medapp-print="">{PRINT_CSS}</style>
      {data && <ExportContent data={data} />}
    </Page>
  );
}

type ExportedFile = NonNullable<PatientExport['attachments']>[number];

const SCAN_STATES: Record<string, string> = {NotScanned: 'not scanned', Clean: 'clean', Infected: 'malware found'};

/**
 * What an exported file was attached to, in words: "Attached to the patient", "Attached to the referral to
 * Dermatology received 3 November 2026", "Attached to Consent, in Knee review".
 */
function attachedToText(file: ExportedFile, pathwayTitles: Map<string | undefined, string>, referralNames: Map<string | undefined, string>): string {
  if (file.attachedTo === 'Referral') return `Attached to ${referralNames.get(file.referralId ?? undefined) ?? 'a referral'}.`;
  if (file.attachedTo === 'Patient' || !file.pathwayId) return 'Attached to the patient.';
  const title = pathwayTitles.get(file.pathwayId) || 'a pathway';
  return file.attachedTo === 'Stage' ? `Attached to ${file.stageName ?? 'a stage'}, in ${title}.` : `Attached to ${title}.`;
}

function ExportContent({data}: {data: PatientExport}) {
  const patient = data.patient;
  const pathways = data.pathways ?? [];
  const history = data.pathwayHistory ?? [];
  // Every version of every note, those of deleted pathways too.
  const noteHistory = data.noteHistory ?? [];
  // Staff's requests to delete notes, with their reasons and outcomes.
  const deletionRequests = data.noteDeletionRequests ?? [];
  const appointments = data.appointments ?? [];
  const appointmentHistory = data.appointmentHistory ?? [];
  // Every text and email to the patient, and those that couldn't go, with why.
  const messages = data.messages ?? [];
  const access = data.accessLog ?? [];
  const notifications = data.notifications ?? [];
  const files = data.attachments ?? [];
  const referrals = data.referrals ?? [];
  const referralHistory = data.referralHistory ?? [];
  // Where each specimen came from and went; their history is under Pathway History, their reports under Files.
  const specimens = data.specimens ?? [];
  // Pathways put forward for a multidisciplinary team meeting, with the question and the team's outcome as written.
  const mdtItems = data.mdtItems ?? [];
  // Each referral by its specialty and when it was received, for its letters and its history.
  const referralNames = new Map(referrals.map((r) => [r.id, referralName(r)]));
  // What an appointment is for: its pathway's title (from the export's pathways) and its stage.
  const pathwayTitles = new Map(pathways.map((pathway) => [pathway.id, pathway.title ?? '']));
  const appointmentFor = (a: ExportedAppointment) =>
    a.pathwayId ? [pathwayTitles.get(a.pathwayId) || 'A deleted pathway', a.stageName].filter(Boolean).join(': ') : 'Not for a pathway';
  // Which appointment a notification is about, by its status and time.
  const appointmentsById = new Map(appointments.map((a) => [a.id, a]));
  const aboutAppointment = (appointmentId: string) => {
    const appointment = appointmentsById.get(appointmentId);
    return `About the appointment: ${appointment ? describeExportedAppointment(appointment) : 'no longer held'}`;
  };

  return (
    <VStack gap={6} hAlign="stretch">
      <ExportSection title="Patient">
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Name">{`${patient?.firstName ?? ''} ${patient?.lastName ?? ''}`.trim() || '—'}</MetadataListItem>
          <MetadataListItem label="Date of Birth">{patient?.dateOfBirth ? formatDate(patient.dateOfBirth) : '—'}</MetadataListItem>
          <MetadataListItem label="NHS Number">{patient?.nhsNumber ? formatNhsNumber(patient.nhsNumber) : 'Not given'}</MetadataListItem>
          <MetadataListItem label="Email">{patient?.email || '—'}</MetadataListItem>
          <MetadataListItem label="Mobile">{patient?.mobilePhone ? formatMobile(patient.mobilePhone) : '—'}</MetadataListItem>
          <MetadataListItem label="Reminders">{reminderChannelLabel(patient?.reminderChannel)}</MetadataListItem>
          <MetadataListItem label="Address">{oneLineAddress(patient?.addressLines, patient?.postcode) || '—'}</MetadataListItem>
          <MetadataListItem label="GP Practice">
            {patient?.practiceName ? `${patient.practiceName}${patient.practiceOdsCode ? ` (${patient.practiceOdsCode})` : ''}` : '—'}
          </MetadataListItem>
          <MetadataListItem label="Patient ID">{patient?.id}</MetadataListItem>
          <MetadataListItem label="Record Created">{when(patient?.createdAt)}</MetadataListItem>
          <MetadataListItem label="Last Changed">{when(patient?.lastUpdated)}</MetadataListItem>
          {patient?.restrictedAtUtc && (
            <MetadataListItem label="Restricted">{`${when(patient.restrictedAtUtc)}: ${patient.restrictionReason ?? ''}`}</MetadataListItem>
          )}
          {patient?.disputedAtUtc && (
            <MetadataListItem label="Disputed">{`${when(patient.disputedAtUtc)}: ${patient.disputeReason ?? ''}`}</MetadataListItem>
          )}
          {patient?.anonymisedAtUtc && <MetadataListItem label="Anonymised">{when(patient.anonymisedAtUtc)}</MetadataListItem>}
          {patient?.retentionKeptUntil && (
            <MetadataListItem label="Kept Past Retention Until">
              {`${formatDate(patient.retentionKeptUntil)}: ${patient.retentionKeptReason ?? ''}`}
            </MetadataListItem>
          )}
        </MetadataList>
      </ExportSection>

      <ExportSection title={`Pathways (${pathways.length})`}>
        {pathways.length === 0
          ? nothing('No pathways.')
          : (
            <VStack gap={6} hAlign="stretch">
              {pathways.map((pathway) => (
                <VStack key={pathway.id} gap={3} hAlign="stretch">
                  <Heading level={3}>{pathway.title}</Heading>
                  <MetadataList columns={2} label={{position: 'top'}}>
                    <MetadataListItem label="Template">{pathway.templateName || '—'}</MetadataListItem>
                    <MetadataListItem label="Status">{statusLabel(pathway.status ?? '')}</MetadataListItem>
                    <MetadataListItem label="Priority">{pathway.priority}</MetadataListItem>
                    <MetadataListItem label="Target date">{pathway.dueDate ? formatDate(pathway.dueDate) : '—'}</MetadataListItem>
                    <MetadataListItem label="Created">{when(pathway.createdAt)}</MetadataListItem>
                    <MetadataListItem label="Last Changed">{when(pathway.lastUpdated)}</MetadataListItem>
                    <MetadataListItem label="Assigned To">{(pathway.assignedTo ?? []).join(', ') || 'Nobody'}</MetadataListItem>
                  </MetadataList>
                  {(pathway.notes ?? []).length > 0 && <Heading level={4}>{`Notes (${(pathway.notes ?? []).length})`}</Heading>}
                  {(pathway.notes ?? []).map((note) => (
                    <VStack key={note.id} gap={1} hAlign="stretch">
                      <HStack gap={2} vAlign="center" wrap="wrap">
                        <Text type="supporting" color="secondary">
                          {`Note by ${note.author}, ${when(note.createdAtUtc)}${note.lastEditedAtUtc ? `, edited ${when(note.lastEditedAtUtc)}` : ''}`}
                        </Text>
                        {note.deletedAtUtc && <Token label="Deleted" size="sm" />}
                        {/* For whoever checks the export before release: the third-party and serious harm tests. */}
                        {note.isAboutAnotherPatient && <Token label={ANOTHER_PATIENTS_DETAILS} size="sm" color="red" />}
                      </HStack>
                      {/* Hidden from the pathway page, but kept: when, by whom and why. */}
                      {note.deletedAtUtc && <Text type="supporting" color="secondary">{deletedNoteText(note)}</Text>}
                      <RichText>{note.text ?? ''}</RichText>
                    </VStack>
                  ))}
                  <List hasDividers edge="bleed">
                    {(pathway.stages ?? []).map((stage) => (
                      <ListItem
                        key={stage.order}
                        align="first-line"
                        label={
                          // A skipped stage is struck through, as the pathway page shows it.
                          <Text type="body" weight="semibold" hasStrikethrough={Boolean(stage.skippedAtUtc)}>
                            {`${stage.order}. ${stage.name}${stage.branch ? ` (${stage.branch} branch)` : ''}`}
                          </Text>
                        }
                        description={
                          <VStack gap={1}>
                            <Text type="supporting" color="secondary">
                              {stage.skippedAtUtc && !stage.startedAtUtc
                                ? skippedStageText(stage)
                                : [
                                    stage.startedAtUtc ? `Started ${when(stage.startedAtUtc)} by ${stage.startedBy}` : 'Not started',
                                    stage.finishedAtUtc ? `finished ${when(stage.finishedAtUtc)} by ${stage.finishedBy}` : null,
                                  ]
                                    .filter(Boolean)
                                    .join(', ')}
                            </Text>
                            {/* Started, then skipped: both are said. */}
                            {stage.skippedAtUtc && stage.startedAtUtc && (
                              <Text type="supporting" color="secondary">
                                {skippedStageText(stage)}
                              </Text>
                            )}
                            {stage.notes ? <RichText>{stage.notes}</RichText> : null}
                          </VStack>
                        }
                      />
                    ))}
                  </List>
                </VStack>
              ))}
            </VStack>
          )}
      </ExportSection>

      <ExportSection title={`Referrals (${referrals.length})`}>
        {referrals.length === 0
          ? nothing('No referrals.')
          : (
            <VStack gap={6} hAlign="stretch">
              {referrals.map((referral) => (
                <VStack key={referral.id} gap={3} hAlign="stretch">
                  <Heading level={3}>{`Referral to ${referral.specialtyName}`}</Heading>
                  <MetadataList columns={2} label={{position: 'top'}}>
                    <MetadataListItem label="Status">{referralStatusLabel(referral.status)}</MetadataListItem>
                    <MetadataListItem label="Received">{referral.receivedOn ? formatDate(referral.receivedOn) : '—'}</MetadataListItem>
                    <MetadataListItem label="Priority Asked For">{referral.requestedPriority}</MetadataListItem>
                    <MetadataListItem label="Practice">{referral.referrerPractice || '—'}</MetadataListItem>
                    <MetadataListItem label="Clinician">{referral.referrerClinician || '—'}</MetadataListItem>
                    <MetadataListItem label="ODS Code">{referral.referrerOdsCode || '—'}</MetadataListItem>
                    <MetadataListItem label="Registered">{`${when(referral.registeredAtUtc)} by ${referral.registeredBy}`}</MetadataListItem>
                    <MetadataListItem label="Decided">
                      {referral.decidedAtUtc ? `${when(referral.decidedAtUtc)} by ${referral.decidedBy}` : 'Not yet'}
                    </MetadataListItem>
                    {referral.rejectionReason && <MetadataListItem label="Why Rejected">{rejectionReasonWords(referral.rejectionReason)}</MetadataListItem>}
                    {referral.redirectedTo && <MetadataListItem label="Redirected To">{referral.redirectedTo}</MetadataListItem>}
                    {referral.decisionDetail && <MetadataListItem label="Detail">{referral.decisionDetail}</MetadataListItem>}
                    {referral.redirectDetail && (
                      <MetadataListItem label="Redirected From Another Specialty">{referral.redirectDetail}</MetadataListItem>
                    )}
                    {referral.pathwayId && (
                      <MetadataListItem label="Accepted Into">{pathwayTitles.get(referral.pathwayId) || 'A deleted pathway'}</MetadataListItem>
                    )}
                  </MetadataList>
                  <Text type="body">{`Reason for referral: ${referral.reason ?? ''}`}</Text>
                  <ReferralClock referral={referral} />
                </VStack>
              ))}
            </VStack>
          )}
      </ExportSection>

      <ExportSection title={`Specimens (${specimens.length})`}>
        {specimens.length === 0
          ? nothing('No specimens.')
          : (
            <VStack gap={6} hAlign="stretch">
              {specimens.map((specimen) => (
                <VStack key={specimen.id} gap={3} hAlign="stretch">
                  <Heading level={3}>{`Specimen ${specimen.number}: ${specimen.specimenTypeName}`}</Heading>
                  <MetadataList columns={2} label={{position: 'top'}}>
                    <MetadataListItem label="Status">{specimenStatusLabel(specimen.status)}</MetadataListItem>
                    <MetadataListItem label="Pathway">{pathwayTitles.get(specimen.pathwayId) || 'A deleted pathway'}</MetadataListItem>
                    <MetadataListItem label="Lab">{specimen.laboratoryName}</MetadataListItem>
                    <MetadataListItem label="Requested By">{specimen.requestedBy}</MetadataListItem>
                    <MetadataListItem label="Taken">{`${when(specimen.takenAtUtc)} by ${specimen.takenBy}`}</MetadataListItem>
                    <MetadataListItem label="Site">{specimen.site || '—'}</MetadataListItem>
                    <MetadataListItem label="Sent">
                      {specimen.sentAtUtc ? `${when(specimen.sentAtUtc)}${specimen.courierReference ? `, reference ${specimen.courierReference}` : ''}` : 'Not sent'}
                    </MetadataListItem>
                    <MetadataListItem label="Received">
                      {specimen.receivedAtUtc ? `${when(specimen.receivedAtUtc)}${specimen.labReference ? `, lab's reference ${specimen.labReference}` : ''}` : 'Not received'}
                    </MetadataListItem>
                    <MetadataListItem label="Reported">
                      {specimen.reportedAtUtc ? `${when(specimen.reportedAtUtc)}${specimen.reportAttachmentId ? ', its report is under Files' : ''}` : 'Not reported'}
                    </MetadataListItem>
                    {specimen.closedAtUtc && (
                      <MetadataListItem label="Closed">{`${when(specimen.closedAtUtc)} by ${specimen.closedBy ?? 'unknown'}`}</MetadataListItem>
                    )}
                    {specimen.rejectionReason && <MetadataListItem label="Why Rejected">{rejectionReasonLabel(specimen.rejectionReason)}</MetadataListItem>}
                    {specimen.closedDetail && <MetadataListItem label="Detail">{specimen.closedDetail}</MetadataListItem>}
                  </MetadataList>
                  <Text type="body">{`Notes for the lab: ${specimen.requestNotes || 'none'}`}</Text>
                </VStack>
              ))}
            </VStack>
          )}
      </ExportSection>

      <ExportSection title={`Files (${files.length})`}>
        <VStack gap={4} hAlign="stretch">
          <Text type="body" color="secondary">
            The details of every file attached to the patient, their pathways, stages and referrals, including deleted ones. The files
            themselves aren't included in this export.
          </Text>
          {files.length === 0
            ? nothing('No files.')
            : (
              <List hasDividers edge="bleed">
                {files.map((file) => (
                  <ListItem
                    key={file.id}
                    align="first-line"
                    label={
                      <HStack gap={2} vAlign="center" wrap="wrap">
                        <Text type="body" weight="semibold">
                          {file.fileName}
                        </Text>
                        {file.deletedAtUtc && <Token size="sm" label="Deleted" color="gray" />}
                      </HStack>
                    }
                    description={
                      <VStack gap={1}>
                        <Text type="supporting" color="secondary">
                          {`${file.typeName} (${file.contentType}), ${formatFileSize(file.sizeBytes ?? 0)}. ${attachedToText(file, pathwayTitles, referralNames)}`}
                        </Text>
                        <Text type="supporting" color="secondary">
                          {`Added ${when(file.uploadedAtUtc)} by ${file.uploadedBy}${file.deletedAtUtc ? `; deleted ${when(file.deletedAtUtc)} by ${file.deletedBy}` : ''}`}
                        </Text>
                        <Text type="supporting" color="secondary">
                          {`Malware scan: ${SCAN_STATES[file.scanStatus ?? ''] ?? file.scanStatus}. SHA-256: ${file.sha256}`}
                        </Text>
                      </VStack>
                    }
                  />
                ))}
              </List>
            )}
        </VStack>
      </ExportSection>

      <ExportSection title={`Pathway History (${history.length})`}>
        {history.length === 0
          ? nothing('No changes recorded.')
          : (
            <List hasDividers edge="bleed">
              {history.map((entry, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${auditActionLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    <Text type="supporting" color="secondary">
                      {[entry.pathwayTitle, entry.stageName, describeAuditDetails(entry)].filter(Boolean).join(' · ')}
                    </Text>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Note History (${noteHistory.length})`}>
        {noteHistory.length === 0
          ? nothing('No notes.')
          : (
            <List hasDividers edge="bleed">
              {noteHistory.map((version, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={
                    <Text type="body">
                      {`${when(version.atUtc)}: ${version.version === 1 ? 'Note added' : `Note edited (version ${version.version})`} by ${version.username}`}
                    </Text>
                  }
                  description={
                    <VStack gap={1} hAlign="stretch">
                      <Text type="supporting" color="secondary">
                        {pathwayTitles.get(version.pathwayId) || 'A deleted pathway'}
                      </Text>
                      <RichText>{version.text ?? ''}</RichText>
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Note Deletion Requests (${deletionRequests.length})`}>
        {deletionRequests.length === 0
          ? nothing('No requests to delete notes.')
          : (
            <List hasDividers edge="bleed">
              {deletionRequests.map((request) => (
                <ListItem
                  key={request.id}
                  align="first-line"
                  label={<Text type="body">{`${when(request.requestedAtUtc)}: ${request.requestedBy} asked for a note to be deleted`}</Text>}
                  description={
                    <VStack gap={1} hAlign="stretch">
                      <Text type="supporting" color="secondary">
                        {pathwayTitles.get(request.pathwayId) || 'A deleted pathway'}
                      </Text>
                      <Text type="body">{`Reason: ${describeNoteDeletionReason(request.reason, request.detail)}`}</Text>
                      <Text type="body">{deletionRequestOutcome(request)}</Text>
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Appointments (${appointments.length})`}>
        {appointments.length === 0
          ? nothing('No appointments.')
          : (
            <List hasDividers edge="bleed">
              {appointments.map((a) => (
                <ListItem
                  key={a.id}
                  align="first-line"
                  label={
                    <Text type="body" weight="semibold">
                      {describeExportedAppointment(a)}
                    </Text>
                  }
                  description={
                    <VStack gap={1}>
                      <Text type="supporting" color="secondary">
                        {[appointmentFor(a), appointmentHistoryWords(a)].filter(Boolean).join(', ')}
                      </Text>
                      {a.cancelReason && (
                        <Text type="supporting" color="secondary">
                          {`${a.status === 'EnteredInError' ? 'Why entered in error' : 'Why cancelled'}: ${a.cancelReason}`}
                        </Text>
                      )}
                      {a.dnaFollowUp && (
                        <Text type="supporting" color="secondary">
                          {[dnaFollowUpExportWords(a), a.dnaFollowUpDetail].filter(Boolean).join(': ')}
                        </Text>
                      )}
                      {a.notes && <Text type="body">{`Notes: ${a.notes}`}</Text>}
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Appointment History (${appointmentHistory.length})`}>
        {appointmentHistory.length === 0
          ? nothing('No appointment changes recorded.')
          : (
            <List hasDividers edge="bleed">
              {appointmentHistory.map((entry, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${appointmentChangeLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    <Text type="supporting" color="secondary">
                      {[
                        entry.pathwayTitle || 'Not for a pathway',
                        entry.stageName,
                        appointmentSlot(entry.startUtc, entry.endUtc, entry.staffUsername),
                        describeAppointmentChange(entry),
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </Text>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Messages (${messages.length})`}>
        {messages.length === 0
          ? nothing('No texts or emails to the patient.')
          : (
            <List hasDividers edge="bleed">
              {messages.map((m, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(m.createdAtUtc)}: ${exportedMessageTitle(m)}`}</Text>}
                  description={
                    <VStack gap={1}>
                      <Text type="supporting" color="secondary">
                        {[
                          exportedMessageOutcome(m),
                          `About the appointment at ${formatAppointmentTime(m.appointmentStartUtc ?? '')}`,
                          m.requestedBy ? `Asked for by ${m.requestedBy}` : 'Sent by MedApp',
                        ].join(' · ')}
                      </Text>
                      {m.body && <Text type="body">{m.body}</Text>}
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Referral History (${referralHistory.length})`}>
        {referralHistory.length === 0
          ? nothing('No referral changes recorded.')
          : (
            <List hasDividers edge="bleed">
              {referralHistory.map((entry, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${referralChangeLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    <Text type="supporting" color="secondary">
                      {[capitalised(referralNames.get(entry.referralId) ?? 'a referral'), describeReferralChange(entry.details)].filter(Boolean).join(' · ')}
                    </Text>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`MDT Discussions (${mdtItems.length})`}>
        {mdtItems.length === 0
          ? nothing('Never listed for an MDT.')
          : (
            <List hasDividers edge="bleed">
              {mdtItems.map((item, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(item.listedAtUtc)}: listed for the ${item.specialty} MDT by ${item.listedBy}`}</Text>}
                  description={
                    <VStack gap={1}>
                      <Text type="supporting" color="secondary">
                        {[
                          item.pathwayTitle,
                          mdtStatusWords(item.status),
                          item.meetingStartUtc
                            ? `Meeting ${when(item.meetingStartUtc)}`
                            : item.meetingCancelledStartUtc
                              ? `Meeting of ${when(item.meetingCancelledStartUtc)} cancelled`
                              : null,
                          item.withdrawnAtUtc ? `Withdrawn ${when(item.withdrawnAtUtc)} by ${item.withdrawnBy}` : null,
                        ]
                          .filter(Boolean)
                          .join(' · ')}
                      </Text>
                      <Text type="body">{`Question: ${item.question ?? ''}`}</Text>
                      {item.outcomeAtUtc && (
                        <Text type="body">
                          {`Outcome (${when(item.outcomeAtUtc)}, by ${item.outcomeBy}; present: ${(item.present ?? []).join(', ')}): ${item.outcome ?? ''}`}
                        </Text>
                      )}
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Access Log (${access.length})`}>
        {access.length === 0
          ? nothing('No access recorded.')
          : (
            <List hasDividers edge="bleed">
              {access.map((entry, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${activityActionLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    describeActivity(entry) ? (
                      <Text type="supporting" color="secondary">
                        {describeActivity(entry)}
                      </Text>
                    ) : undefined
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Notifications (${notifications.length})`}>
        {notifications.length === 0
          ? nothing('No notifications.')
          : (
            <List hasDividers edge="bleed">
              {notifications.map((n, i) => (
                <ListItem
                  key={i}
                  align="first-line"
                  label={<Text type="body">{`${when(n.createdAtUtc)}: to ${n.username}`}</Text>}
                  description={
                    <VStack gap={1}>
                      <Text type="supporting" color="secondary">
                        {`${n.title}: ${n.message}`}
                      </Text>
                      {n.appointmentId && (
                        <Text type="supporting" color="secondary">
                          {aboutAppointment(n.appointmentId)}
                        </Text>
                      )}
                    </VStack>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>
    </VStack>
  );
}

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
import {appointmentStatusLabel} from '../../api/appointments';
import {auditActionLabel, describeAuditDetails} from '../../api/audit';
import {ApiError} from '../../api/errors';
import {patientExportQuery, type PatientExport} from '../../api/patients';
import {Page} from '../../components/pages/Page';
import {statusLabel} from '../../components/procedures/ProcedureCells';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {RichText} from '../../components/shared/RichText';
import {PanelHeader} from '../../components/views/PanelCard';
import {sameMoment} from '../../utils/calendarSlots';
import {formatAppointmentTime, formatDate, formatDateTime} from '../../utils/dates';
import {formatFileSize} from '../../utils/fileSize';
import {formatNhsNumber} from '../../utils/nhsNumber';

/** While printing, only the export shows: the side nav and the page's buttons are left off the paper. */
const PRINT_CSS = '@media print { .astryx-side-nav, button { display: none !important; } }';

/** The export's file name: the patient's ID and the day, never their name. */
export const exportFileName = (data: PatientExport) =>
  `patient-${data.patient?.id ?? 'record'}-${(data.exportedAtUtc ?? '').slice(0, 10) || 'export'}.json`;

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
    case 'NotesUpdated': {
      const change = `${quoted(d.from)} → ${quoted(d.to)}`;
      return change.charAt(0).toUpperCase() + change.slice(1);
    }
    default:
      return '';
  }
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
 * A subject access export (admin): everything MedApp holds about a patient (their details, procedures with stages,
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

/** What an exported file was attached to, in words: "Attached to the patient", "Attached to Consent, in Knee review". */
function attachedToText(file: ExportedFile, procedureTitles: Map<string | undefined, string>): string {
  if (file.attachedTo === 'Patient' || !file.procedureId) return 'Attached to the patient.';
  const title = procedureTitles.get(file.procedureId) || 'a procedure';
  return file.attachedTo === 'Stage' ? `Attached to ${file.stageName ?? 'a stage'}, in ${title}.` : `Attached to ${title}.`;
}

function ExportContent({data}: {data: PatientExport}) {
  const patient = data.patient;
  const procedures = data.procedures ?? [];
  const history = data.procedureHistory ?? [];
  // Every version of every note, those of deleted procedures too.
  const noteHistory = data.noteHistory ?? [];
  const appointments = data.appointments ?? [];
  const appointmentHistory = data.appointmentHistory ?? [];
  const access = data.accessLog ?? [];
  const notifications = data.notifications ?? [];
  const files = data.attachments ?? [];
  // What an appointment is for: its procedure's title (from the export's procedures) and its stage.
  const procedureTitles = new Map(procedures.map((procedure) => [procedure.id, procedure.title ?? '']));
  const appointmentFor = (a: ExportedAppointment) =>
    a.procedureId ? [procedureTitles.get(a.procedureId) || 'A deleted procedure', a.stageName].filter(Boolean).join(': ') : 'Not for a procedure';
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

      <ExportSection title={`Procedures (${procedures.length})`}>
        {procedures.length === 0
          ? nothing('No procedures.')
          : (
            <VStack gap={6} hAlign="stretch">
              {procedures.map((procedure) => (
                <VStack key={procedure.id} gap={3} hAlign="stretch">
                  <Heading level={3}>{procedure.title}</Heading>
                  <MetadataList columns={2} label={{position: 'top'}}>
                    <MetadataListItem label="Template">{procedure.templateName || '—'}</MetadataListItem>
                    <MetadataListItem label="Status">{statusLabel(procedure.status ?? '')}</MetadataListItem>
                    <MetadataListItem label="Priority">{procedure.priority}</MetadataListItem>
                    <MetadataListItem label="Due">{procedure.dueDate ? formatDate(procedure.dueDate) : '—'}</MetadataListItem>
                    <MetadataListItem label="Created">{when(procedure.createdAt)}</MetadataListItem>
                    <MetadataListItem label="Last Changed">{when(procedure.lastUpdated)}</MetadataListItem>
                    <MetadataListItem label="Assigned To">{(procedure.assignedTo ?? []).join(', ') || 'Nobody'}</MetadataListItem>
                  </MetadataList>
                  {(procedure.notes ?? []).length > 0 && <Heading level={4}>{`Notes (${(procedure.notes ?? []).length})`}</Heading>}
                  {(procedure.notes ?? []).map((note) => (
                    <VStack key={note.id} gap={1} hAlign="stretch">
                      <Text type="supporting" color="secondary">
                        {`Note by ${note.author}, ${when(note.createdAtUtc)}${note.lastEditedAtUtc ? `, edited ${when(note.lastEditedAtUtc)}` : ''}`}
                      </Text>
                      <RichText>{note.text ?? ''}</RichText>
                    </VStack>
                  ))}
                  <List hasDividers edge="bleed">
                    {(procedure.stages ?? []).map((stage) => (
                      <ListItem
                        key={stage.order}
                        align="first-line"
                        label={
                          <Text type="body" weight="semibold">
                            {`${stage.order}. ${stage.name}`}
                          </Text>
                        }
                        description={
                          <VStack gap={1}>
                            <Text type="supporting" color="secondary">
                              {[
                                stage.startedAtUtc ? `Started ${when(stage.startedAtUtc)} by ${stage.startedBy}` : 'Not started',
                                stage.finishedAtUtc ? `finished ${when(stage.finishedAtUtc)} by ${stage.finishedBy}` : null,
                              ]
                                .filter(Boolean)
                                .join(', ')}
                            </Text>
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

      <ExportSection title={`Files (${files.length})`}>
        <VStack gap={4} hAlign="stretch">
          <Text type="body" color="secondary">
            The details of every file attached to the patient, their procedures and stages, including deleted ones. The files
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
                          {`${file.typeName} (${file.contentType}), ${formatFileSize(file.sizeBytes ?? 0)}. ${attachedToText(file, procedureTitles)}`}
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

      <ExportSection title={`Procedure History (${history.length})`}>
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
                      {[entry.procedureTitle, entry.stageName, describeAuditDetails(entry)].filter(Boolean).join(' · ')}
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
                        {procedureTitles.get(version.procedureId) || 'A deleted procedure'}
                      </Text>
                      <RichText>{version.text ?? ''}</RichText>
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
                        entry.procedureTitle || 'Not for a procedure',
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

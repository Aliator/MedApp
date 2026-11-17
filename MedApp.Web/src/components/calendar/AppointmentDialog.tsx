import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useRef, useState} from 'react';
import {appointmentQuery, useAppointmentActions, type Appointment} from '../../api/appointments';
import {ApiValidationError, fieldStatus, problemMessage} from '../../api/errors';
import {whyCantChange} from '../../api/patients';
import {useAnnounce} from '../../app/announcements';
import {formatCalendarDay, formatTimeRange} from '../../utils/calendarSlots';
import {formatDateTime, ukDay} from '../../utils/dates';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AppointmentPatientBanner, useAppointmentPatient} from './AppointmentPatient';
import {BookAppointmentDialog} from './BookAppointmentDialog';
import {appointmentPurpose} from './calendarItems';
import {AppointmentStatusToken, VisitRequirementToken} from './CalendarTokens';

export interface AppointmentDialogProps {
  /** The appointment to show; it's fetched afresh, and shown from this until then. Null when closed. */
  appointment: Appointment | null;
  onClose: () => void;
  /** The finest steps booking and rescheduling offer (`BookAppointmentDialog`'s): 5 from a calendar in 5-minute rows. */
  stepMinutes?: number;
}

/** When an appointment is, in words: "Monday 23 November, 09:00 to 09:30 with jsmith", or that it isn't booked yet. */
export function appointmentWhen(appointment: Appointment): string {
  if (!appointment.startUtc || !appointment.endUtc) return 'Not booked yet';
  const when = `${formatCalendarDay(ukDay(appointment.startUtc))}, ${formatTimeRange(appointment.startUtc, appointment.endUtc)}`;
  return appointment.staffUsername ? `${when} with ${appointment.staffUsername}` : when;
}

type Mode = 'view' | 'book' | 'bookAnother' | 'cancel';

/**
 * An appointment's details, and everything that can be done to it (docs/calendar-plan.md, decisions 15 and 16): who
 * it's for (links to the patient and the procedure), its status, when and with whom, its notes (editable), who asked
 * for it, booked it and recorded how it went, and why it was cancelled. Its buttons follow what the API allows
 * (`can...`): Book, Reschedule, Attended and Did not attend (once it has started), Cancel (with a reason), and Book
 * another (after Did not attend or Cancelled). Booking opens `BookAppointmentDialog` in its place, and comes back here.
 * The patient's banner heads it (name, date of birth and NHS number), with their record's flags: a restricted
 * patient's appointment can only be cancelled, and an anonymised one's not changed at all (the API's flags say so),
 * and neither is offered Book another.
 *
 * Use it wherever an appointment is opened: the calendar, Needs booking, a procedure or a patient.
 */
export function AppointmentDialog({appointment: initial, onClose, stepMinutes}: AppointmentDialogProps) {
  const isOpen = initial !== null;
  const announce = useAnnounce();
  const actions = useAppointmentActions();
  const fresh = useQuery({...appointmentQuery(initial?.id ?? ''), enabled: isOpen && !!initial?.id});
  const appointment = fresh.data ?? initial;
  const patientState = useAppointmentPatient(isOpen ? (appointment?.patientId ?? undefined) : undefined);
  const [mode, setMode] = useState<Mode>('view');
  const [notes, setNotes] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [notesError, setNotesError] = useState<ReturnType<typeof fieldStatus>>(undefined);
  const [openedFor, setOpenedFor] = useState(initial?.id);

  // A different appointment (or the same one opened again) starts clean.
  if (initial?.id !== openedFor) {
    setOpenedFor(initial?.id);
    setMode('view');
    setNotes(null);
    setReason('');
    setProblem(null);
    setNotesError(undefined);
  }

  const content = useRef<HTMLDivElement>(null);
  const shownNotes = notes ?? appointment?.notes ?? '';
  const notesChanged = notes !== null && notes !== (appointment?.notes ?? '');

  const fail = (error: unknown) => setProblem(problemMessage(error, 'The appointment could not be changed. Please try again.'));

  // The appointment's ID (the dialog only shows, and acts, with one).
  const id = initial?.id ?? '';

  const saveNotes = () => {
    if (!notesChanged) return false;
    setProblem(null);
    setNotesError(undefined);
    actions.saveNotes.mutate(
      {id, notes: shownNotes.trim()},
      {
        onSuccess: () => {
          setNotes(null);
          announce('Notes saved');
        },
        onError: (error) => {
          if (error instanceof ApiValidationError) setNotesError(fieldStatus(error.errors, 'notes'));
          else fail(error);
        },
      },
    );
  };
  useSaveShortcut(content, isOpen && mode === 'view' && appointment?.canEditNotes ? saveNotes : undefined);

  const record = (outcome: 'Attended' | 'DidNotAttend') => {
    setProblem(null);
    actions.recordOutcome.mutate(
      {id, outcome},
      {onSuccess: () => announce(outcome === 'Attended' ? 'Marked Attended' : 'Marked Did not attend'), onError: fail},
    );
  };

  const cancel = () => {
    actions.cancel.mutate(
      {id, reason: reason.trim()},
      {
        onSuccess: () => {
          announce('Appointment cancelled');
          setMode('view');
          setReason('');
        },
        onError: (error) => {
          setMode('view');
          fail(error);
        },
      },
    );
  };

  if (!appointment) return null;

  const status = appointment.status ?? '';
  const isBusy = actions.recordOutcome.isPending || actions.saveNotes.isPending || actions.cancel.isPending;
  // Only once the patient is known to be bookable (not restricted or anonymised), as booking needs their details.
  const patient = patientState.patient;
  const canBookAnother = (status === 'DidNotAttend' || status === 'Cancelled') && patient !== undefined && whyCantChange(patient) === null;
  const purpose = appointmentPurpose(appointment);

  const history: {label: string; value: string}[] = [
    {
      label: 'Requested',
      value: appointment.isAutomatic
        ? `Automatically, ${formatDateTime(appointment.createdAtUtc ?? '')}`
        : `By ${appointment.createdByUsername ?? 'someone'}, ${formatDateTime(appointment.createdAtUtc ?? '')}`,
    },
  ];
  if (appointment.bookedAtUtc) {
    history.push({label: 'Booked', value: `By ${appointment.bookedByUsername ?? 'someone'}, ${formatDateTime(appointment.bookedAtUtc)}`});
  }
  if (appointment.outcomeAtUtc) {
    history.push({label: 'Outcome recorded', value: `By ${appointment.outcomeByUsername ?? 'someone'}, ${formatDateTime(appointment.outcomeAtUtc)}`});
  }

  return (
    <>
      <DialogFrame
        isOpen={isOpen && mode === 'view'}
        title="Appointment"
        icon={<Icon icon={AppIcon.Calendar} size="lg" color="accent" />}
        onClose={onClose}
        width={560}
        purpose={isBusy ? 'required' : 'info'}
        footer={
          <HStack gap={2} wrap="wrap" hAlign="end">
            {appointment.canCancel && (
              <Button label="Cancel Appointment" variant="destructive" isDisabled={isBusy} onClick={() => setMode('cancel')} />
            )}
            {appointment.canRecordOutcome && (
              <>
                <Button
                  label="Did not attend"
                  variant={status === 'DidNotAttend' ? 'primary' : 'secondary'}
                  icon={<Icon icon={AppIcon.AppointmentDidNotAttend} />}
                  isDisabled={isBusy || status === 'DidNotAttend'}
                  isLoading={actions.recordOutcome.isPending && actions.recordOutcome.variables?.outcome === 'DidNotAttend'}
                  onClick={() => record('DidNotAttend')}
                />
                <Button
                  label="Attended"
                  variant={status === 'Attended' ? 'primary' : 'secondary'}
                  icon={<Icon icon={AppIcon.Success} />}
                  isDisabled={isBusy || status === 'Attended'}
                  isLoading={actions.recordOutcome.isPending && actions.recordOutcome.variables?.outcome === 'Attended'}
                  onClick={() => record('Attended')}
                />
              </>
            )}
            {canBookAnother && <Button label="Book Another" variant="secondary" icon={<Icon icon={AppIcon.Book} />} onClick={() => setMode('bookAnother')} />}
            {appointment.canReschedule && <Button label="Reschedule" variant="secondary" isDisabled={isBusy} onClick={() => setMode('book')} />}
            {appointment.canBook && <Button label="Book" variant="primary" icon={<Icon icon={AppIcon.Book} />} onClick={() => setMode('book')} />}
            <Button label="Close" variant="secondary" isDisabled={isBusy} onClick={onClose} />
          </HStack>
        }>
        <VStack ref={content} gap={5} hAlign="stretch">
          <AppointmentPatientBanner state={patientState} name={appointment.patientName} />
          {problem && <AlertMessage message={problem} />}
          <VStack gap={1}>
            <HStack gap={2} vAlign="center" wrap="wrap">
              <AppointmentStatusToken status={status} />
              <VisitRequirementToken requirement={appointment.visitRequirement} />
            </HStack>
            <Text type="large" weight="semibold">
              {appointmentWhen(appointment)}
            </Text>
          </VStack>
          <MetadataList label={{position: 'start', width: 140}}>
            <MetadataListItem label="Patient">
              <Link href={`/patients/${appointment.patientId}`} onClick={onClose}>
                {appointment.patientName ?? ''}
              </Link>
            </MetadataListItem>
            <MetadataListItem label="For">
              {appointment.procedureId ? (
                <Link href={`/procedures/${appointment.procedureId}`} onClick={onClose}>
                  {purpose}
                </Link>
              ) : (
                purpose
              )}
            </MetadataListItem>
            {history.map((h) => (
              <MetadataListItem key={h.label} label={h.label}>
                {h.value}
              </MetadataListItem>
            ))}
            {appointment.cancelReason && <MetadataListItem label="Why cancelled">{appointment.cancelReason}</MetadataListItem>}
          </MetadataList>
          {appointment.canEditNotes ? (
            <VStack gap={2} hAlign="stretch">
              <TextArea
                label="Notes"
                isOptional
                description="About the visit, as procedure notes are."
                value={shownNotes}
                onChange={setNotes}
                rows={3}
                maxLength={2000}
                status={notesError}
              />
              <HStack hAlign="end">
                <Button
                  label="Save Notes"
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Save} />}
                  isDisabled={!notesChanged}
                  isLoading={actions.saveNotes.isPending}
                  onClick={saveNotes}
                />
              </HStack>
            </VStack>
          ) : (
            appointment.notes && (
              <MetadataList label={{position: 'top'}}>
                <MetadataListItem label="Notes">{appointment.notes}</MetadataListItem>
              </MetadataList>
            )
          )}
        </VStack>
      </DialogFrame>
      <ConfirmDialog
        isOpen={isOpen && mode === 'cancel'}
        title="Cancel Appointment"
        message="A cancelled appointment can't be booked again; book another instead. Say why it's being cancelled."
        icon={AppIcon.Cancel}
        isDanger
        confirmText="Cancel Appointment"
        cancelText="Keep It"
        confirmDisabled={!reason.trim()}
        isProcessing={actions.cancel.isPending}
        onConfirm={cancel}
        onCancel={() => setMode('view')}>
        <TextArea label="Reason" value={reason} onChange={setReason} rows={3} maxLength={500} isRequired />
      </ConfirmDialog>
      <BookAppointmentDialog
        isOpen={isOpen && mode === 'book'}
        appointment={appointment}
        stepMinutes={stepMinutes}
        onClose={() => setMode('view')}
      />
      <BookAppointmentDialog
        isOpen={isOpen && mode === 'bookAnother'}
        patient={patient && {...patient, id: appointment.patientId ?? ''}}
        procedure={appointment.procedureId ? {id: appointment.procedureId, title: appointment.procedureTitle ?? ''} : undefined}
        stage={appointment.stageId ? {id: appointment.stageId, name: appointment.stageName ?? ''} : undefined}
        suggestedStaffUserId={appointment.staffUserId ?? undefined}
        stepMinutes={stepMinutes}
        onSaved={onClose}
        onClose={() => setMode('view')}
      />
    </>
  );
}

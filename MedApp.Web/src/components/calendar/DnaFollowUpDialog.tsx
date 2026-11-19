import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {
  DNA_FOLLOW_UPS,
  dnaFollowUpLabel,
  MAX_DNA_FOLLOW_UP_DETAIL,
  useAppointmentActions,
  type Appointment,
  type DnaFollowUpDecision,
} from '../../api/appointments';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {formatAppointmentTime, formatDateTime} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AppointmentPatientBanner, useAppointmentPatient, type AppointmentPatientState} from './AppointmentPatient';
import {appointmentPurpose} from './calendarItems';

/** What the dialog says first: recording a follow-up is a record, never an action on the pathway (decision 132). */
export const DNA_FOLLOW_UP_CHANGES_NOTHING =
  'Recording this changes nothing else. MedApp never discharges a patient or changes a pathway because of a missed appointment.';

/** The decision that needs a detail (who decided there's to be no further appointment, and why). */
const needsDetail = (decision: string) => decision === 'NoFurtherAppointment';

/**
 * The follow-up recorded so far, in words: "Rebook, by k.ashford, 24 Nov 2026, 10:00", or null when there's none.
 * The detail (free text) isn't part of it; show it on its own.
 */
export function dnaFollowUpWords(appointment: Pick<Appointment, 'dnaFollowUp' | 'dnaFollowUpByUsername' | 'dnaFollowUpAtUtc'>): string | null {
  if (!appointment.dnaFollowUp) return null;
  const by = appointment.dnaFollowUpByUsername ? `, by ${appointment.dnaFollowUpByUsername}` : '';
  const at = appointment.dnaFollowUpAtUtc ? `, ${formatDateTime(appointment.dnaFollowUpAtUtc)}` : '';
  return `${dnaFollowUpLabel(appointment.dnaFollowUp)}${by}${at}`;
}

export interface DnaFollowUpDialogProps {
  isOpen: boolean;
  /** The appointment marked Did not attend. */
  appointment: Appointment | null;
  onClose: () => void;
  /** Called with the appointment as saved, after the dialog has announced it. */
  onSaved?: (saved: Appointment) => void;
  /** Who the patient is, when the caller has already read it (the appointment dialog); otherwise it's read here. */
  patientState?: AppointmentPatientState;
}

/**
 * Recording what happens after a patient did not attend (centre plan decision 132, hazard H-55): **Rebook**,
 * **Clinician to review** or **No further appointment** (nothing chosen until someone chooses), with a detail of up
 * to 500 characters, required for No further appointment (who decided, and why). It shows who the patient is (their
 * banner, hazard H-34) and which appointment was missed, and says that recording it changes nothing else: MedApp
 * never discharges a patient or changes a pathway because of a missed appointment. The API's 400 shows on the
 * fields, anything else in its words.
 *
 * Used by the appointment dialog and the Requires Appointment page's Did not attend list.
 */
export function DnaFollowUpDialog({isOpen, appointment, onClose, onSaved, patientState}: DnaFollowUpDialogProps) {
  const announce = useAnnounce();
  const {recordDnaFollowUp} = useAppointmentActions();
  const read = useAppointmentPatient(isOpen && !patientState ? (appointment?.patientId ?? undefined) : undefined);
  const patient = patientState ?? read;
  const [decision, setDecision] = useState<DnaFollowUpDecision | ''>('');
  const [detail, setDetail] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [openedFor, setOpenedFor] = useState<string | undefined>(undefined);

  // Each opening starts with nothing chosen.
  const openKey = isOpen ? appointment?.id : undefined;
  if (openKey !== openedFor) {
    setOpenedFor(openKey);
    setDecision('');
    setDetail('');
    setErrors(null);
    setProblem(null);
  }

  const isComplete = decision !== '' && detail.length <= MAX_DNA_FOLLOW_UP_DETAIL && (!needsDetail(decision) || detail.trim() !== '');

  const save = () => {
    if (!appointment?.id || decision === '' || !isComplete) return false;
    setErrors(null);
    setProblem(null);
    recordDnaFollowUp.mutate(
      {id: appointment.id, decision, detail: detail.trim() || null},
      {
        onSuccess: (saved) => {
          announce(`Follow-up recorded: ${dnaFollowUpLabel(decision)}`);
          onSaved?.(saved);
          onClose();
        },
        onError: (error) => {
          if (error instanceof ApiValidationError) setErrors(error.errors);
          else setProblem(problemMessage(error, 'The follow-up could not be recorded. Please try again.'));
        },
      },
    );
  };

  const recorded = appointment ? dnaFollowUpWords(appointment) : null;

  return (
    <ConfirmDialog
      isOpen={isOpen && appointment !== null}
      title="Record Follow-up"
      message={DNA_FOLLOW_UP_CHANGES_NOTHING}
      icon={AppIcon.DnaFollowUp}
      confirmText="Record Follow-up"
      confirmDisabled={!isComplete}
      isProcessing={recordDnaFollowUp.isPending}
      hasSaveShortcut
      onConfirm={save}
      onCancel={onClose}>
      {isOpen && appointment && (
        <VStack gap={4} hAlign="stretch">
          <AppointmentPatientBanner state={patient} name={appointment.patientName} />
          {problem && <AlertMessage label="Follow-up not recorded" message={problem} />}
          <MetadataList label={{position: 'start', width: 140}}>
            <MetadataListItem label="Missed">
              {appointment.startUtc
                ? `${formatAppointmentTime(appointment.startUtc, appointment.endUtc)}${appointment.staffUsername ? ` with ${appointment.staffUsername}` : ''}`
                : 'Not booked'}
            </MetadataListItem>
            <MetadataListItem label="For">{appointmentPurpose(appointment)}</MetadataListItem>
            {recorded && <MetadataListItem label="Recorded so far">{recorded}</MetadataListItem>}
          </MetadataList>
          <RadioList
            label="Follow-up"
            value={decision}
            onChange={(value) => setDecision(value as DnaFollowUpDecision)}
            isRequired
            isDisabled={recordDnaFollowUp.isPending}
            status={fieldStatus(errors, 'decision')}>
            {DNA_FOLLOW_UPS.map((d) => (
              <RadioListItem key={d.value} value={d.value} label={d.label} />
            ))}
          </RadioList>
          <TextArea
            label="Detail"
            description="Who decided, and why"
            value={detail}
            onChange={setDetail}
            rows={3}
            maxLength={MAX_DNA_FOLLOW_UP_DETAIL}
            isRequired={needsDetail(decision)}
            isOptional={!needsDetail(decision)}
            isDisabled={recordDnaFollowUp.isPending}
            status={fieldStatus(errors, 'detail')}
          />
          {needsDetail(decision) && detail.trim() === '' && (
            <Text type="supporting" color="secondary">
              Say who decided there's to be no further appointment, and why.
            </Text>
          )}
        </VStack>
      )}
    </ConfirmDialog>
  );
}

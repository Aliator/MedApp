import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {patientOpenAppointmentCountQuery} from '../../api/appointments';
import type {PatientFlag} from '../../api/patients';
import {patientOpenPathwayCountQuery} from '../../api/pathways';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** A change to one of a patient's flags: which flag, and whether it's being set or cleared. */
export interface PatientFlagChange {
  flag: PatientFlag;
  set: boolean;
}

const TEXT = {
  'restriction-set': {
    title: 'Restrict Record',
    message:
      "While it's restricted (UK GDPR Article 18), nobody can change this patient, their pathways or their appointments. Staff see a banner with the reason.",
    confirm: 'Restrict Record',
    reasonLabel: 'Reason',
  },
  'restriction-clear': {
    title: 'Lift Restriction',
    message: 'Staff can change this patient, their pathways and their appointments again.',
    confirm: 'Lift Restriction',
    reasonLabel: 'Reason (optional)',
  },
  'dispute-set': {
    title: 'Mark as Disputed',
    message: 'Record that the patient disputes something in their record (Article 16). Staff see a banner with what they dispute.',
    confirm: 'Mark Disputed',
    reasonLabel: "What's disputed",
  },
  'dispute-clear': {
    title: 'Clear Dispute',
    message: 'The banner saying the patient disputes their record is removed.',
    confirm: 'Clear Dispute',
    reasonLabel: 'Reason (optional)',
  },
} as const;

export interface PatientFlagDialogProps {
  /** The patient, whose pathways under way and open appointments restricting them would stop. */
  patientId: string;
  /** The change being confirmed; null when closed. */
  change: PatientFlagChange | null;
  isProcessing: boolean;
  /** Why the last try failed: a 400's field errors, or a message. The dialog stays open, keeping what was typed. */
  errors?: ValidationErrors | null;
  message?: string | null;
  onConfirm: (reason: string, confirmOpenPathways: boolean) => void;
  onCancel: () => void;
}

/**
 * Confirms setting or clearing a patient's restricted or disputed flag (admins), with a reason, which setting one
 * needs. The activity log keeps the reason. Restricting a record with pathways under way or open appointments (Needs
 * booking or booked) says how many of each, and needs the admin to confirm that their work stops: pathways can still
 * be blocked or cancelled, with a reason, and appointments cancelled. If the API still asks (someone added one since
 * the counts were read), the checkbox shows its message. Give it a `key` per change so the reason starts empty.
 */
export function PatientFlagDialog({
  patientId,
  change,
  isProcessing,
  errors = null,
  message = null,
  onConfirm,
  onCancel,
}: PatientFlagDialogProps) {
  const [reason, setReason] = useState('');
  const [isConfirmed, setIsConfirmed] = useState(false);
  const text = TEXT[`${change?.flag ?? 'restriction'}-${change?.set === false ? 'clear' : 'set'}`];
  const needsReason = change?.set !== false;
  const isRestricting = change?.flag === 'restriction' && change.set;
  const underWay = useQuery({...patientOpenPathwayCountQuery(patientId), enabled: isRestricting});
  const openAppointments = useQuery({...patientOpenAppointmentCountQuery(patientId), enabled: isRestricting});
  const pathways = underWay.data ?? 0;
  const appointments = openAppointments.data ?? 0;
  const isCounting = isRestricting && (underWay.isPending || openAppointments.isPending);
  // The API asks for the confirmation itself when its counts differ from these: its message shows on the checkbox.
  const isAskedByApi = fieldStatus(errors, 'confirmOpenPathways') !== undefined;
  const needsConfirming = isRestricting && (pathways > 0 || appointments > 0 || isAskedByApi);
  const warning = freezeWarning(pathways, appointments);

  return (
    <ConfirmDialog
      isOpen={change !== null}
      title={text.title}
      message={text.message}
      icon={change?.flag === 'dispute' ? AppIcon.Info : AppIcon.Lock}
      intent="warning"
      confirmText={text.confirm}
      confirmDisabled={(needsReason && !reason.trim()) || isCounting || (needsConfirming && !isConfirmed)}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm(reason.trim(), needsConfirming && isConfirmed)}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {message && <AlertMessage message={message} />}
        {isCounting && (
          <Text type="body" color="secondary">
            Checking for pathways under way and open appointments...
          </Text>
        )}
        {needsConfirming && (
          <VStack gap={2} hAlign="stretch">
            {warning && <AlertMessage status="warning" message={warning} />}
            <CheckboxInput
              label={`I understand that work on ${whatStops(pathways, appointments)} stops`}
              value={isConfirmed}
              onChange={setIsConfirmed}
              isDisabled={isProcessing}
              status={fieldStatus(errors, 'confirmOpenPathways')}
            />
          </VStack>
        )}
        <TextArea
          label={text.reasonLabel}
          value={reason}
          onChange={setReason}
          rows={3}
          isRequired={needsReason}
          isDisabled={isProcessing}
          status={fieldStatus(errors, 'reason')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

/** "1 pathway is under way", "2 appointments are open". */
function counted(count: number, noun: string, state: string): string {
  return `${count} ${noun}${count === 1 ? ' is' : 's are'} ${state}`;
}

/** What restricting the record stops, counted; null when nothing is under way or open. */
function freezeWarning(pathways: number, appointments: number): string | null {
  if (pathways > 0 && appointments > 0) {
    return `${counted(pathways, 'pathway', 'under way')} and ${counted(appointments, 'appointment', 'open')}. While the record is restricted, work on them stops: the pathways can still be blocked or cancelled, with a reason, and the appointments cancelled, but nothing else.`;
  }
  if (pathways > 0) {
    const [them, they] = pathways === 1 ? ['it', 'it'] : ['them', 'they'];
    return `${counted(pathways, 'pathway', 'under way')}. While the record is restricted, work on ${them} stops: ${they} can still be blocked or cancelled, with a reason, but nothing else.`;
  }
  if (appointments > 0) {
    const they = appointments === 1 ? 'it' : 'they';
    return `${counted(appointments, 'appointment', 'open')}. While the record is restricted, ${they} can't be booked, moved or given an outcome: ${they} can still be cancelled, but nothing else.`;
  }
  return null;
}

/** The checkbox's words for what stops: "this pathway", "these 2 appointments", "the 1 pathway and 2 appointments". */
function whatStops(pathways: number, appointments: number): string {
  const plural = (count: number, noun: string) => `${count} ${noun}${count === 1 ? '' : 's'}`;
  if (pathways > 0 && appointments > 0) return `the ${plural(pathways, 'pathway')} and ${plural(appointments, 'appointment')}`;
  if (pathways > 0) return pathways === 1 ? 'this pathway' : `these ${pathways} pathways`;
  if (appointments > 0) return appointments === 1 ? 'this appointment' : `these ${appointments} appointments`;
  return "this patient's pathways and appointments";
}

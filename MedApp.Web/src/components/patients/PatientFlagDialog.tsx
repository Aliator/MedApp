import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import type {PatientFlag} from '../../api/patients';
import {patientOpenProcedureCountQuery} from '../../api/procedures';
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
      "While it's restricted (UK GDPR Article 18), nobody can change this patient or their procedures. Staff see a banner with the reason.",
    confirm: 'Restrict Record',
    reasonLabel: 'Reason',
  },
  'restriction-clear': {
    title: 'Lift Restriction',
    message: 'Staff can change this patient and their procedures again.',
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
  /** The patient, whose procedures under way restricting them would stop. */
  patientId: string;
  /** The change being confirmed; null when closed. */
  change: PatientFlagChange | null;
  isProcessing: boolean;
  /** Why the last try failed: a 400's field errors, or a message. The dialog stays open, keeping what was typed. */
  errors?: ValidationErrors | null;
  message?: string | null;
  onConfirm: (reason: string, confirmOpenProcedures: boolean) => void;
  onCancel: () => void;
}

/**
 * Confirms setting or clearing a patient's restricted or disputed flag (admins), with a reason, which setting one
 * needs. The activity log keeps the reason. Restricting a record whose procedures are under way says how many, and
 * needs the admin to confirm that their work stops (they can still be blocked or cancelled, with a reason). Give it a
 * `key` per change so the reason starts empty.
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
  const underWay = useQuery({...patientOpenProcedureCountQuery(patientId), enabled: isRestricting});
  const count = underWay.data ?? 0;
  const needsConfirming = isRestricting && count > 0;
  const them = count === 1 ? 'it' : 'them';
  const they = count === 1 ? 'it' : 'they';

  return (
    <ConfirmDialog
      isOpen={change !== null}
      title={text.title}
      message={text.message}
      icon={change?.flag === 'dispute' ? AppIcon.Info : AppIcon.Lock}
      intent="warning"
      confirmText={text.confirm}
      confirmDisabled={(needsReason && !reason.trim()) || (isRestricting && underWay.isPending) || (needsConfirming && !isConfirmed)}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm(reason.trim(), needsConfirming && isConfirmed)}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {message && <AlertMessage message={message} />}
        {isRestricting && underWay.isPending && (
          <Text type="body" color="secondary">
            Checking for procedures under way...
          </Text>
        )}
        {needsConfirming && (
          <VStack gap={2} hAlign="stretch">
            <AlertMessage
              status="warning"
              message={`${count} ${count === 1 ? 'procedure is' : 'procedures are'} under way. While the record is restricted, work on ${them} stops: ${they} can still be blocked or cancelled, with a reason, but nothing else.`}
            />
            <CheckboxInput
              label={`I understand that work on ${count === 1 ? 'this procedure' : `these ${count} procedures`} stops`}
              value={isConfirmed}
              onChange={setIsConfirmed}
              isDisabled={isProcessing}
              status={fieldStatus(errors, 'confirmOpenProcedures')}
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

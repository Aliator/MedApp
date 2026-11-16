import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import type {PatientFlag} from '../../api/patients';
import {ConfirmDialog} from '../modals/ConfirmDialog';
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
  /** The change being confirmed; null when closed. */
  change: PatientFlagChange | null;
  isProcessing: boolean;
  errors?: ValidationErrors | null;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

/**
 * Confirms setting or clearing a patient's restricted or disputed flag (admins), with a reason, which setting one
 * needs. The activity log keeps the reason. Give it a `key` per change so the reason starts empty.
 */
export function PatientFlagDialog({change, isProcessing, errors = null, onConfirm, onCancel}: PatientFlagDialogProps) {
  const [reason, setReason] = useState('');
  const text = TEXT[`${change?.flag ?? 'restriction'}-${change?.set === false ? 'clear' : 'set'}`];
  const needsReason = change?.set !== false;

  return (
    <ConfirmDialog
      isOpen={change !== null}
      title={text.title}
      message={text.message}
      icon={change?.flag === 'dispute' ? AppIcon.Info : AppIcon.Lock}
      intent="warning"
      confirmText={text.confirm}
      confirmDisabled={needsReason && !reason.trim()}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm(reason.trim())}
      onCancel={onCancel}>
      <TextArea
        label={text.reasonLabel}
        value={reason}
        onChange={setReason}
        rows={3}
        isRequired={needsReason}
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'reason')}
      />
    </ConfirmDialog>
  );
}

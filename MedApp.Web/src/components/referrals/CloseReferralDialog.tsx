import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {MAX_CLOSE_DETAIL, type Referral} from '../../api/referrals';
import {formatDate} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Closing a referral that awaits triage: the referrer or patient withdrew it, or it was registered in error. */
export type ReferralClosing = 'withdraw' | 'enteredInError';

const WORDS: Record<ReferralClosing, {title: string; message: string; confirm: string; label: string; placeholder: string}> = {
  withdraw: {
    title: 'Withdraw Referral?',
    message: "The referrer or the patient has withdrawn it. It's closed and won't be triaged; it stays in the patient's record.",
    confirm: 'Withdraw Referral',
    label: 'Who withdrew it, and why',
    placeholder: 'e.g. The practice phoned: the patient is being seen elsewhere',
  },
  enteredInError: {
    title: 'Mark Entered in Error?',
    message:
      "It was registered on the wrong patient, or twice. It's closed and won't be triaged; it stays in the record, marked entered in error. This can't be undone.",
    confirm: 'Mark Entered in Error',
    label: 'What was wrong',
    placeholder: 'e.g. Registered twice: the same letter is on another referral',
  },
};

export interface CloseReferralDialogProps {
  /** Which closing, or null while the dialog is shut. */
  closing: ReferralClosing | null;
  referral: Referral;
  isProcessing: boolean;
  /** A 400's errors (on `detail`), shown on the field. */
  errors?: ValidationErrors | null;
  /** Why it failed otherwise (a 409 in the API's words), shown in the dialog. */
  message?: string | null;
  onConfirm: (detail: string) => void;
  onCancel: () => void;
}

/**
 * Withdrawing a referral, or marking it entered in error: what it means, the patient as `PatientBanner` shows them
 * (compact: the dialog covers the page's), the specialty and when it was received, so the wrong referral isn't closed
 * (hazard H-41), and the detail (required, 500 characters). Mount it afresh for each opening (a `key`), so the detail
 * starts empty.
 */
export function CloseReferralDialog({closing, referral, isProcessing, errors, message, onConfirm, onCancel}: CloseReferralDialogProps) {
  const [detail, setDetail] = useState('');
  const words = WORDS[closing ?? 'withdraw'];
  return (
    <ConfirmDialog
      isOpen={closing !== null}
      title={words.title}
      message={words.message}
      icon={closing === 'enteredInError' ? AppIcon.EnteredInError : AppIcon.Withdraw}
      intent="warning"
      isDanger={closing === 'enteredInError'}
      confirmText={words.confirm}
      confirmDisabled={!detail.trim()}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm(detail.trim())}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {referral.patient && <PatientBanner patient={referral.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Specialty">{referral.specialtyName}</MetadataListItem>
          <MetadataListItem label="Received">{referral.receivedOn ? formatDate(referral.receivedOn) : '—'}</MetadataListItem>
          <MetadataListItem label="Practice">{referral.referrerPractice}</MetadataListItem>
        </MetadataList>
        {message && <AlertMessage message={message} />}
        <TextArea
          label={words.label}
          value={detail}
          onChange={setDetail}
          placeholder={words.placeholder}
          rows={3}
          maxLength={MAX_CLOSE_DETAIL}
          isRequired
          isDisabled={isProcessing}
          status={fieldStatus(errors, 'detail')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

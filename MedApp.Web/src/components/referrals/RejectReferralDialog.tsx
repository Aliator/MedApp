import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  MAX_TRIAGE_DETAIL,
  REFERRAL_REJECTION_REASONS,
  rejectionNeedsDetail,
  rejectionReasonWords,
  useRejectReferral,
  type Referral,
} from '../../api/referrals';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {AppIcon} from '../shared/icons';
import {TriageDialog, type TriageStep} from './TriageDialog';

const FIELDS = ['reason', 'detail'] as const;

export interface RejectReferralDialogProps {
  isOpen: boolean;
  referral: Referral;
  onCancel: () => void;
  /** Called with the referral, now rejected. */
  onRejected: (referral: Referral) => void;
}

/**
 * Rejecting a referral (decision 20): a reason from the API's fixed choices (nothing chosen to start with) and a
 * detail (500 characters; required for Other). Asking the referrer for more is a rejection with Insufficient
 * information for now. Then Check and Confirm shows the patient, the team and the reason before it's sent
 * (hazard H-41). A 400 goes back to the form with the errors on their fields; a 403 or 409 shows the API's words, and
 * the referral is read again. Mount it afresh for each opening (a `key`), so it starts empty.
 */
export function RejectReferralDialog({isOpen, referral, onCancel, onRejected}: RejectReferralDialogProps) {
  const reject = useRejectReferral(referral.id ?? '');
  const [step, setStep] = useState<TriageStep>('form');
  const [reason, setReason] = useState('');
  const [detail, setDetail] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const needsDetail = rejectionNeedsDetail(reason);
  const isTooLong = detail.length > MAX_TRIAGE_DETAIL;
  const canContinue = reason !== '' && !isTooLong && (!needsDetail || detail.trim() !== '');

  const confirm = () => {
    setMessage(null);
    reject.mutate(
      {reason, detail, expectedVersion: referral.version ?? 0},
      {
        onSuccess: (rejected) => onRejected(rejected),
        onError: (e) => {
          if (e instanceof ApiValidationError) {
            setErrors(e.errors);
            setStep('form');
            return;
          }
          setMessage(problemMessage(e, 'The referral could not be rejected. Please try again.'));
        },
      },
    );
  };

  return (
    <TriageDialog
      isOpen={isOpen}
      title="Reject Referral"
      icon={AppIcon.StatusCancelled}
      referral={referral}
      step={step}
      canContinue={canContinue}
      isProcessing={reject.isPending}
      outcome="Rejecting closes the referral; it stays in the patient's record. Whoever registered it is told, so the referrer can be written to."
      errors={errors}
      fields={FIELDS}
      message={message}
      onContinue={() => {
        setMessage(null);
        setStep('check');
      }}
      onBack={() => setStep('form')}
      onConfirm={confirm}
      onCancel={onCancel}
      summary={
        <>
          <MetadataListItem label="Reason">{rejectionReasonWords(reason)}</MetadataListItem>
          <MetadataListItem label="Detail">{detail.trim() || 'None'}</MetadataListItem>
        </>
      }>
      <VStack gap={4} hAlign="stretch">
        <AnchoredSelector
          label="Reason"
          placeholder="Choose a reason"
          options={REFERRAL_REJECTION_REASONS.map((r) => ({value: r.value, label: r.label}))}
          value={reason || undefined}
          onChange={(value: string) => setReason(value)}
          isRequired
          status={fieldStatus(errors, 'reason')}
        />
        <TextArea
          label={needsDetail ? 'Detail' : 'Detail (optional)'}
          description="Why, for the record and for whoever writes to the referrer."
          value={detail}
          onChange={setDetail}
          rows={3}
          isRequired={needsDetail}
          status={
            fieldStatus(errors, 'detail') ?? (isTooLong ? {type: 'error', message: `At most ${MAX_TRIAGE_DETAIL} characters.`} : undefined)
          }
        />
      </VStack>
    </TriageDialog>
  );
}

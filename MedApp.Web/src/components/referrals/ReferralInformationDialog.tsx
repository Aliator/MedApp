import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {useReferralInformationActions, type Referral} from '../../api/referrals';
import {formatDate} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Which of the two: asking the referrer for more, or recording that it came. */
export type ReferralInformationStep = 'ask' | 'received';

const WORDS: Record<ReferralInformationStep, {title: string; message: string; confirm: string}> = {
  ask: {
    title: 'Ask for More Information?',
    message:
      "The referral waits, still open, in Information requested until what you asked for arrives. Its clock keeps running. Write the request to the referrer as a letter (Request for more information) from this page. You can still accept, reject or redirect it meanwhile.",
    confirm: 'Ask for More Information',
  },
  received: {
    title: 'Record Information Received?',
    message: 'The referral goes back to Awaiting triage, to be accepted, rejected or redirected. Attach what arrived to it.',
    confirm: 'Information Received',
  },
};

export interface ReferralInformationDialogProps {
  step: ReferralInformationStep;
  referral: Referral;
  onCancel: () => void;
  /** It's done: the referral as the API returned it. */
  onDone: (referral: Referral) => void;
}

/**
 * Asking the referrer for more information (an Awaiting triage referral becomes Information requested) or recording
 * that it came (back to Awaiting triage), decision 106. Like every triage step it shows the patient as `PatientBanner`
 * does (compact: the dialog covers the page's), the specialty and when it was received, before anything is sent, so the
 * wrong referral isn't changed (hazard H-41). A refusal (a 403, or a 409: changed meanwhile, the record restricted)
 * stays in the dialog in the API's words, and the referral is read again. Mount it only while open.
 */
export function ReferralInformationDialog({step, referral, onCancel, onDone}: ReferralInformationDialogProps) {
  const actions = useReferralInformationActions(referral.id ?? '');
  const mutation = step === 'ask' ? actions.askForInformation : actions.informationReceived;
  const [error, setError] = useState<string | null>(null);
  const words = WORDS[step];
  return (
    <ConfirmDialog
      isOpen
      title={words.title}
      message={words.message}
      icon={step === 'ask' ? AppIcon.Info : AppIcon.Success}
      confirmText={words.confirm}
      isProcessing={mutation.isPending}
      onConfirm={() => {
        setError(null);
        mutation.mutate(
          {expectedVersion: referral.version ?? 0},
          {onSuccess: onDone, onError: (e) => setError(problemMessage(e, 'The referral could not be changed. Please try again.'))},
        );
      }}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {referral.patient && <PatientBanner patient={referral.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Specialty">{referral.specialtyName}</MetadataListItem>
          <MetadataListItem label="Received">{referral.receivedOn ? formatDate(referral.receivedOn) : '—'}</MetadataListItem>
          <MetadataListItem label="Practice">{referral.referrerPractice}</MetadataListItem>
        </MetadataList>
        {error && <AlertMessage message={error} />}
      </VStack>
    </ConfirmDialog>
  );
}

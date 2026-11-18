import {Button} from '@astryxdesign/core/Button';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import type {ValidationErrors} from '../../api/errors';
import type {Referral} from '../../api/referrals';
import {formatDate} from '../../utils/dates';
import {DialogFrame} from '../modals/DialogFrame';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Where a triage dialog is: the triager's choices, or checking them before they're sent. */
export type TriageStep = 'form' | 'check';

export interface TriageDialogProps {
  isOpen: boolean;
  /** The decision, as the form step's title and the confirm button's words: "Accept Referral". */
  title: string;
  icon: IconType;
  referral: Referral;
  step: TriageStep;
  /** Whether everything the decision needs has been chosen, so Continue can be pressed. */
  canContinue: boolean;
  isProcessing: boolean;
  /** What the check step says the decision does. */
  outcome: string;
  /** The choices, as rows of the check step's list (`MetadataListItem`s), after the specialty and received date. */
  summary: ReactNode;
  /** A 400's errors: the ones not on a field of the form are listed here (`fields` are shown on their fields). */
  errors?: ValidationErrors | null;
  fields?: readonly string[];
  /** Why the decision failed otherwise (a 403 or 409 in the API's words). */
  message?: string | null;
  onContinue: () => void;
  onBack: () => void;
  onConfirm: () => void;
  onCancel: () => void;
  /** The form step's fields. */
  children: ReactNode;
}

/**
 * The frame of every triage decision (accept, reject, redirect): a form step, then "Check and Confirm", which shows
 * the patient exactly as `PatientBanner` does (compact: the dialog covers the page's), the referral's specialty and
 * received date, and every choice made, before anything is sent (hazard H-41). Back returns to the form with the
 * choices kept; Confirm sends. The patient and the referral show on the form step too, so the triager knows which
 * referral they're deciding from the start.
 *
 * **The clinician decides; the app records:** nothing in a triage form is filled in or suggested. Use it for any new
 * triage outcome, so each asks the same way.
 */
export function TriageDialog({
  isOpen,
  title,
  icon,
  referral,
  step,
  canContinue,
  isProcessing,
  outcome,
  summary,
  errors,
  fields,
  message,
  onContinue,
  onBack,
  onConfirm,
  onCancel,
  children,
}: TriageDialogProps) {
  const isChecking = step === 'check';
  return (
    <DialogFrame
      isOpen={isOpen}
      title={isChecking ? 'Check and Confirm' : title}
      icon={<Icon icon={isChecking ? AppIcon.Check : icon} size="lg" color="accent" />}
      width={560}
      onClose={onCancel}
      purpose={isProcessing ? 'required' : 'form'}
      footer={
        isChecking ? (
          <>
            <Button label="Back" variant="secondary" icon={<Icon icon={AppIcon.Back} />} isDisabled={isProcessing} onClick={onBack} />
            <Button label={title} variant="primary" isLoading={isProcessing} onClick={onConfirm} />
          </>
        ) : (
          <>
            <Button label="Cancel" variant="secondary" onClick={onCancel} />
            <Button label="Continue" variant="primary" isDisabled={!canContinue} onClick={onContinue} />
          </>
        )
      }>
      <VStack gap={4} hAlign="stretch">
        {referral.patient && <PatientBanner patient={referral.patient} isCompact />}
        {isChecking && (
          <Text type="body" color="secondary">
            {`Check this is the right patient and referral, and that this is your decision. ${outcome}`}
          </Text>
        )}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Specialty">{referral.specialtyName}</MetadataListItem>
          <MetadataListItem label="Received">{referral.receivedOn ? formatDate(referral.receivedOn) : '—'}</MetadataListItem>
          {isChecking && summary}
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={fields} />}
        {message && <AlertMessage message={message} />}
        {!isChecking && children}
      </VStack>
    </DialogFrame>
  );
}

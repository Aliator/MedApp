import type {IconType} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  MAX_CLOSE_DETAIL,
  REJECTION_REASONS,
  rejectionReasonLabel,
  specimenStatusLabel,
  useSpecimenActions,
  type Specimen,
} from '../../api/specimens';
import {formatDateTime} from '../../utils/dates';
import {ukDateTime, ukDateTimeToUtc, type UkDateTime} from '../../utils/specimenTimes';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {StepTimeFields} from './StepTimeFields';

/** A step that moves a specimen on, or closes it. */
export type SpecimenStep = 'sent' | 'received' | 'rejected' | 'lost' | 'enteredInError';

interface StepWords {
  title: string;
  message: string;
  confirm: string;
  icon: IconType;
  announced: string;
  failed: string;
}

const WORDS: Record<SpecimenStep, StepWords> = {
  sent: {
    title: 'Mark Sent',
    message: "It's on its way to the lab. The lab's worklist expects it until it's marked received.",
    confirm: 'Mark Sent',
    icon: AppIcon.SpecimenSent,
    announced: 'Sample marked sent',
    failed: 'The sample could not be marked sent.',
  },
  received: {
    title: 'Mark Received',
    message: 'The lab has it. It stays in the lab until its report is attached.',
    confirm: 'Mark Received',
    icon: AppIcon.SpecimenReceived,
    announced: 'Sample marked received',
    failed: 'The sample could not be marked received.',
  },
  rejected: {
    title: 'Reject Sample',
    message: "The lab can't test it. It's closed, and whoever asked for it and took it are told; taking another is a new sample.",
    confirm: 'Reject Sample',
    icon: AppIcon.StatusCancelled,
    announced: 'Sample rejected',
    failed: 'The sample could not be rejected.',
  },
  lost: {
    title: 'Mark Lost',
    message: "It can't be found. It's closed, and whoever asked for it and took it are told; taking another is a new sample.",
    confirm: 'Mark Lost',
    icon: AppIcon.Warning,
    announced: 'Sample marked lost',
    failed: 'The sample could not be marked lost.',
  },
  enteredInError: {
    title: 'Mark Entered in Error?',
    message:
      "It was recorded by mistake, or on the wrong patient. It's closed and stays in the record, marked entered in error. This can't be undone.",
    confirm: 'Mark Entered in Error',
    icon: AppIcon.EnteredInError,
    announced: 'Sample marked entered in error',
    failed: 'The sample could not be marked entered in error.',
  },
};

/** The fields each step's 400 can name, each shown on its field. */
const FIELDS = ['atUtc', 'reference', 'reason', 'detail'];

export interface SpecimenStepDialogProps {
  /** Which step, or null while the dialog is shut. */
  step: SpecimenStep | null;
  specimen: Specimen;
  onClose: () => void;
}

/**
 * Moving a specimen on or closing it, from the version the page shows: Mark Sent (when, and the courier's or batch's
 * reference), Mark Received (when, and the lab's own reference), Reject Specimen (a reason, none chosen to start with,
 * and a detail, required for Other), Mark Lost and Entered in Error (what happened). Each shows the patient as
 * `PatientBanner` does and the specimen's number, type and lab, so the right one is changed (hazard H-47). A 400 shows on
 * its fields; a 409 (changed meanwhile, or the record restricted) or 403 in the API's words, after which the specimen is
 * read again. Mount it afresh for each opening (a `key`), so it starts empty and at now.
 */
export function SpecimenStepDialog({step, specimen, onClose}: SpecimenStepDialogProps) {
  const announce = useAnnounce();
  const actions = useSpecimenActions(specimen.id ?? '');
  const [initialAt] = useState<UkDateTime>(() => ukDateTime());
  const [at, setAt] = useState<UkDateTime>(initialAt);
  const [reference, setReference] = useState('');
  const [reason, setReason] = useState('');
  const [detail, setDetail] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const words = WORDS[step ?? 'sent'];
  const isTimed = step === 'sent' || step === 'received';
  const needsDetail = step === 'lost' || step === 'enteredInError' || (step === 'rejected' && reason === 'Other');
  const mutation = step ? actions[step] : actions.sent;

  const isComplete = isTimed
    ? Boolean(at.date && at.time)
    : (step !== 'rejected' || reason !== '') && (!needsDetail || detail.trim() !== '');

  const confirm = () => {
    if (!step) return;
    setErrors(null);
    setMessage(null);
    const expectedVersion = specimen.version ?? 0;
    const callbacks = {
      onSuccess: () => {
        announce(words.announced);
        onClose();
      },
      onError: (e: unknown) => {
        if (e instanceof ApiValidationError) setErrors(e.errors);
        else setMessage(problemMessage(e, words.failed));
      },
    };
    // Left at the time the dialog opened, it's sent as now (the API's default), so it's never a moment in the future.
    const atUtc = at.date === initialAt.date && at.time === initialAt.time ? undefined : (ukDateTimeToUtc(at) ?? undefined);
    if (step === 'sent' || step === 'received') actions[step].mutate({atUtc, reference, expectedVersion}, callbacks);
    else if (step === 'rejected') actions.rejected.mutate({reason, detail, expectedVersion}, callbacks);
    else actions[step].mutate({detail, expectedVersion}, callbacks);
  };

  return (
    <ConfirmDialog
      isOpen={step !== null}
      title={words.title}
      message={words.message}
      icon={words.icon}
      intent={isTimed ? 'accent' : 'warning'}
      isDanger={step === 'enteredInError'}
      confirmText={words.confirm}
      confirmDisabled={!isComplete}
      isProcessing={mutation.isPending}
      hasSaveShortcut={isTimed}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {specimen.patient && <PatientBanner patient={specimen.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Sample">{specimen.number}</MetadataListItem>
          <MetadataListItem label="Status">{specimenStatusLabel(specimen.status)}</MetadataListItem>
          <MetadataListItem label="Type">{specimen.specimenTypeName}</MetadataListItem>
          <MetadataListItem label="Taken">{formatDateTime(specimen.takenAtUtc ?? '')}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage message={message} />}
        {isTimed && (
          <>
            <StepTimeFields step={step === 'sent' ? 'Sent' : 'Received'} value={at} onChange={setAt} status={fieldStatus(errors, 'atUtc')} />
            <TextInput
              label={step === 'sent' ? 'Courier or batch reference' : "Lab's reference"}
              description={step === 'sent' ? "The courier's tracking number, or the batch it went in." : 'The number the lab gave it.'}
              value={reference}
              onChange={setReference}
              isOptional
              autoComplete="off"
              status={fieldStatus(errors, 'reference')}
            />
          </>
        )}
        {step === 'rejected' && (
          <RadioList label="Why the lab rejected it" value={reason} onChange={setReason} isRequired status={fieldStatus(errors, 'reason')}>
            {REJECTION_REASONS.map((r) => (
              <RadioListItem key={r} value={r} label={rejectionReasonLabel(r)} />
            ))}
          </RadioList>
        )}
        {!isTimed && step !== null && (
          <TextArea
            label={step === 'rejected' ? (needsDetail ? 'Detail' : 'Detail (optional)') : 'What happened'}
            description="Part of the patient's clinical record."
            value={detail}
            onChange={setDetail}
            rows={3}
            maxLength={MAX_CLOSE_DETAIL}
            isRequired={needsDetail}
            status={fieldStatus(errors, 'detail')}
          />
        )}
      </VStack>
    </ConfirmDialog>
  );
}

import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {FileInput} from '@astryxdesign/core/FileInput';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {allowedTypesText, checkAttachmentFile, DEFAULT_ATTACHMENT_LIMITS} from '../../api/attachments';
import {consentFormLabel, MAX_CONSENT_DETAIL, signedByLabel, useConsentActions, type ConsentRecord} from '../../api/consent';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {formatDate, todayIso, WEEK_STARTS_ON} from '../../utils/dates';
import {formatFileSize} from '../../utils/fileSize';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** What the dialog does to a consent record. */
export type ConsentAction = 'withdraw' | 'enteredInError' | 'attachScan';

const WORDS: Record<ConsentAction, {title: string; message: string; confirm: string; announced: string; failed: string; notDone: string}> = {
  withdraw: {
    title: 'Withdraw Consent',
    message: "Record that consent was withdrawn. It stays in the record, marked withdrawn; a stage that needs consent can't be finished without new consent.",
    confirm: 'Withdraw Consent',
    announced: 'Consent withdrawn',
    failed: 'The withdrawal could not be recorded.',
    notDone: 'Withdrawal not recorded',
  },
  enteredInError: {
    title: 'Mark Entered in Error?',
    message: "It was recorded by mistake: the wrong patient, stage or form. It stays in the record, marked entered in error, and its scan is deleted. This can't be undone.",
    confirm: 'Mark Entered in Error',
    announced: 'Consent record marked entered in error',
    failed: 'The consent record could not be marked entered in error.',
    notDone: 'Not marked entered in error',
  },
  attachScan: {
    title: 'Add Scan',
    message: 'The scanned, signed form. It is attached to the pathway as a file.',
    confirm: 'Add Scan',
    announced: 'Scan added',
    failed: 'The scan could not be added.',
    notDone: 'Scan not added',
  },
};

const FIELDS = ['withdrawnOn', 'detail', 'attachmentId'];

export interface ConsentActionDialogProps {
  /** Which action, or null while the dialog is shut. */
  action: ConsentAction | null;
  record: ConsentRecord;
  patient?: BannerPatient | null;
  onClose: () => void;
}

/**
 * Withdrawing consent (when, and what was said), marking a record entered in error (what was wrong), or adding its scan,
 * from the version the page shows. The patient and the record show, so the right one is changed (hazard H-59). A 400
 * shows on its fields; a 409 or 403 in the API's words. Mount it afresh for each opening.
 */
export function ConsentActionDialog({action, record, patient, onClose}: ConsentActionDialogProps) {
  const announce = useAnnounce();
  const actions = useConsentActions(record);
  const [withdrawnOn, setWithdrawnOn] = useState<string>(todayIso());
  const [detail, setDetail] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const words = WORDS[action ?? 'withdraw'];
  const mutation = actions[action ?? 'withdraw'];
  const fileProblem = file ? checkAttachmentFile(file, DEFAULT_ATTACHMENT_LIMITS) : null;
  const isComplete =
    action === 'attachScan' ? file !== null && fileProblem === null : detail.trim() !== '' && (action !== 'withdraw' || withdrawnOn !== '');

  const confirm = () => {
    if (!action) return;
    setErrors(null);
    setMessage(null);
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
    if (action === 'withdraw') actions.withdraw.mutate({withdrawnOn, detail}, callbacks);
    else if (action === 'enteredInError') actions.enteredInError.mutate({detail}, callbacks);
    else if (file) actions.attachScan.mutate({file}, callbacks);
  };

  return (
    <ConfirmDialog
      isOpen={action !== null}
      title={words.title}
      message={words.message}
      icon={action === 'attachScan' ? AppIcon.Upload : action === 'enteredInError' ? AppIcon.EnteredInError : AppIcon.Withdraw}
      intent={action === 'attachScan' ? 'accent' : 'warning'}
      isDanger={action === 'enteredInError'}
      confirmText={words.confirm}
      confirmDisabled={!isComplete}
      isProcessing={mutation.isPending}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {patient && <PatientBanner patient={patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Stage">{record.stageName}</MetadataListItem>
          <MetadataListItem label="Form">{consentFormLabel(record.formName, record.formVersion)}</MetadataListItem>
          <MetadataListItem label="Signed on">{formatDate(record.signedOn ?? '')}</MetadataListItem>
          <MetadataListItem label="Signed by">{signedByLabel(record.signedBy)}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage label={words.notDone} message={message} />}
        {action === 'withdraw' && (
          <DateInput
            label="Withdrawn on"
            value={withdrawnOn as ISODateString}
            onChange={(value) => setWithdrawnOn(value ?? '')}
            weekStartsOn={WEEK_STARTS_ON}
            format="date_weekday"
            isRequired
            status={fieldStatus(errors, 'withdrawnOn')}
          />
        )}
        {action !== 'attachScan' && action !== null && (
          <TextArea
            label={action === 'withdraw' ? 'What was said' : 'What was wrong'}
            description="Part of the patient's clinical record."
            value={detail}
            onChange={setDetail}
            rows={3}
            maxLength={MAX_CONSENT_DETAIL}
            isRequired
            status={fieldStatus(errors, 'detail')}
          />
        )}
        {action === 'attachScan' && (
          <FileInput
            label="Scanned form"
            value={file}
            onChange={(picked) => setFile([picked].flat()[0] ?? null)}
            placeholder="Choose the signed form's scan"
            description={`${allowedTypesText(DEFAULT_ATTACHMENT_LIMITS)}, up to ${formatFileSize(DEFAULT_ATTACHMENT_LIMITS.maxFileBytes)}.`}
            status={fileProblem ? {type: 'error', message: fileProblem} : fieldStatus(errors, 'attachmentId')}
            isDisabled={mutation.isPending}
          />
        )}
      </VStack>
    </ConfirmDialog>
  );
}

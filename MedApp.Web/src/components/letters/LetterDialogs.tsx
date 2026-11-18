import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {List, ListItem} from '@astryxdesign/core/List';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery, type UseMutationResult} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiValidationError, fieldStatus, otherMessages, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  letterKindWords,
  MAX_LETTER_COMMENT,
  RECORD_SENT_DAYS_BACK,
  RECORDED_METHODS,
  recipientAddressLines,
  recipientKindWords,
  useLetterActions,
  type Letter,
  type LetterRecipient,
} from '../../api/letters';
import {formatDate, ukDay} from '../../utils/dates';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog, type ConfirmIntent} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import type {IconType} from '@astryxdesign/core/Icon';
import {recipientRole} from './RecipientsEditor';

/** What every letter dialog is told: the letter as the page shows it, and what to do when it's closed or done. */
export interface LetterDialogProps {
  letter: Letter;
  onCancel: () => void;
  /** Done: the letter as the API returned it. */
  onDone: (letter: Letter) => void;
}

interface FrameProps {
  letter: Letter;
  title: string;
  message: ReactNode;
  icon: IconType;
  intent?: ConfirmIntent;
  isDanger?: boolean;
  confirmText: string;
  confirmDisabled?: boolean;
  isProcessing: boolean;
  error: string | null;
  onConfirm: () => void;
  onCancel: () => void;
  children?: ReactNode;
}

/**
 * The frame of every letter dialog: what it does, then the patient as `PatientBanner` shows them (compact: the dialog
 * covers the page's) and what the letter is, so the wrong letter isn't changed, then the dialog's own fields, then a
 * refusal in the API's words. Each dialog is mounted only while open, so its fields start empty.
 */
function LetterDialogFrame({letter, title, message, icon, intent, isDanger, confirmText, confirmDisabled, isProcessing, error, onConfirm, onCancel, children}: FrameProps) {
  return (
    <ConfirmDialog
      isOpen
      title={title}
      message={message}
      icon={icon}
      intent={intent}
      isDanger={isDanger}
      confirmText={confirmText}
      confirmDisabled={confirmDisabled}
      isProcessing={isProcessing}
      onConfirm={onConfirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {letter.patient && <PatientBanner patient={letter.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Letter">{letterKindWords(letter.kind)}</MetadataListItem>
          <MetadataListItem label="Template">{letter.templateName}</MetadataListItem>
        </MetadataList>
        {children}
        {error && <AlertMessage message={error} />}
      </VStack>
    </ConfirmDialog>
  );
}

/**
 * Runs a dialog's change: a 400 shows on its fields (and any other messages above them), anything else in the API's
 * words in the dialog, which stays open (the letter is read again after a 409).
 */
function useDialogChange<T>(
  mutation: UseMutationResult<Letter, unknown, T>,
  onDone: (letter: Letter) => void,
  fallback: string,
  fields: readonly string[] = [],
) {
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [error, setError] = useState<string | null>(null);
  const run = (variables: T) => {
    setErrors(null);
    setError(null);
    mutation.mutate(variables, {
      onSuccess: onDone,
      onError: (e) => {
        if (e instanceof ApiValidationError) {
          setErrors(e.errors);
          setError(otherMessages(e.errors, fields).join(' ') || null);
        } else setError(problemMessage(e, fallback));
      },
    });
  };
  return {run, errors, error, isProcessing: mutation.isPending};
}

/** A list of recipients with their addresses: who it's addressed to, then the copies. */
function RecipientAddresses({recipients, label}: {recipients: readonly LetterRecipient[]; label: string}) {
  return (
    <List edge="flush" density="compact" hasDividers aria-label={label}>
      {recipients.map((recipient, index) => (
        <ListItem
          key={recipient.id}
          label={
            <Text type="body" weight="semibold">
              {`${recipientRole(index)}: ${recipient.name || recipientKindWords(recipient.kind)}`}
            </Text>
          }
          description={
            <VStack gap={0}>
              {[...recipientAddressLines(recipient), recipient.email ?? ''].filter(Boolean).map((line, i) => (
                <Text key={i} type="body">
                  {line}
                </Text>
              ))}
            </VStack>
          }
        />
      ))}
    </List>
  );
}

/**
 * Submitting a draft for approval (decision 102): to a named holder of ApproveLetters, never its author. Nothing is
 * chosen to begin with.
 */
export function SubmitLetterDialog({letter, onCancel, onDone}: LetterDialogProps) {
  const approvers = useQuery(staffWithPermissionQuery('ApproveLetters'));
  const options = (approvers.data ?? [])
    .filter((person) => person.userId !== letter.authorUserId)
    .map((person) => ({value: person.userId ?? '', label: person.username ?? ''}));
  const [approverUserId, setApproverUserId] = useState('');
  const change = useDialogChange(useLetterActions(letter.id ?? '').submit, onDone, 'The letter could not be submitted. Please try again.', [
    'approverUserId',
  ]);
  return (
    <LetterDialogFrame
      letter={letter}
      title="Submit for Approval"
      message="Choose who approves it. They're told, and it can't be edited while it waits; they can approve it or return it to you with a comment."
      icon={AppIcon.Send}
      confirmText="Submit"
      confirmDisabled={!approverUserId}
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({approverUserId, expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}>
      <AnchoredSelector
        label="Approver"
        placeholder={approvers.isPending ? 'Loading...' : 'Choose who approves it'}
        options={options}
        value={approverUserId || undefined}
        onChange={(value: string) => setApproverUserId(value)}
        isRequired
        isDisabled={approvers.isPending || change.isProcessing}
        status={fieldStatus(change.errors, 'approverUserId')}
      />
      {approvers.isSuccess && options.length === 0 && (
        <Text type="body" color="secondary">
          Nobody else can approve letters. Ask an administrator to give someone the permission.
        </Text>
      )}
    </LetterDialogFrame>
  );
}

/**
 * Approving (decision 102; hazard H-48): the patient, and every recipient with the address it will go to, are shown
 * before it's approved. Approving makes the PDF and freezes the letter.
 */
export function ApproveLetterDialog({letter, onCancel, onDone}: LetterDialogProps) {
  const change = useDialogChange(useLetterActions(letter.id ?? '').approve, onDone, 'The letter could not be approved. Please try again.');
  return (
    <LetterDialogFrame
      letter={letter}
      title="Approve Letter?"
      message="Check it's about the right patient and goes to the right people. Approving makes the PDF, with the patient's details on every page, and the letter can't be changed after."
      icon={AppIcon.Success}
      confirmText="Approve"
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}>
      <RecipientAddresses recipients={letter.recipients ?? []} label="Recipients" />
    </LetterDialogFrame>
  );
}

/** Returning a letter to its author with a comment (required, 500 characters). */
export function ReturnLetterDialog({letter, onCancel, onDone}: LetterDialogProps) {
  const [comment, setComment] = useState('');
  const change = useDialogChange(useLetterActions(letter.id ?? '').returnToAuthor, onDone, 'The letter could not be returned. Please try again.', [
    'comment',
  ]);
  return (
    <LetterDialogFrame
      letter={letter}
      title="Return to Author?"
      message={`It goes back to ${letter.authorUsername} as a draft, with your comment, for them to change and submit again.`}
      icon={AppIcon.Back}
      intent="warning"
      confirmText="Return"
      confirmDisabled={!comment.trim()}
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({comment: comment.trim(), expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}>
      <TextArea
        label="What needs changing"
        value={comment}
        onChange={setComment}
        rows={3}
        maxLength={MAX_LETTER_COMMENT}
        isRequired
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'comment')}
      />
    </LetterDialogFrame>
  );
}

/** Discarding a draft: it's gone, and can't be brought back. */
export function DiscardLetterDialog({letter, onCancel, onDone}: LetterDialogProps) {
  const change = useDialogChange(useLetterActions(letter.id ?? '').discard, onDone, 'The draft could not be discarded. Please try again.');
  return (
    <LetterDialogFrame
      letter={letter}
      title="Discard Draft?"
      message="The draft and its recipients are discarded. This can't be undone."
      icon={AppIcon.Delete}
      isDanger
      confirmText="Discard Draft"
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}
    />
  );
}

/**
 * Withdrawing an approved letter found wrong (decision 104), with a reason (required, 500 characters). It lists the
 * recipients it was already sent to and says to tell them. The PDF stays on file, marked withdrawn; a correction can
 * be written after.
 */
export function WithdrawLetterDialog({letter, onCancel, onDone}: LetterDialogProps) {
  const [reason, setReason] = useState('');
  const change = useDialogChange(useLetterActions(letter.id ?? '').withdraw, onDone, 'The letter could not be withdrawn. Please try again.', [
    'reason',
  ]);
  const sent = (letter.recipients ?? []).filter((r) => r.dispatchStatus === 'Sent');
  return (
    <LetterDialogFrame
      letter={letter}
      title="Withdraw Letter?"
      message="It's marked withdrawn, and the PDF stays on file marked so. It can't be sent to anyone else. You can write a correction after."
      icon={AppIcon.Withdraw}
      intent="warning"
      isDanger
      confirmText="Withdraw Letter"
      confirmDisabled={!reason.trim()}
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({reason: reason.trim(), expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}>
      {sent.length > 0 ? (
        <VStack gap={2} hAlign="stretch">
          <AlertMessage status="warning" message="It has already been sent to these recipients. Tell them it's withdrawn, and why." />
          <RecipientAddresses recipients={sent} label="Already sent to" />
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          It hasn't been sent to anyone yet.
        </Text>
      )}
      <TextArea
        label="Why it's withdrawn"
        value={reason}
        onChange={setReason}
        rows={3}
        maxLength={MAX_LETTER_COMMENT}
        isRequired
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'reason')}
      />
    </LetterDialogFrame>
  );
}

/** A recipient's dialog: the letter and which recipient. */
export interface RecipientDialogProps extends LetterDialogProps {
  recipient: LetterRecipient;
}

/**
 * Sending to one recipient by secure email from MedApp, naming the address. It's sent at once: Sent only when the mail
 * server takes it; a refusal is a 409 in the API's words, and the recipient then shows Failed.
 */
export function SendSecureEmailDialog({letter, recipient, onCancel, onDone}: RecipientDialogProps) {
  const change = useDialogChange(useLetterActions(letter.id ?? '').sendSecureEmail, onDone, 'The letter could not be sent. Please try again.');
  return (
    <LetterDialogFrame
      letter={letter}
      title="Send by Secure Email?"
      message={`The PDF goes to ${recipient.name || recipientKindWords(recipient.kind)} at ${recipient.email}, from MedApp's secure email. The email itself says only that a letter is attached.`}
      icon={AppIcon.Send}
      confirmText="Send"
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({recipientId: recipient.id ?? '', expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}
    />
  );
}

/** Today (UK) and the days before it that a letter can be recorded as sent on, newest first, as YYYY-MM-DD. */
export function recordableDays(now: Date = new Date()): string[] {
  const [year, month, day] = ukDay(now.toISOString()).split('-').map(Number);
  return Array.from({length: RECORD_SENT_DAYS_BACK + 1}, (_, back) => new Date(Date.UTC(year, month - 1, day - back)).toISOString().slice(0, 10));
}

/** Recording a letter as sent to one recipient another way (decision 103): how, and on which day (today, or up to 7 days back). */
export function RecordSentDialog({letter, recipient, onCancel, onDone}: RecipientDialogProps) {
  const days = recordableDays();
  const [method, setMethod] = useState('');
  const [sentOn, setSentOn] = useState(days[0]);
  const change = useDialogChange(useLetterActions(letter.id ?? '').recordSent, onDone, 'It could not be recorded. Please try again.', [
    'method',
    'sentOn',
  ]);
  return (
    <LetterDialogFrame
      letter={letter}
      title="Record as Sent"
      message={`How it reached ${recipient.name || recipientKindWords(recipient.kind)}, from the PDF.`}
      icon={AppIcon.Check}
      confirmText="Record as Sent"
      confirmDisabled={!method}
      isProcessing={change.isProcessing}
      error={change.error}
      onConfirm={() => change.run({recipientId: recipient.id ?? '', method, sentOn, expectedVersion: letter.version ?? 0})}
      onCancel={onCancel}>
      <RadioList label="How" value={method} onChange={setMethod} isRequired status={fieldStatus(change.errors, 'method')}>
        {RECORDED_METHODS.map((m) => (
          <RadioListItem key={m.value} value={m.value} label={m.label} />
        ))}
      </RadioList>
      <AnchoredSelector
        label="Sent on"
        options={days.map((d, i) => ({value: d, label: i === 0 ? `Today, ${formatDate(d)}` : formatDate(d)}))}
        value={sentOn}
        onChange={(value: string) => setSentOn(value)}
        isRequired
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'sentOn')}
      />
    </LetterDialogFrame>
  );
}

import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import type {IconType} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import type {UseMutationResult} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {
  IDENTITY_CHECK_METHODS,
  MAX_DATA_REQUEST_REASON,
  SEND_METHODS,
  useDataRequestActions,
  type DataRequest,
} from '../../api/dataRequests';
import {ApiValidationError, fieldStatus, otherMessages, problemMessage, type ValidationErrors} from '../../api/errors';
import {formatDate, todayIso, WEEK_STARTS_ON} from '../../utils/dates';
import {ConfirmDialog, type ConfirmIntent} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** What every data request dialog is told: the request as the page shows it, its patient, and what to do when closed or done. */
export interface DataRequestDialogProps {
  request: DataRequest;
  /** The patient as the banner shows them, once read. */
  patient?: BannerPatient | null;
  onCancel: () => void;
  /** Done: the request as the API returned it. */
  onDone: (request: DataRequest) => void;
}

interface FrameProps {
  request: DataRequest;
  patient?: BannerPatient | null;
  title: string;
  message: ReactNode;
  icon: IconType;
  intent?: ConfirmIntent;
  isDanger?: boolean;
  confirmText: string;
  confirmDisabled?: boolean;
  isProcessing: boolean;
  error: string | null;
  /** The refusal's headline, naming what wasn't done ("Letter not sent"). */
  errorLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  children?: ReactNode;
}

/**
 * The frame of every data request dialog: what it does, then the patient as `PatientBanner` shows them (compact: the
 * dialog covers the page's) and the request's dates, so the wrong request isn't changed, then the dialog's own fields,
 * then a refusal in the API's words. Each dialog is mounted only while open, so its fields start empty.
 */
function DataRequestDialogFrame({
  request,
  patient,
  title,
  message,
  icon,
  intent,
  isDanger,
  confirmText,
  confirmDisabled,
  isProcessing,
  error,
  errorLabel,
  onConfirm,
  onCancel,
  children,
}: FrameProps) {
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
        {patient && <PatientBanner patient={patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Received">{formatDate(request.receivedOn ?? '')}</MetadataListItem>
          <MetadataListItem label="Due">{formatDate(request.dueOn ?? '')}</MetadataListItem>
        </MetadataList>
        {children}
        {error && <AlertMessage label={errorLabel} message={error} />}
      </VStack>
    </ConfirmDialog>
  );
}

/**
 * Runs a dialog's change: a 400 shows on its fields (and any other messages above them), anything else in the API's
 * words in the dialog, which stays open (the request is read again after a 409).
 */
export function useDialogChange<T>(
  mutation: UseMutationResult<DataRequest, unknown, T>,
  onDone: (request: DataRequest) => void,
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

/**
 * Recording that the requester's identity was checked (needed before the pack can be made): on which day, how (none
 * chosen to begin with) and, for Other, how in words.
 */
export function IdentityCheckDialog({request, patient, onCancel, onDone}: DataRequestDialogProps) {
  const [checkedOn, setCheckedOn] = useState<string>(todayIso());
  const [method, setMethod] = useState('');
  const [detail, setDetail] = useState('');
  const change = useDialogChange(useDataRequestActions(request.id ?? '').identityCheck, onDone, 'The identity check could not be recorded. Please try again.', [
    'checkedOn',
    'method',
    'detail',
  ]);
  const needsDetail = method === 'Other';
  return (
    <DataRequestDialogFrame
      request={request}
      patient={patient}
      title="Record Identity Check"
      message="How you made sure the requester is who they say they are, and has the right to the record."
      icon={AppIcon.IdentityCheck}
      confirmText="Record Identity Check"
      confirmDisabled={!checkedOn || !method || (needsDetail && !detail.trim())}
      isProcessing={change.isProcessing}
      error={change.error}
      errorLabel="Identity check not recorded"
      onConfirm={() => change.run({checkedOn, method, detail: detail.trim() || null, expectedVersion: request.version ?? 0})}
      onCancel={onCancel}>
      <DateInput
        label="Checked on"
        value={checkedOn as ISODateString}
        onChange={(value) => setCheckedOn(value ?? '')}
        weekStartsOn={WEEK_STARTS_ON}
        format="date_weekday"
        isRequired
        status={fieldStatus(change.errors, 'checkedOn')}
      />
      <RadioList label="How" value={method} onChange={setMethod} isRequired status={fieldStatus(change.errors, 'method')}>
        {IDENTITY_CHECK_METHODS.map((m) => (
          <RadioListItem key={m.value} value={m.value} label={m.label} />
        ))}
      </RadioList>
      <TextArea
        label="Detail"
        description={needsDetail ? 'How it was checked, in words.' : 'Optional: anything worth noting, such as the documents seen.'}
        value={detail}
        onChange={setDetail}
        rows={3}
        maxLength={MAX_DATA_REQUEST_REASON}
        isRequired={needsDetail}
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'detail')}
      />
    </DataRequestDialogFrame>
  );
}

/** The extensions a request can still have: up to two more months in all, beyond the first. */
export function extensionChoices(request: Pick<DataRequest, 'extensionMonths'>): number[] {
  return [1, 2].filter((months) => months > (request.extensionMonths ?? 0));
}

/**
 * Extending the due date by one or two more months in all (only within the first month, for a complex request or
 * several from the same person), with the reason the requester is given.
 */
export function ExtendDialog({request, patient, onCancel, onDone}: DataRequestDialogProps) {
  const choices = extensionChoices(request);
  const [months, setMonths] = useState('');
  const [reason, setReason] = useState(request.extensionReason ?? '');
  const change = useDialogChange(useDataRequestActions(request.id ?? '').extend, onDone, 'The due date could not be extended. Please try again.', [
    'months',
    'reason',
  ]);
  return (
    <DataRequestDialogFrame
      request={request}
      patient={patient}
      title="Extend Due Date"
      message="Only for a complex request, or several from the same person, and only within the first month. Tell the requester why, within that month."
      icon={AppIcon.Wait}
      intent="warning"
      confirmText="Extend"
      confirmDisabled={!months || !reason.trim()}
      isProcessing={change.isProcessing}
      error={change.error}
      errorLabel="Due date not extended"
      onConfirm={() => change.run({months: Number(months), reason: reason.trim(), expectedVersion: request.version ?? 0})}
      onCancel={onCancel}>
      <RadioList label="Extend the due date by" value={months} onChange={setMonths} isRequired status={fieldStatus(change.errors, 'months')}>
        {choices.map((m) => (
          <RadioListItem key={m} value={String(m)} label={m === 1 ? 'One more month' : 'Two more months'} />
        ))}
      </RadioList>
      <TextArea
        label="Reason given to the requester"
        value={reason}
        onChange={setReason}
        rows={3}
        maxLength={MAX_DATA_REQUEST_REASON}
        isRequired
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'reason')}
      />
    </DataRequestDialogFrame>
  );
}

/** Recording that the response was sent: on which day, and how (none chosen to begin with). It closes the request. */
export function RecordSentDialog({request, patient, onCancel, onDone}: DataRequestDialogProps) {
  const [sentOn, setSentOn] = useState<string>(todayIso());
  const [method, setMethod] = useState('');
  const change = useDialogChange(useDataRequestActions(request.id ?? '').sent, onDone, 'It could not be recorded as sent. Please try again.', [
    'sentOn',
    'method',
  ]);
  return (
    <DataRequestDialogFrame
      request={request}
      patient={patient}
      title="Record as Sent"
      message="When and how the pack reached the requester. The request is then closed."
      icon={AppIcon.Send}
      confirmText="Record as Sent"
      confirmDisabled={!sentOn || !method}
      isProcessing={change.isProcessing}
      error={change.error}
      errorLabel="Not recorded as sent"
      onConfirm={() => change.run({sentOn, method, expectedVersion: request.version ?? 0})}
      onCancel={onCancel}>
      <DateInput
        label="Sent on"
        value={sentOn as ISODateString}
        onChange={(value) => setSentOn(value ?? '')}
        weekStartsOn={WEEK_STARTS_ON}
        format="date_weekday"
        isRequired
        status={fieldStatus(change.errors, 'sentOn')}
      />
      <RadioList label="How" value={method} onChange={setMethod} isRequired status={fieldStatus(change.errors, 'method')}>
        {SEND_METHODS.map((m) => (
          <RadioListItem key={m.value} value={m.value} label={m.label} />
        ))}
      </RadioList>
    </DataRequestDialogFrame>
  );
}

/** Refusing the request, with the reason the requester is given (required). It closes the request. */
export function RefuseDialog({request, patient, onCancel, onDone}: DataRequestDialogProps) {
  const [reason, setReason] = useState('');
  const change = useDialogChange(useDataRequestActions(request.id ?? '').refuse, onDone, 'The request could not be refused. Please try again.', ['reason']);
  return (
    <DataRequestDialogFrame
      request={request}
      patient={patient}
      title="Refuse Request?"
      message="Only where the request is manifestly unfounded or excessive. The requester must be told why, and that they can complain to the Information Commissioner's Office."
      icon={AppIcon.StatusCancelled}
      intent="warning"
      isDanger
      confirmText="Refuse Request"
      confirmDisabled={!reason.trim()}
      isProcessing={change.isProcessing}
      error={change.error}
      errorLabel="Request not refused"
      onConfirm={() => change.run({reason: reason.trim(), expectedVersion: request.version ?? 0})}
      onCancel={onCancel}>
      <TextArea
        label="Reason given to the requester"
        value={reason}
        onChange={setReason}
        rows={3}
        maxLength={MAX_DATA_REQUEST_REASON}
        isRequired
        isDisabled={change.isProcessing}
        status={fieldStatus(change.errors, 'reason')}
      />
    </DataRequestDialogFrame>
  );
}

/** Recording that the requester withdrew the request. It closes the request. */
export function WithdrawDialog({request, patient, onCancel, onDone}: DataRequestDialogProps) {
  const change = useDialogChange(useDataRequestActions(request.id ?? '').withdraw, onDone, 'The request could not be withdrawn. Please try again.');
  return (
    <DataRequestDialogFrame
      request={request}
      patient={patient}
      title="Withdraw Request?"
      message="Record that the requester withdrew it. The request is closed, and stays in the patient's record."
      icon={AppIcon.Withdraw}
      intent="warning"
      confirmText="Withdraw Request"
      isProcessing={change.isProcessing}
      error={change.error}
      errorLabel="Request not withdrawn"
      onConfirm={() => change.run({expectedVersion: request.version ?? 0})}
      onCancel={onCancel}>
      <Text type="body" color="secondary">
        Nothing is sent to the requester.
      </Text>
    </DataRequestDialogFrame>
  );
}

import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type ReactNode} from 'react';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  MAX_ACTION_DETAIL,
  MAX_ERROR_DETAIL,
  RESULT_ACTIONS,
  resultSourceLabel,
  resultStatusLabel,
  useResultActions,
  type Result,
} from '../../api/results';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime} from '../../utils/dates';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Who can be a result's responsible clinician: holders of this permission (the API's own rule). */
export const RESPONSIBLE_PERMISSION = 'SignOffResults' as const;

interface ResultDialogProps {
  result: Result;
  onClose: () => void;
}

/**
 * The patient as `PatientBanner` shows them (compact: the dialog covers the page's) and the result's title, source,
 * when it was reported and its status, so the right one is changed. Every result dialog starts with it.
 */
function ResultSummaryLines({result}: {result: Result}) {
  return (
    <>
      {result.patient && <PatientBanner patient={result.patient} isCompact />}
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Result">{result.title}</MetadataListItem>
        <MetadataListItem label="Source">
          {result.specimenNumber ? `Sample ${result.specimenNumber}` : resultSourceLabel(result.source)}
        </MetadataListItem>
        <MetadataListItem label="Reported">{formatDateTime(result.reportedAtUtc ?? '')}</MetadataListItem>
        <MetadataListItem label="Status">{resultStatusLabel(result.status)}</MetadataListItem>
      </MetadataList>
    </>
  );
}

/** How a dialog shows the API's refusal: a 400 on its fields, anything else (a 409, a 403) in the API's words, headed `notDone`. */
function useOutcome(failed: string, notDone: string) {
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  return {
    errors,
    message,
    reset: () => {
      setErrors(null);
      setMessage(null);
    },
    onError: (e: unknown) => {
      if (e instanceof ApiValidationError) setErrors(e.errors);
      else setMessage(problemMessage(e, failed));
    },
    alerts: (fields: string[]): ReactNode => (
      <>
        {errors && <ValidationAlert errors={errors} fields={fields} />}
        {message && <AlertMessage label={notDone} message={message} />}
      </>
    ),
  };
}

/**
 * Signing a result off (decision 94): what was done, from the six actions in their fixed order, **none chosen to
 * start with** (the clinician chooses; MedApp never suggests one or checks it fits the report), and a detail, required
 * for Other (1,000 characters). Sends the version the page shows. Mount it afresh for each opening, so it starts empty.
 * A result on a pathway goes back to it once signed off ("Sign Off and Open Pathway", as authorising a lab case does);
 * one with no pathway stays where it is.
 */
export function SignOffResultDialog({result, onClose}: ResultDialogProps) {
  const announce = useAnnounce();
  const navigate = useNavigate();
  const {signOff} = useResultActions(result.id ?? '');
  const pathwayId = result.pathwayId;
  const outcome = useOutcome('The result could not be signed off.', 'Not signed off');
  const [action, setAction] = useState('');
  const [detail, setDetail] = useState('');
  const needsDetail = action === 'Other';
  const isComplete = action !== '' && (!needsDetail || detail.trim() !== '');

  const confirm = () => {
    outcome.reset();
    signOff.mutate(
      {action, detail, expectedVersion: result.version ?? 0},
      {
        onSuccess: () => {
          announce('Result signed off');
          onClose();
          if (pathwayId) void navigate({to: '/pathways/$id', params: {id: pathwayId}});
        },
        onError: outcome.onError,
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Sign Off Result"
      message="Record what was done about this result. Signing off is final, and it leaves the list of results awaiting sign-off."
      icon={AppIcon.SignOff}
      intent="accent"
      confirmText={pathwayId ? 'Sign Off and Open Pathway' : 'Sign Off'}
      confirmDisabled={!isComplete}
      isProcessing={signOff.isPending}
      hasSaveShortcut
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        <ResultSummaryLines result={result} />
        {outcome.alerts(['action', 'detail'])}
        <RadioList label="What was done" value={action} onChange={setAction} isRequired status={fieldStatus(outcome.errors, 'action')}>
          {RESULT_ACTIONS.map((a) => (
            <RadioListItem key={a.value} value={a.value} label={a.label} />
          ))}
        </RadioList>
        <TextArea
          label={needsDetail ? 'Detail' : 'Detail (optional)'}
          description="Part of the patient's clinical record."
          value={detail}
          onChange={setDetail}
          rows={3}
          maxLength={MAX_ACTION_DETAIL}
          isRequired={needsDetail}
          status={fieldStatus(outcome.errors, 'detail')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

/**
 * Giving a result to another responsible clinician (decision 95): a holder of SignOffResults, chosen from those who
 * hold it (not the one it has now), nobody chosen to start with. They're told. Sends the version the page shows.
 */
export function ReassignResultDialog({result, onClose}: ResultDialogProps) {
  const announce = useAnnounce();
  const {reassign} = useResultActions(result.id ?? '');
  const staff = useQuery(staffWithPermissionQuery(RESPONSIBLE_PERMISSION));
  const outcome = useOutcome('The result could not be reassigned.', 'Result not reassigned');
  const [userId, setUserId] = useState('');
  const options = (staff.data ?? [])
    .filter((member) => member.userId !== result.responsibleUserId)
    .map((member) => ({value: member.userId ?? '', label: member.username ?? ''}));

  const confirm = () => {
    outcome.reset();
    const chosen = options.find((option) => option.value === userId)?.label ?? '';
    reassign.mutate(
      {responsibleUserId: userId, expectedVersion: result.version ?? 0},
      {
        onSuccess: () => {
          announce(`Result reassigned to ${chosen}`);
          onClose();
        },
        onError: outcome.onError,
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Reassign Result"
      message={`Give it to another clinician to sign off. It's ${result.responsibleUsername}'s now; whoever you choose is told.`}
      icon={AppIcon.Reassign}
      intent="accent"
      confirmText="Reassign"
      confirmDisabled={!userId}
      isProcessing={reassign.isPending}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        <ResultSummaryLines result={result} />
        {outcome.alerts(['responsibleUserId'])}
        <AnchoredSelector
          label="Responsible clinician"
          description="Someone who can sign results off."
          placeholder={staff.isPending ? 'Loading staff...' : 'Choose who signs it off'}
          options={options}
          value={userId || undefined}
          onChange={(value: string) => setUserId(value)}
          isRequired
          status={staff.isError ? {type: 'error', message: 'The staff could not be loaded.'} : fieldStatus(outcome.errors, 'responsibleUserId')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

/**
 * Taking a result over (decision 95): the caller becomes its responsible clinician (`reassign` with their own ID).
 * Asks first, showing the patient and the result, as it takes it from someone.
 */
export function TakeOverResultDialog({result, userId, onClose}: ResultDialogProps & {userId: string}) {
  const announce = useAnnounce();
  const {reassign} = useResultActions(result.id ?? '');
  const outcome = useOutcome('The result could not be taken over.', 'Result not taken over');

  const confirm = () => {
    outcome.reset();
    reassign.mutate(
      {responsibleUserId: userId, expectedVersion: result.version ?? 0},
      {
        onSuccess: () => {
          announce('You are now responsible for this result');
          onClose();
        },
        onError: outcome.onError,
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Take Over Result?"
      message={
        result.needsClinician
          ? `${result.responsibleUsername} can no longer sign results off. You become responsible for signing it off.`
          : `It's ${result.responsibleUsername}'s now. You become responsible for signing it off instead of them.`
      }
      icon={AppIcon.Assign}
      intent="accent"
      confirmText="Take Over"
      isProcessing={reassign.isPending}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        <ResultSummaryLines result={result} />
        {outcome.alerts([])}
      </VStack>
    </ConfirmDialog>
  );
}

/**
 * Marking a result entered in error (decision 96): the report was on the wrong patient or specimen. Asks what was
 * wrong (required, 500 characters), and says what follows: its file is deleted and, for a specimen's report, the
 * specimen goes back to the lab to be reported again. Final. Sends the version the page shows.
 */
export function ResultEnteredInErrorDialog({result, onClose}: ResultDialogProps) {
  const announce = useAnnounce();
  const {enteredInError} = useResultActions(result.id ?? '');
  const outcome = useOutcome('The result could not be marked entered in error.', 'Not marked entered in error');
  const [detail, setDetail] = useState('');

  const confirm = () => {
    outcome.reset();
    enteredInError.mutate(
      {detail, expectedVersion: result.version ?? 0},
      {
        onSuccess: () => {
          announce('Result marked entered in error');
          onClose();
        },
        onError: outcome.onError,
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Mark Entered in Error?"
      message={
        result.specimenId
          ? `The report is on the wrong patient or sample. Its file will be deleted, and sample ${result.specimenNumber ?? ''} goes back to the lab to be reported again. The result stays in the record, marked entered in error. This can't be undone.`
          : "The report is on the wrong patient, or isn't a result. Its file will be deleted. The result stays in the record, marked entered in error. This can't be undone."
      }
      icon={AppIcon.EnteredInError}
      intent="warning"
      isDanger
      confirmText="Mark Entered in Error"
      confirmDisabled={detail.trim() === ''}
      isProcessing={enteredInError.isPending}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        <ResultSummaryLines result={result} />
        {outcome.alerts(['detail'])}
        <TextArea
          label="What was wrong"
          description="Part of the patient's clinical record."
          value={detail}
          onChange={setDetail}
          rows={3}
          maxLength={MAX_ERROR_DETAIL}
          isRequired
          status={fieldStatus(outcome.errors, 'detail')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

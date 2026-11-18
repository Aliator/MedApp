import {CheckboxList, CheckboxListItem} from '@astryxdesign/core/CheckboxList';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {upcomingMeetings, type MdtItem, type MdtMeeting} from '../../api/mdt';
import {formatDateTime} from '../../utils/dates';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** The most a question for the team can hold, as the API allows. */
export const MAX_MDT_QUESTION_LENGTH = 1000;
/** The most an outcome can hold, as the API allows. */
export const MAX_MDT_OUTCOME_LENGTH = 2000;

/** Sends a dialog's change; `fail` shows what went wrong in the dialog (a 400 on its fields, or the API's words). */
export type MdtSend<T> = (input: T, fail: (error: unknown) => void) => void;

/** What went wrong with a dialog's change: a 400's messages on its fields, or the API's words for anything else. */
function useDialogProblem(fields: readonly string[], fallback: string) {
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError && fields.some((f) => error.errors.fields[f]?.length)) setErrors(error.errors);
    else setProblem(problemMessage(error, fallback));
  };
  const clear = () => {
    setProblem(null);
    setErrors(null);
  };
  return {problem, errors, fail, clear};
}

/** An MDT as a choice: "Dermatology MDT: Thu 3 Dec 2026, 13:00". */
export const meetingLabel = (meeting: MdtMeeting) =>
  `${meeting.specialtyName ?? ''} ${meeting.title ? `(${meeting.title})` : 'MDT'}: ${formatDateTime(meeting.startUtc ?? '')}`;

const tooLong = (max: number) => ({type: 'error' as const, message: `At most ${max} characters.`});

export interface ListForMdtDialogProps {
  patient?: BannerPatient;
  pathwayTitle: string;
  /** Every MDT read; only those not yet ended are offered, soonest first. */
  meetings: readonly MdtMeeting[];
  isLoadingMeetings?: boolean;
  isProcessing: boolean;
  onConfirm: MdtSend<{meetingId: string; question: string}>;
  onCancel: () => void;
}

/**
 * Listing a pathway for an MDT (decision 141): the patient (as the patient banner shows them: hazard H-58), which MDT
 * (none chosen to begin with) and the question for the team (required, 1,000 characters). Mount it only while it's
 * open, so it starts empty.
 */
export function ListForMdtDialog({patient, pathwayTitle, meetings, isLoadingMeetings, isProcessing, onConfirm, onCancel}: ListForMdtDialogProps) {
  const [meetingId, setMeetingId] = useState('');
  const [question, setQuestion] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['meetingId', 'question'], 'The pathway could not be listed.');
  const options = upcomingMeetings(meetings).map((m) => ({value: m.id ?? '', label: meetingLabel(m)}));
  const isTooLong = question.length > MAX_MDT_QUESTION_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title="List for MDT"
      message={`Put ${pathwayTitle} forward for a multidisciplinary team meeting, with the question for the team.`}
      icon={AppIcon.Mdt}
      intent="accent"
      confirmText="List for MDT"
      confirmDisabled={!meetingId || !question.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm({meetingId, question: question.trim()}, fail);
      }}
      onCancel={onCancel}>
      {patient && <PatientBanner patient={patient} isCompact />}
      <AnchoredSelector
        label="MDT"
        value={meetingId}
        onChange={setMeetingId}
        isLoading={isLoadingMeetings}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'meetingId')}
        options={options}
      />
      {!isLoadingMeetings && options.length === 0 && (
        <Text type="body" color="secondary">No MDTs are arranged yet. Whoever arranges meetings marks one as a specialty's MDT.</Text>
      )}
      <TextArea
        label="Question for the team"
        value={question}
        onChange={setQuestion}
        rows={3}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'question') ?? (isTooLong ? tooLong(MAX_MDT_QUESTION_LENGTH) : undefined)}
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

export interface RecordMdtOutcomeDialogProps {
  item: MdtItem;
  /** The meeting's attendees, to choose who was present. */
  attendees: readonly {userId?: string; username?: string | null}[];
  isProcessing: boolean;
  onConfirm: MdtSend<{outcome: string; presentUserIds: string[]}>;
  onCancel: () => void;
}

/**
 * Recording what the team decided (decision 142): the patient (hazard H-58), the question, the outcome as the person
 * recording it writes it (required, 2,000 characters) and who was present, from the meeting's attendees. **Nothing is
 * prefilled or suggested**: the box starts empty and nobody is ticked. Mount it only while it's open.
 */
export function RecordMdtOutcomeDialog({item, attendees, isProcessing, onConfirm, onCancel}: RecordMdtOutcomeDialogProps) {
  const [outcome, setOutcome] = useState('');
  const [present, setPresent] = useState<string[]>([]);
  const {problem, errors, fail, clear} = useDialogProblem(['outcome', 'presentUserIds'], 'The outcome could not be recorded.');
  const isTooLong = outcome.length > MAX_MDT_OUTCOME_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title="Record MDT outcome"
      message="Write what the team decided. It's shown on the pathway and the pathway's assignees are told; nothing about the pathway changes by itself."
      icon={AppIcon.Mdt}
      intent="accent"
      confirmText="Record Outcome"
      confirmDisabled={!outcome.trim() || present.length === 0 || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm({outcome: outcome.trim(), presentUserIds: present}, fail);
      }}
      onCancel={onCancel}>
      {item.patient && <PatientBanner patient={item.patient} isCompact />}
      <Text type="body" weight="semibold">{item.pathwayTitle ?? ''}</Text>
      <Text type="body">{`Question: ${item.question ?? ''}`}</Text>
      <TextArea
        label="Outcome"
        value={outcome}
        onChange={setOutcome}
        rows={4}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'outcome') ?? (isTooLong ? tooLong(MAX_MDT_OUTCOME_LENGTH) : undefined)}
      />
      <CheckboxList
        label="Present"
        description="Who was at the meeting"
        value={present}
        onChange={setPresent}
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'presentUserIds')}>
        {attendees.map((a) => (
          <CheckboxListItem key={a.userId} value={a.userId ?? ''} label={a.username ?? ''} />
        ))}
      </CheckboxList>
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

export interface DeferMdtItemDialogProps {
  item: MdtItem;
  meetings: readonly MdtMeeting[];
  isLoadingMeetings?: boolean;
  isProcessing: boolean;
  onConfirm: MdtSend<string>;
  onCancel: () => void;
}

/**
 * Moving an item to a later MDT (decision 142), or listing it again after its meeting was cancelled (decision 144):
 * the patient, and which MDT (only those starting after the one it's on, none chosen).
 */
export function DeferMdtItemDialog({item, meetings, isLoadingMeetings, isProcessing, onConfirm, onCancel}: DeferMdtItemDialogProps) {
  const [meetingId, setMeetingId] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['meetingId'], 'The item could not be moved.');
  const after = item.meetingStartUtc ? new Date(item.meetingStartUtc).getTime() : 0;
  const options = upcomingMeetings(meetings)
    .filter((m) => m.id !== item.meetingId && new Date(m.startUtc ?? 0).getTime() > after)
    .map((m) => ({value: m.id ?? '', label: meetingLabel(m)}));
  const isRelisting = !item.meetingId;

  return (
    <ConfirmDialog
      isOpen
      title={isRelisting ? 'List again' : 'Move to a later MDT'}
      message={isRelisting ? 'Its meeting was cancelled. Choose the MDT to list it for.' : 'Choose the MDT to discuss it at instead.'}
      icon={AppIcon.Calendar}
      intent="accent"
      confirmText={isRelisting ? 'List Again' : 'Move'}
      confirmDisabled={!meetingId}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm(meetingId, fail);
      }}
      onCancel={onCancel}>
      {item.patient && <PatientBanner patient={item.patient} isCompact />}
      <AnchoredSelector
        label="MDT"
        value={meetingId}
        onChange={setMeetingId}
        isLoading={isLoadingMeetings}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'meetingId')}
        options={options}
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

export interface WithdrawMdtItemDialogProps {
  item: MdtItem;
  isProcessing: boolean;
  onConfirm: MdtSend<void>;
  onCancel: () => void;
}

/** Taking an item off the list without discussion: the patient and the question, then a confirmation. */
export function WithdrawMdtItemDialog({item, isProcessing, onConfirm, onCancel}: WithdrawMdtItemDialogProps) {
  const {problem, fail, clear} = useDialogProblem([], 'The item could not be withdrawn.');
  return (
    <ConfirmDialog
      isOpen
      title="Withdraw from MDT?"
      message="It comes off the list without being discussed. This can't be undone; list it again if it's needed."
      icon={AppIcon.Withdraw}
      intent="warning"
      confirmText="Withdraw"
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm(undefined, fail);
      }}
      onCancel={onCancel}>
      {item.patient && <PatientBanner patient={item.patient} isCompact />}
      <Text type="body">{`Question: ${item.question ?? ''}`}</Text>
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

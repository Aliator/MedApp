import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {MAX_CONSULT_QUESTION_LENGTH, MAX_CONSULT_REPLY_LENGTH, type Consult} from '../../api/consults';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Sends a dialog's change; `fail` shows what went wrong in the dialog (a 400 on its fields, or the API's words). */
export type ConsultSend<T> = (input: T, fail: (error: unknown) => void) => void;

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

const tooLong = (max: number) => ({type: 'error' as const, message: `At most ${max} characters.`});

export interface AskConsultDialogProps {
  patient?: BannerPatient;
  pathwayTitle: string;
  /** The teams that can be asked (every active team but the pathway's own), by name. */
  teams: readonly {id?: string; name?: string | null}[];
  isLoadingTeams?: boolean;
  isProcessing: boolean;
  onConfirm: ConsultSend<{askedTeamId: string; question: string}>;
  onCancel: () => void;
}

/**
 * Asking another team about a pathway (MC25): the patient (as the banner shows them), which team (a clinic or the lab;
 * none chosen to begin with) and the question (required, 2,000 characters). Mount it only while it's open, so it
 * starts empty.
 */
export function AskConsultDialog({patient, pathwayTitle, teams, isLoadingTeams, isProcessing, onConfirm, onCancel}: AskConsultDialogProps) {
  const [askedTeamId, setAskedTeamId] = useState('');
  const [question, setQuestion] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['askedTeamId', 'question'], 'The consult could not be sent.');
  const isTooLong = question.length > MAX_CONSULT_QUESTION_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title="Ask Another Team"
      message={`Ask a clinic or the lab about ${pathwayTitle}. They can read the pathway until you close the consult, and their reply is kept on it.`}
      icon={AppIcon.Question}
      intent="accent"
      confirmText="Send Consult"
      confirmDisabled={!askedTeamId || !question.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm({askedTeamId, question: question.trim()}, fail);
      }}
      onCancel={onCancel}>
      {patient && <PatientBanner patient={patient} isCompact />}
      <AnchoredSelector
        label="Team to ask"
        value={askedTeamId}
        onChange={setAskedTeamId}
        isLoading={isLoadingTeams}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'askedTeamId')}
        options={teams.map((t) => ({value: t.id ?? '', label: t.name ?? ''}))}
      />
      <TextArea
        label="Question"
        value={question}
        onChange={setQuestion}
        rows={4}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'question') ?? (isTooLong ? tooLong(MAX_CONSULT_QUESTION_LENGTH) : undefined)}
      />
      {problem && <AlertMessage label="Consult not sent" message={problem} />}
    </ConfirmDialog>
  );
}

export interface ReplyConsultDialogProps {
  consult: Consult;
  isProcessing: boolean;
  onConfirm: ConsultSend<string>;
  onCancel: () => void;
}

/**
 * The asked team's reply (MC25): the patient, the question, and the reply as its author writes it (required, 4,000
 * characters; nothing prefilled). Kept on the pathway; the asking team is told, without the reply.
 */
export function ReplyConsultDialog({consult, isProcessing, onConfirm, onCancel}: ReplyConsultDialogProps) {
  const [reply, setReply] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['reply'], 'The reply could not be saved.');
  const isTooLong = reply.length > MAX_CONSULT_REPLY_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title="Reply to Consult"
      message={`${consult.askingTeamName ?? 'Another team'} sent this consult to ${consult.askedTeamName ?? 'your team'}. Your reply is kept on the pathway, and they're told.`}
      icon={AppIcon.Question}
      intent="accent"
      confirmText="Reply"
      confirmDisabled={!reply.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm(reply.trim(), fail);
      }}
      onCancel={onCancel}>
      {consult.patient && <PatientBanner patient={consult.patient} isCompact />}
      <Text type="body" weight="semibold">{consult.pathwayTitle ?? ''}</Text>
      <Text type="body">{`Question: ${consult.question ?? ''}`}</Text>
      <TextArea
        label="Reply"
        value={reply}
        onChange={setReply}
        rows={5}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'reply') ?? (isTooLong ? tooLong(MAX_CONSULT_REPLY_LENGTH) : undefined)}
      />
      {problem && <AlertMessage label="Reply not saved" message={problem} />}
    </ConfirmDialog>
  );
}

export interface EndConsultDialogProps {
  consult: Consult;
  /** Close (after the reply) or withdraw (no reply needed any more). */
  kind: 'close' | 'withdraw';
  isProcessing: boolean;
  onConfirm: ConsultSend<void>;
  onCancel: () => void;
}

/** Closing a replied consult, or withdrawing one not yet replied to: either ends the asked team's reading of the pathway. */
export function EndConsultDialog({consult, kind, isProcessing, onConfirm, onCancel}: EndConsultDialogProps) {
  const isClose = kind === 'close';
  const {problem, fail, clear} = useDialogProblem([], isClose ? 'The consult could not be closed.' : 'The consult could not be withdrawn.');
  const team = consult.askedTeamName ?? 'The other team';
  return (
    <ConfirmDialog
      isOpen
      title={isClose ? 'Close Consult?' : 'Withdraw Consult?'}
      message={
        isClose
          ? `${team} can no longer read the pathway once it's closed. The question and reply stay on it.`
          : `${team} hasn't replied. Withdrawn, they can no longer read the pathway or reply. This can't be undone. Send a new consult if it's needed.`
      }
      icon={isClose ? AppIcon.Check : AppIcon.Withdraw}
      intent={isClose ? 'accent' : 'warning'}
      confirmText={isClose ? 'Close Consult' : 'Withdraw'}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm(undefined, fail);
      }}
      onCancel={onCancel}>
      <Text type="body">{`Question: ${consult.question ?? ''}`}</Text>
      {problem && <AlertMessage label={isClose ? 'Consult not closed' : 'Consult not withdrawn'} message={problem} />}
    </ConfirmDialog>
  );
}

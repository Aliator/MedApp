import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import type {Pathway, PathwayStage, StageSkipInput} from '../../api/pathways';
import {
  MAX_REINSTATE_REASON_LENGTH,
  MAX_SKIP_DETAIL_LENGTH,
  skipFieldsComplete,
  skipNeedsDetail,
  skipReasonLabel,
  STAGE_SKIP_REASONS,
  type StageSkipReason,
} from '../../api/stageSkips';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** Sends a dialog's change; `fail` shows what went wrong in the dialog (a 400 on its fields, or the API's words). */
type Send<T> = (input: T, fail: (error: unknown) => void) => void;

/** What went wrong with a dialog's change: a 400's messages on its fields, or the API's words for anything else. */
export function useDialogProblem(fields: readonly string[], fallback: string) {
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

export interface SkipStageDialogProps {
  stage: PathwayStage;
  /** Who the pathway is for, as the patient banner shows them (the dialog covers the page's own banner). */
  patient?: Pathway['patient'];
  isProcessing: boolean;
  onConfirm: Send<StageSkipInput>;
  onCancel: () => void;
}

/**
 * Skipping an optional stage while it's current (decision 113): the patient (as the patient banner shows them, so the
 * right record is skipped: hazard H-51), which stage, and why: a required choice of Not needed, Patient declined or
 * Other, none chosen to begin with, and a detail (500 characters, required for Other). Mount it only while it's open,
 * so its choices start empty.
 */
export function SkipStageDialog({stage, patient, isProcessing, onConfirm, onCancel}: SkipStageDialogProps) {
  const [reason, setReason] = useState<StageSkipReason | ''>('');
  const [detail, setDetail] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['reason', 'detail'], 'The stage could not be skipped.');
  const isTooLong = detail.length > MAX_SKIP_DETAIL_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title={`Skip ${stage.name}?`}
      message={`${stage.name} will be skipped: nothing more is done for it, and the pathway moves on to the next stage. Any visit for it waiting in Requires Appointment is cancelled. Someone who can unlock stages can reinstate it.`}
      icon={AppIcon.Skip}
      intent="warning"
      confirmText="Skip Stage"
      confirmDisabled={!skipFieldsComplete(reason, detail)}
      isProcessing={isProcessing}
      onConfirm={() => {
        if (!reason) return;
        clear();
        onConfirm({reason, detail: detail.trim()}, fail);
      }}
      onCancel={onCancel}>
      {patient && <PatientBanner patient={patient} isCompact />}
      <Text type="body" weight="semibold">{`Stage to skip: ${stage.stageOrder}. ${stage.name}`}</Text>
      <RadioList
        label="Reason"
        value={reason}
        onChange={(value) => setReason(value as StageSkipReason)}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'reason')}>
        {STAGE_SKIP_REASONS.map((r) => (
          <RadioListItem key={r.value} value={r.value} label={r.label} />
        ))}
      </RadioList>
      <TextArea
        label={skipNeedsDetail(reason) ? 'Detail' : 'Detail (optional)'}
        description="Say more if it helps."
        value={detail}
        onChange={setDetail}
        rows={2}
        isRequired={skipNeedsDetail(reason)}
        isDisabled={isProcessing}
        status={
          fieldStatus(errors, 'detail') ??
          (isTooLong ? {type: 'error', message: `At most ${MAX_SKIP_DETAIL_LENGTH} characters.`} : undefined)
        }
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

export interface ReinstateStageDialogProps {
  stage: PathwayStage;
  patient?: Pathway['patient'];
  isProcessing: boolean;
  /** Sends the reason, trimmed. */
  onConfirm: Send<string>;
  onCancel: () => void;
}

/**
 * Reinstating a skipped stage (decision 115, holders of UnlockStages): the patient, the stage and how it was
 * skipped, and why it's to run again (required, 500 characters). A branch is re-chosen only this way: by reinstating
 * its stages. Mount it only while it's open, so the reason starts empty.
 */
export function ReinstateStageDialog({stage, patient, isProcessing, onConfirm, onCancel}: ReinstateStageDialogProps) {
  const [reason, setReason] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['reason'], 'The stage could not be reinstated.');
  const isTooLong = reason.length > MAX_REINSTATE_REASON_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title={`Reinstate ${stage.name}?`}
      message="It runs again in its place: if it comes before the current stage, it becomes the current stage. A pathway awaiting review or discharged goes back to in progress."
      icon={AppIcon.Restore}
      intent="warning"
      confirmText="Reinstate Stage"
      confirmDisabled={!reason.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm(reason.trim(), fail);
      }}
      onCancel={onCancel}>
      {patient && <PatientBanner patient={patient} isCompact />}
      <Text type="body" weight="semibold">{`Stage to reinstate: ${stage.stageOrder}. ${stage.name}`}</Text>
      {stage.skipReason && <Text type="body">{`It was skipped: ${skipReasonLabel(stage.skipReason)}.`}</Text>}
      <TextArea
        label="Reason"
        value={reason}
        onChange={setReason}
        rows={2}
        placeholder="e.g. The other branch was chosen in error"
        isRequired
        isDisabled={isProcessing}
        status={
          fieldStatus(errors, 'reason') ??
          (isTooLong ? {type: 'error', message: `At most ${MAX_REINSTATE_REASON_LENGTH} characters.`} : undefined)
        }
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

export interface OutcomeChoiceProps {
  /** A choice node being finished for the first time (`needsOutcome`), with its `outcomes`. */
  stage: PathwayStage;
  /** The outcome recorded, "" until the clinician chooses one. */
  outcome: string;
  onOutcomeChange: (outcome: string) => void;
  isDisabled?: boolean;
}

/**
 * Recording a choice node's outcome as it's finished (the owner's model, 19 Nov). **The clinician records it; MedApp
 * never chooses**: the outcomes are listed in the template's order, none chosen to begin with, each with the stages it
 * adds, and nothing here reads results or notes to suggest one. The finish dialogs hold the choice, and keep Finish
 * disabled until there is one. Unchosen outcomes never join the pathway.
 */
export function OutcomeChoice({stage, outcome, onOutcomeChange, isDisabled}: OutcomeChoiceProps) {
  const chosen = (stage.outcomes ?? []).find((o) => o.name === outcome);
  return (
    <>
      <RadioList
        label="What was the outcome?"
        description="Record the outcome for this patient. Its stages are added to the pathway."
        value={outcome}
        onChange={onOutcomeChange}
        isRequired
        isDisabled={isDisabled}>
        {(stage.outcomes ?? []).map((o) => (
          <RadioListItem
            key={o.name}
            value={o.name ?? ''}
            label={o.name ?? ''}
            description={(o.stageNames ?? []).length > 0 ? `Adds: ${(o.stageNames ?? []).join(', ')}` : 'Adds no stages'}
          />
        ))}
      </RadioList>
      {chosen && (
        <Text type="body">
          {(chosen.stageNames ?? []).length > 0 ? `These stages will be added: ${(chosen.stageNames ?? []).join(', ')}.` : 'No stages will be added.'}
        </Text>
      )}
    </>
  );
}

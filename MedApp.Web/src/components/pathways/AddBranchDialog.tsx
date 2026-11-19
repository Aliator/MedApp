import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {andList, MAX_BRANCH_REASON_LENGTH} from '../../api/pathwayRoutes';
import type {Pathway} from '../../api/pathways';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {useDialogProblem} from './StageSkipDialogs';

export interface AddBranchInput {
  templateBranchId: string;
  afterStageId: string;
  reason: string;
}

export interface AddBranchDialogProps {
  /** The pathway, with its template's `addableBranches` (each with where it can go now) and its stages. */
  pathway: Pathway;
  isProcessing: boolean;
  /** Sends the branch; `fail` shows what went wrong in the dialog. */
  onConfirm: (input: AddBranchInput, fail: (error: unknown) => void) => void;
  onCancel: () => void;
}

/**
 * Adding one of the template's branches when new information comes in (MC10, H-70). **The clinician chooses; MedApp
 * never suggests**: the branches are listed in the template's order, none chosen to begin with, nor where it goes,
 * and a reason is needed. Only the branches that can be added now are offered, each after the stages it may follow.
 * Its stages run side by side with what's left, and the last stage waits for them.
 */
export function AddBranchDialog({pathway, isProcessing, onConfirm, onCancel}: AddBranchDialogProps) {
  const [branchId, setBranchId] = useState('');
  const [afterId, setAfterId] = useState('');
  const [reason, setReason] = useState('');
  const {problem, errors, fail, clear} = useDialogProblem(['reason', 'templateBranchId', 'afterStageId'], 'The branch could not be added.');
  const addable = (pathway.addableBranches ?? []).filter((b) => (b.afterStageIds ?? []).length > 0);
  const branch = addable.find((b) => b.templateBranchId === branchId);
  const afterStages = (pathway.stages ?? []).filter((s) => branch?.afterStageIds?.includes(s.id ?? ''));
  const isTooLong = reason.length > MAX_BRANCH_REASON_LENGTH;

  return (
    <ConfirmDialog
      isOpen
      title="Add a Branch"
      message="Add one of this template's branches because of new information. Choose the branch, the stage it comes after, and say why: nothing is chosen for you."
      icon={AppIcon.Add}
      confirmText="Add Branch"
      confirmDisabled={!branch || !afterId || !reason.trim() || isTooLong}
      isProcessing={isProcessing}
      onConfirm={() => {
        clear();
        onConfirm({templateBranchId: branchId, afterStageId: afterId, reason: reason.trim()}, fail);
      }}
      onCancel={onCancel}>
      {pathway.patient && <PatientBanner patient={pathway.patient} isCompact />}
      <AnchoredSelector
        label="Branch"
        placeholder="Choose a branch"
        options={addable.map((b) => ({value: b.templateBranchId ?? '', label: b.name ?? ''}))}
        value={branchId || undefined}
        onChange={(value: string) => {
          setBranchId(value);
          setAfterId('');
        }}
        isRequired
        isDisabled={isProcessing}
        status={fieldStatus(errors, 'templateBranchId')}
      />
      {branch && <Text type="body">{`Its stages: ${andList((branch.stageNames ?? []).map((name, i) => `${i + 1}. ${name}`))}.`}</Text>}
      <AnchoredSelector
        label="Comes after"
        description="Its stages start when this stage is finished, side by side with what's left. The last stage waits for them."
        placeholder="Choose a stage"
        options={afterStages.map((s) => ({value: s.id ?? '', label: `${s.stageOrder}. ${s.name}`}))}
        value={afterId || undefined}
        onChange={setAfterId}
        isRequired
        isDisabled={isProcessing || !branch}
        status={fieldStatus(errors, 'afterStageId')}
      />
      <TextArea
        label="Reason"
        value={reason}
        onChange={setReason}
        rows={2}
        placeholder="e.g. The histology report shows a melanoma"
        isRequired
        isDisabled={isProcessing}
        status={
          fieldStatus(errors, 'reason') ?? (isTooLong ? {type: 'error', message: `At most ${MAX_BRANCH_REASON_LENGTH} characters.`} : undefined)
        }
      />
      {problem && <AlertMessage message={problem} />}
    </ConfirmDialog>
  );
}

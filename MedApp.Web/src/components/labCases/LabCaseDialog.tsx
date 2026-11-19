import type {IconType} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  labOutcomeLabel,
  MAX_LAB_DETAIL,
  MAX_LAB_RESULTS,
  MAX_SECOND_OPINION_WHERE,
  useLabCaseActions,
  type LabCase,
  type LabCaseStep,
} from '../../api/labCases';
import {ukDateTime, ukDateTimeToUtc, type UkDateTime} from '../../utils/specimenTimes';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {StepTimeFields} from '../specimens/StepTimeFields';

/** What the dialog records: the next step, a choice's outcome, the results, or entered in error (a step, or the case). */
export type LabCaseDialogKind =
  | {kind: 'step'}
  | {kind: 'outcome'; outcome: string}
  | {kind: 'report'}
  | {kind: 'stepError'; step: LabCaseStep}
  | {kind: 'caseError'};

const FIELDS = ['atUtc', 'detail', 'outcome', 'results'];

export interface LabCaseDialogProps {
  dialog: LabCaseDialogKind;
  labCase: LabCase;
  onClose: () => void;
}

interface Words {
  title: string;
  message: string;
  confirm: string;
  icon: IconType;
  announced: string;
  failed: string;
  /** The banner's headline when it fails. */
  notDone: string;
  isDanger?: boolean;
}

function wordsFor(dialog: LabCaseDialogKind, labCase: LabCase): Words {
  const next = labCase.next;
  switch (dialog.kind) {
    case 'step':
      return {
        title: `Record ${next?.name ?? 'Step'}`,
        message: 'Recorded as done, by you, at the time below.',
        confirm: 'Record Step',
        icon: AppIcon.Success,
        announced: `${next?.name ?? 'Step'} recorded`,
        failed: 'The step could not be recorded.',
        notDone: 'Step not recorded',
      };
    case 'outcome':
      return {
        title: `${next?.choice?.question ?? 'Outcome'} ${labOutcomeLabel(dialog.outcome)}`,
        message:
          dialog.outcome === 'Insufficient'
            ? 'The case closes with a repeat sample requested, its samples are rejected as insufficient, and the clinic is told. The clinic or hospital sends a new sample.'
            : 'Recorded as your outcome. The case goes on from it.',
        confirm: 'Record Outcome',
        icon: dialog.outcome === 'Insufficient' ? AppIcon.Warning : AppIcon.Success,
        announced: 'Outcome recorded',
        failed: 'The outcome could not be recorded.',
        notDone: 'Outcome not recorded',
      };
    case 'report':
      return {
        title: 'Enter Results and Authorise',
        // A pathway's case: the results go on its lab stage, and the pathway opens next for the lab to finish the stage
        // there (with its notes), which sends it back (the owner, 19 November 2026).
        message: labCase.pathwayId
          ? 'The results you enter are the report. Authorising sends it to the clinic and adds the results to the pathway. The pathway then opens: finish the lab stage to hand it back.'
          : 'The results you enter are the report. Authorising sends it back to the clinic or the hospital.',
        confirm: labCase.pathwayId ? 'Authorise and Open Pathway' : 'Authorise Report',
        icon: AppIcon.SignOff,
        announced: 'Report authorised',
        failed: 'The report could not be authorised.',
        notDone: 'Report not authorised',
      };
    case 'stepError':
      return {
        title: 'Mark Step Entered in Error?',
        message: `“${dialog.step.name}” was recorded by mistake. It stays in the case, struck through, and the case goes back to it.`,
        confirm: 'Mark Entered in Error',
        icon: AppIcon.EnteredInError,
        announced: 'Step marked entered in error',
        failed: 'The step could not be marked entered in error.',
        notDone: 'Not marked entered in error',
        isDanger: true,
      };
    case 'caseError':
      return {
        title: 'Mark Case Entered in Error?',
        message: `Lab case ${labCase.number} was opened by mistake (the wrong test, say). It closes, stays in the record, and its samples can be put in a new case. This can't be undone.`,
        confirm: 'Mark Entered in Error',
        icon: AppIcon.EnteredInError,
        announced: 'Lab case marked entered in error',
        failed: 'The case could not be marked entered in error.',
        notDone: 'Not marked entered in error',
        isDanger: true,
      };
  }
}

/**
 * Recording on a lab case, from the version the page shows: the next step (when, and for a second opinion where it
 * went), a choice's outcome as the person chose it on the page (a reason, required for Insufficient), the results and
 * their authorising, or entered in error. Each shows the patient and the case's number and test, so the right case is
 * changed (hazard H-73). A 400 shows on its fields; a 409 or 403 in the API's words, after which the case is read
 * again. Mount it afresh for each opening, so it starts empty and at now.
 */
export function LabCaseDialog({dialog, labCase, onClose}: LabCaseDialogProps) {
  const announce = useAnnounce();
  const navigate = useNavigate();
  const actions = useLabCaseActions(labCase.id ?? '');
  const [initialAt] = useState<UkDateTime>(() => ukDateTime());
  const [at, setAt] = useState<UkDateTime>(initialAt);
  const [detail, setDetail] = useState('');
  const [results, setResults] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const words = wordsFor(dialog, labCase);
  const next = labCase.next;
  const isSecondOpinion = dialog.kind === 'step' && next?.stepKind === 'SentForSecondOpinion';
  const needsDetail = isSecondOpinion || dialog.kind === 'stepError' || dialog.kind === 'caseError' || (dialog.kind === 'outcome' && dialog.outcome === 'Insufficient');
  const expectedVersion = labCase.version ?? 0;
  const mutation = {step: actions.step, outcome: actions.outcome, report: actions.report, stepError: actions.stepEnteredInError, caseError: actions.enteredInError}[
    dialog.kind
  ];

  const isComplete =
    (dialog.kind !== 'step' || Boolean(at.date && at.time)) && (!needsDetail || detail.trim() !== '') && (dialog.kind !== 'report' || results.trim() !== '');

  const confirm = () => {
    setErrors(null);
    setMessage(null);
    const callbacks = {
      onSuccess: () => {
        announce(words.announced);
        onClose();
        // A pathway's case: on to the pathway, where the lab finishes its stage to send it back (the owner, 19 Nov).
        if (dialog.kind === 'report' && labCase.pathwayId) void navigate({to: '/pathways/$id', params: {id: labCase.pathwayId}});
      },
      onError: (e: unknown) => {
        if (e instanceof ApiValidationError) setErrors(e.errors);
        else setMessage(problemMessage(e, words.failed));
      },
    };
    if (dialog.kind === 'step') {
      // Left at the time the dialog opened, it's sent as now (the API's default), so it's never in the future.
      const atUtc = at.date === initialAt.date && at.time === initialAt.time ? undefined : (ukDateTimeToUtc(at) ?? undefined);
      actions.step.mutate(
        {testStepId: next?.testStepId ?? null, fixedStep: next?.testStepId ? null : (next?.name ?? null), atUtc, detail, expectedVersion},
        callbacks,
      );
    } else if (dialog.kind === 'outcome')
      actions.outcome.mutate({choice: next?.choice?.choice ?? '', outcome: dialog.outcome, detail, expectedVersion}, callbacks);
    else if (dialog.kind === 'report') actions.report.mutate({results: results.trim(), expectedVersion}, callbacks);
    else if (dialog.kind === 'stepError') actions.stepEnteredInError.mutate({stepId: dialog.step.id ?? '', detail: detail.trim(), expectedVersion}, callbacks);
    else actions.enteredInError.mutate({detail: detail.trim(), expectedVersion}, callbacks);
  };

  return (
    <ConfirmDialog
      isOpen
      title={words.title}
      message={words.message}
      icon={words.icon}
      intent={words.isDanger || (dialog.kind === 'outcome' && dialog.outcome === 'Insufficient') ? 'warning' : 'accent'}
      isDanger={words.isDanger}
      confirmText={words.confirm}
      confirmDisabled={!isComplete}
      isProcessing={mutation.isPending}
      hasSaveShortcut={dialog.kind === 'step' || dialog.kind === 'report'}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {labCase.patient && <PatientBanner patient={labCase.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Lab case">{labCase.number}</MetadataListItem>
          <MetadataListItem label="Test">{labCase.labTestName}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage label={words.notDone} message={message} />}
        {dialog.kind === 'step' && <StepTimeFields step="Done" value={at} onChange={setAt} status={fieldStatus(errors, 'atUtc')} />}
        {isSecondOpinion && (
          <TextInput
            label="Where it went"
            description="The service or pathologist giving the second opinion, as you'd write it."
            value={detail}
            onChange={(value) => setDetail(value.slice(0, MAX_SECOND_OPINION_WHERE))}
            isRequired
            autoComplete="off"
            status={fieldStatus(errors, 'detail')}
          />
        )}
        {dialog.kind === 'report' && (
          <TextArea
            label="Results"
            description="The report as it goes back. Part of the patient's clinical record; MedApp doesn't read or interpret it."
            value={results}
            onChange={setResults}
            rows={8}
            maxLength={MAX_LAB_RESULTS}
            isRequired
            status={fieldStatus(errors, 'results')}
          />
        )}
        {needsDetail && !isSecondOpinion && (
          <TextArea
            label={dialog.kind === 'outcome' ? 'Why it’s insufficient' : 'What was wrong'}
            description="Part of the patient's clinical record."
            value={detail}
            onChange={setDetail}
            rows={3}
            maxLength={MAX_LAB_DETAIL}
            isRequired
            status={fieldStatus(errors, 'detail')}
          />
        )}
      </VStack>
    </ConfirmDialog>
  );
}

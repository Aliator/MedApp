import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {useKeepPatientRecord} from '../../api/patients';
import {saveProblem, NO_PROBLEM, type SaveProblem} from '../../pages/saveProblem';
import {todayIso, WEEK_STARTS_ON} from '../../utils/dates';
import {PatientSummary} from '../forms/PatientSummary';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface KeepRecordPatient {
  patientId: string;
  firstName?: string | null;
  lastName?: string | null;
  dateOfBirth?: string | null;
}

export interface KeepRecordDialogProps {
  /** The patient whose record is being kept; null when closed. */
  patient: KeepRecordPatient | null;
  onDone: () => void;
  onCancel: () => void;
}

/** A date `years` from `iso` (YYYY-MM-DD). */
function yearsFrom(iso: string, years: number): ISODateString {
  const [y, m, d] = iso.split('-');
  return `${Number(y) + years}-${m}-${d}` as ISODateString;
}

/**
 * Keeps a record past its retention period (admins), with a reason and a date to review it again: up to two years
 * ahead, a year by default. It leaves the review list until then. Give it a `key` per patient so it starts fresh.
 */
export function KeepRecordDialog({patient, onDone, onCancel}: KeepRecordDialogProps) {
  return patient ? <KeepRecordForm patient={patient} onDone={onDone} onCancel={onCancel} /> : null;
}

function KeepRecordForm({patient, onDone, onCancel}: {patient: KeepRecordPatient; onDone: () => void; onCancel: () => void}) {
  const keep = useKeepPatientRecord(patient.patientId);
  const today = todayIso();
  const [keepUntil, setKeepUntil] = useState<string>(yearsFrom(today, 1));
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const confirm = () => {
    setProblem(NO_PROBLEM);
    keep.mutate({keepUntil, reason: reason.trim()}, {onSuccess: onDone, onError: (error) => setProblem(saveProblem(error, 'save'))});
  };

  return (
    <ConfirmDialog
      isOpen
      title="Keep Record"
      message="Keep this record past its retention period, for example for a legal hold or ongoing care. It comes back for review on the date you choose."
      icon={AppIcon.Archive}
      confirmText="Keep Record"
      confirmDisabled={!keepUntil || !reason.trim()}
      isProcessing={keep.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {problem.message && <AlertMessage label="Not saved" message={problem.message} />}
        <PatientSummary firstName={patient.firstName} lastName={patient.lastName} dateOfBirth={patient.dateOfBirth} showEmail={false} />
        <DateInput
          label="Review again on"
          description="No more than two years ahead."
          value={(keepUntil || undefined) as ISODateString | undefined}
          onChange={(v) => setKeepUntil(v ?? '')}
          min={yearsFrom(today, 0)}
          max={yearsFrom(today, 2)}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired
          isDisabled={keep.isPending}
          status={fieldStatus(problem.validation, 'keepUntil')}
        />
        <TextArea
          label="Reason"
          value={reason}
          onChange={setReason}
          rows={3}
          isRequired
          isDisabled={keep.isPending}
          status={fieldStatus(problem.validation, 'reason')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {useAnonymisePatient} from '../../api/patients';
import {saveProblem, NO_PROBLEM, type SaveProblem} from '../../pages/saveProblem';
import {PatientSummary} from '../forms/PatientSummary';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';

export interface AnonymisePatient {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  dateOfBirth?: string | null;
}

export interface AnonymiseDialogProps {
  /** The patient to anonymise; null when closed. */
  patient: AnonymisePatient | null;
  onDone: () => void;
  onCancel: () => void;
}

/** The name to type, as staff see it: "First Last". */
export const fullName = (patient: Pick<AnonymisePatient, 'firstName' | 'lastName'>) =>
  [patient.firstName, patient.lastName].filter(Boolean).join(' ').trim();

const same = (a: string, b: string) => a.trim().replace(/\s+/g, ' ').toLowerCase() === b.trim().replace(/\s+/g, ' ').toLowerCase();

/**
 * Anonymises a patient's record (admins): confirmed by typing the patient's full name, with a reason the activity log
 * keeps. Their name, email and notes are replaced with placeholders and their date of birth keeps only its year; the
 * rest stays, for statistics and the audit trail. It can't be undone, and the API refuses it while a procedure is under
 * way or the record is restricted. Give it a `key` per patient so the fields start empty.
 */
export function AnonymiseDialog({patient, onDone, onCancel}: AnonymiseDialogProps) {
  return patient ? <AnonymiseForm patient={patient} onDone={onDone} onCancel={onCancel} /> : null;
}

function AnonymiseForm({patient, onDone, onCancel}: {patient: AnonymisePatient; onDone: () => void; onCancel: () => void}) {
  const anonymise = useAnonymisePatient(patient.id);
  const [reason, setReason] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const name = fullName(patient);
  const isConfirmed = name !== '' && same(confirmation, name);

  const confirm = () => {
    setProblem(NO_PROBLEM);
    anonymise.mutate(
      {reason: reason.trim(), confirmation},
      {onSuccess: onDone, onError: (error) => setProblem(saveProblem(error, 'save'))},
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Anonymise Record"
      message="The patient's name, email and every note are replaced with placeholders, and their date of birth keeps only its year. Procedures, dates and the audit trail stay. This can't be undone."
      isDanger
      confirmText="Anonymise Record"
      confirmDisabled={!isConfirmed || !reason.trim()}
      isProcessing={anonymise.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {problem.message && <AlertMessage message={problem.message} />}
        <PatientSummary firstName={patient.firstName} lastName={patient.lastName} dateOfBirth={patient.dateOfBirth} showEmail={false} />
        <TextArea
          label="Reason"
          description="For example: past its retention period, reviewed and approved."
          value={reason}
          onChange={setReason}
          rows={3}
          isRequired
          isDisabled={anonymise.isPending}
          status={fieldStatus(problem.validation, 'reason')}
        />
        <TextInput
          label={`Type ${name} to confirm`}
          value={confirmation}
          onChange={setConfirmation}
          autoComplete="off"
          isRequired
          isDisabled={anonymise.isPending}
          status={fieldStatus(problem.validation, 'confirmation')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

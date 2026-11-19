import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {ANONYMISATION_REASONS, useAnonymisePatient, type AnonymisationReason} from '../../api/patients';
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
  /** The reason chosen to start with, e.g. past its retention period from the retention review. */
  defaultReason?: AnonymisationReason;
  onDone: () => void;
  onCancel: () => void;
}

/** The name to type, as staff see it: "First Last". */
export const fullName = (patient: Pick<AnonymisePatient, 'firstName' | 'lastName'>) =>
  [patient.firstName, patient.lastName].filter(Boolean).join(' ').trim();

const same = (a: string, b: string) => a.trim().replace(/\s+/g, ' ').toLowerCase() === b.trim().replace(/\s+/g, ' ').toLowerCase();

/** What the dialog says it does: the result is pseudonymised, and still personal data while the ID and dates remain. */
export const ANONYMISE_MESSAGE =
  "The patient's name, email and every note are replaced with placeholders, and their date of birth keeps only its year. " +
  "Attached files' names are replaced and the files deleted. " +
  "The patient's ID, the dates and the outline of their pathways and audit trail are kept, so it's still personal data. " +
  "This can't be undone.";

/**
 * Anonymises (strictly, pseudonymises) a patient's record (admins): confirmed by typing the patient's full name, with a
 * reason chosen from a list (so it holds no patient details) and an optional note, which the activity log keeps. Their
 * name, email and notes are replaced with placeholders, their date of birth keeps only its year, and their attached files'
 * names are replaced and the files deleted; the rest stays, for
 * statistics and the audit trail. It can't be undone, and the API refuses it while a pathway is under way or the
 * record is restricted. It warns that team records keep longer periods than the ones MedApp applies. Give it a
 * `key` per patient so the fields start empty.
 */
export function AnonymiseDialog({patient, defaultReason, onDone, onCancel}: AnonymiseDialogProps) {
  return patient ? <AnonymiseForm patient={patient} defaultReason={defaultReason} onDone={onDone} onCancel={onCancel} /> : null;
}

function AnonymiseForm({
  patient,
  defaultReason,
  onDone,
  onCancel,
}: {
  patient: AnonymisePatient;
  defaultReason?: AnonymisationReason;
  onDone: () => void;
  onCancel: () => void;
}) {
  const anonymise = useAnonymisePatient(patient.id);
  const [reason, setReason] = useState<AnonymisationReason | ''>(defaultReason ?? '');
  const [note, setNote] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const name = fullName(patient);
  const isConfirmed = name !== '' && same(confirmation, name);

  const confirm = () => {
    if (!reason) return;
    setProblem(NO_PROBLEM);
    anonymise.mutate(
      {reason, note: note.trim(), confirmation},
      {onSuccess: onDone, onError: (error) => setProblem(saveProblem(error, 'save'))},
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Anonymise Record"
      message={ANONYMISE_MESSAGE}
      isDanger
      confirmText="Anonymise Record"
      confirmDisabled={!isConfirmed || !reason}
      isProcessing={anonymise.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {problem.message && <AlertMessage label="Record not anonymised" message={problem.message} />}
        <PatientSummary firstName={patient.firstName} lastName={patient.lastName} dateOfBirth={patient.dateOfBirth} showEmail={false} />
        <RadioList
          label="Reason"
          value={reason}
          onChange={(value) => setReason(value as AnonymisationReason)}
          isRequired
          isDisabled={anonymise.isPending}>
          {ANONYMISATION_REASONS.map((r) => (
            <RadioListItem key={r.value} value={r.value} label={r.label} />
          ))}
        </RadioList>
        <TextArea
          label="Note (optional)"
          description="Kept in the activity log after the record is anonymised, so it must hold no patient details."
          value={note}
          onChange={setNote}
          rows={2}
          isDisabled={anonymise.isPending}
          status={fieldStatus(problem.validation, 'note')}
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

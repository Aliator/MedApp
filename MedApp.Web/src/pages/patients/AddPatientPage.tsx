import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useCreatePatient, type PatientInput} from '../../api/patients';
import {FormCard} from '../../components/forms/FormCard';
import {PatientSummary} from '../../components/forms/PatientSummary';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {EMPTY_PATIENT, PATIENT_FIELDS, PatientFormFields} from './PatientFormFields';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/**
 * Add a patient (replaces Blazor's AddPatient): the form, a confirmation showing what will be added, then
 * back to the list. The API validates; its errors go on the fields and in a summary above the form.
 */
export function AddPatientPage() {
  const navigate = useNavigate();
  const create = useCreatePatient();
  const [form, setForm] = useState<PatientInput>(EMPTY_PATIENT);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const add = () => {
    create.mutate(form, {
      onSuccess: () => void navigate({to: '/patients'}),
      onError: (error) => {
        setProblem(saveProblem(error, 'add'));
        setIsConfirming(false);
      },
    });
  };

  return (
    <Page
      title="Add New Patient"
      backHref="/patients"
      backLabel="Back to patients"
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={PATIENT_FIELDS} />
        ) : (
          problem.message && <AlertMessage message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Confirm Add Patient"
          message="Are you sure you want to add this patient to the system?"
          icon={AppIcon.UserPlus}
          confirmText="Confirm Add"
          isProcessing={create.isPending}
          onConfirm={add}
          onCancel={() => setIsConfirming(false)}>
          <PatientSummary {...form} />
        </ConfirmDialog>
      }>
      <FormCard
        saveText="Add Patient"
        onSave={() => {
          setProblem(NO_PROBLEM);
          setIsConfirming(true);
        }}
        onCancel={() => void navigate({to: '/patients'})}
        isProcessing={create.isPending}>
        <PatientFormFields value={form} onChange={setForm} errors={problem.validation} isDisabled={create.isPending} />
      </FormCard>
    </Page>
  );
}

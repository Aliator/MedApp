import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {ApiError} from '../../api/errors';
import {patientQuery, useDeletePatient, useUpdatePatient, type Patient, type PatientInput} from '../../api/patients';
import {patientProcedureCountQuery} from '../../api/procedures';
import {FormCard} from '../../components/forms/FormCard';
import {PatientSummary} from '../../components/forms/PatientSummary';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PatientFormFields} from './PatientFormFields';
import {NO_PROBLEM, saveProblem, type SaveProblem} from './patientErrors';

const toInput = (p: Patient): PatientInput => ({
  firstName: p.firstName ?? '',
  lastName: p.lastName ?? '',
  dateOfBirth: p.dateOfBirth ?? '',
  email: p.email ?? '',
});

const pageProps = {
  title: 'Edit Patient',
  backLabel: 'Back to patient',
} as const;

/**
 * Edit a patient (replaces Blazor's EditPatient). This part loads the patient; an unknown one goes back to
 * the list, as in Blazor. The form only mounts once the patient is here, so it starts from their details.
 */
export function EditPatientPage({id}: {id: string}) {
  const navigate = useNavigate();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: '/patients', replace: true});
  }, [isMissing, navigate]);

  if (patient) return <EditPatientForm id={id} patient={patient} />;

  return (
    <Page
      {...pageProps}
      backHref={`/patients/${id}`}
      isLoading={isPending || isMissing}
      loadingMessage="Loading patient...">
      {error && !isMissing && <AlertMessage message="The patient could not be loaded. Please try again." />}
    </Page>
  );
}

function EditPatientForm({id, patient}: {id: string; patient: Patient}) {
  const navigate = useNavigate();
  const update = useUpdatePatient(id);
  const remove = useDeletePatient(id);
  const [form, setForm] = useState<PatientInput>(() => toInput(patient));
  const [dialog, setDialog] = useState<'save' | 'delete' | null>(null);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const isBusy = update.isPending || remove.isPending;
  // A patient with procedures can't be deleted (their history is kept), so the delete dialog checks first.
  const procedureCount = useQuery({...patientProcedureCountQuery(id), enabled: dialog === 'delete'});
  const hasProcedures = (procedureCount.data ?? 0) > 0;
  const backToPatient = () => void navigate({to: '/patients/$id', params: {id}});

  const save = () =>
    update.mutate(form, {
      onSuccess: backToPatient,
      onError: (e) => {
        setProblem(saveProblem(e, 'save'));
        setDialog(null);
      },
    });

  const deletePatient = () =>
    remove.mutate(undefined, {
      onSuccess: () => void navigate({to: '/patients'}),
      onError: (e) => {
        setProblem(saveProblem(e, 'delete'));
        setDialog(null);
      },
    });

  return (
    <Page
      {...pageProps}
      onBack={backToPatient}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog === 'save'}
            title="Confirm Changes"
            message="Are you sure you want to save these changes?"
            icon={AppIcon.Save}
            confirmText="Confirm Save"
            isProcessing={update.isPending}
            onConfirm={save}
            onCancel={() => setDialog(null)}>
            <PatientSummary {...form} />
          </ConfirmDialog>
          <ConfirmDialog
            isOpen={dialog === 'delete'}
            title="Confirm Delete"
            message={
              procedureCount.isPending
                ? 'Checking for procedures...'
                : hasProcedures
                  ? `This patient has ${procedureCount.data} ${procedureCount.data === 1 ? 'procedure' : 'procedures'}, so they can't be deleted.`
                  : 'This action cannot be undone.'
            }
            isDanger
            confirmText="Delete Patient"
            confirmDisabled={procedureCount.isPending || hasProcedures}
            isProcessing={remove.isPending}
            onConfirm={deletePatient}
            onCancel={() => setDialog(null)}>
            <PatientSummary firstName={patient.firstName} lastName={patient.lastName} showDateOfBirth={false} showEmail={false} />
          </ConfirmDialog>
        </>
      }>
      {problem.validation && <ValidationAlert errors={problem.validation} />}
      {problem.message && <AlertMessage message={problem.message} />}
      <FormCard
        onSave={() => {
          setProblem(NO_PROBLEM);
          setDialog('save');
        }}
        onCancel={backToPatient}
        onDelete={() => setDialog('delete')}
        deleteText="Delete Patient"
        isProcessing={isBusy}>
        <PatientFormFields value={form} onChange={setForm} errors={problem.validation} isDisabled={isBusy} />
      </FormCard>
    </Page>
  );
}

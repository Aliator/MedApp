import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCreatePatient, type PatientInput} from '../../api/patients';
import {FormCard} from '../../components/forms/FormCard';
import {PatientSummary} from '../../components/forms/PatientSummary';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {contactProblems} from './PatientContactFields';
import {EMPTY_PATIENT, PATIENT_FIELDS, PatientFormFields, usePatientLookUp} from './PatientFormFields';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/**
 * Add a patient (replaces Blazor's AddPatient): the form, a confirmation showing what will be added, then
 * back to the list. The API validates; its errors go on the fields and in a summary above the form.
 *
 * From Register Referral (`then="referral"`), for someone not in MedApp yet, it goes back there with the new patient
 * chosen, and Back and Cancel go back there too.
 */
export function AddPatientPage({then}: {then?: 'referral'} = {}) {
  const navigate = useNavigate();
  const create = useCreatePatient();
  const announce = useAnnounce();
  const [form, setForm] = useState<PatientInput>(EMPTY_PATIENT);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const lookUp = usePatientLookUp(form, setForm, 'add');
  const isForReferral = then === 'referral';
  const back = () => void navigate(isForReferral ? {to: '/referrals/new'} : {to: '/patients'});

  const add = () => {
    create.mutate(form, {
      onSuccess: (patient) => {
        announce('Patient added');
        if (isForReferral) void navigate({to: '/referrals/new', search: {patientId: patient.id ?? undefined}});
        else void navigate({to: '/patients'});
      },
      onError: (error) => {
        setProblem(saveProblem(error, 'add'));
        setIsConfirming(false);
      },
    });
  };

  return (
    <Page
      title="Add New Patient"
      backHref={isForReferral ? '/referrals/new' : '/patients'}
      backLabel={isForReferral ? 'Back to Register Referral' : 'Back to patients'}
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={PATIENT_FIELDS} />
        ) : (
          problem.message && <AlertMessage label="Patient not added" message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Add Patient?"
          message="Check the name, date of birth and NHS number first."
          icon={AppIcon.UserPlus}
          confirmText="Add Patient"
          isProcessing={create.isPending}
          onConfirm={add}
          onCancel={() => setIsConfirming(false)}>
          <PatientSummary {...form} />
        </ConfirmDialog>
      }>
      <FormCard
        saveText="Add Patient"
        onSave={() => {
          // Details a look-up filled for another number than the one now entered aren't saved (usePatientLookUp).
          if (lookUp.hold) {
            setProblem({validation: null, message: lookUp.hold});
            return;
          }
          // A postcode or mobile in the wrong format shows on its field before anything is sent (the API checks too).
          const contact = contactProblems(form);
          if (contact) {
            setProblem({validation: contact, message: null});
            return;
          }
          setProblem(NO_PROBLEM);
          setIsConfirming(true);
        }}
        onCancel={back}
        isProcessing={create.isPending}>
        <PatientFormFields value={form} onChange={setForm} lookUp={lookUp} errors={problem.validation} isDisabled={create.isPending} />
      </FormCard>
    </Page>
  );
}

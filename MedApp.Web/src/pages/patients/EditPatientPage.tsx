import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError} from '../../api/errors';
import {patientQuery, useDeletePatient, useUpdatePatient, type Patient, type PatientInput} from '../../api/patients';
import {patientProcedureCountQuery} from '../../api/procedures';
import {FormCard} from '../../components/forms/FormCard';
import {PatientSummary} from '../../components/forms/PatientSummary';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {formatDate} from '../../utils/dates';
import {formatNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {PATIENT_FIELDS, PatientFormFields, usePatientLookUp} from './PatientFormFields';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

const toInput = (p: Patient): PatientInput => ({
  firstName: p.firstName ?? '',
  lastName: p.lastName ?? '',
  dateOfBirth: p.dateOfBirth ?? '',
  email: p.email ?? '',
  nhsNumber: p.nhsNumber ?? '',
});

/** One change to who the record identifies, in words: what it was and what it becomes. */
export interface IdentityChange {
  label: string;
  from: string;
  to: string;
}

const IDENTITY_FIELDS: {key: 'firstName' | 'lastName' | 'dateOfBirth' | 'nhsNumber'; label: string; show: (value: string) => string}[] = [
  {key: 'firstName', label: 'First Name', show: (v) => v.trim() || 'None'},
  {key: 'lastName', label: 'Last Name', show: (v) => v.trim() || 'None'},
  {key: 'dateOfBirth', label: 'Date of Birth', show: (v) => (v ? formatDate(v) : 'None')},
  {key: 'nhsNumber', label: 'NHS Number', show: (v) => (normaliseNhsNumber(v) ? formatNhsNumber(normaliseNhsNumber(v)) : 'Not given')},
];

/** The details that say who a patient is (name, date of birth, NHS number) that a save would change. */
export function identityChanges(saved: PatientInput, form: PatientInput): IdentityChange[] {
  return IDENTITY_FIELDS.map(({key, label, show}) => ({label, from: show(saved[key]), to: show(form[key])})).filter(
    (change) => change.from !== change.to,
  );
}

/** "Old → New" for each change to who the record identifies, in the save confirmation. */
function IdentityChanges({changes}: {changes: IdentityChange[]}) {
  return (
    <VStack gap={2}>
      <Text type="body" weight="semibold">
        Changes to who this record identifies
      </Text>
      <MetadataList>
        {changes.map((change) => (
          <MetadataListItem key={change.label} label={change.label}>
            <HStack gap={1} vAlign="center" wrap="wrap">
              <Text color="secondary">{change.from}</Text>
              <Text aria-hidden>→</Text>
              <VisuallyHidden>changes to</VisuallyHidden>
              <Text weight="semibold">{change.to}</Text>
            </HStack>
          </MetadataListItem>
        ))}
      </MetadataList>
    </VStack>
  );
}

const pageProps = {
  title: 'Edit Patient',
  backLabel: 'Back to patient',
} as const;

/**
 * Edit a patient (replaces Blazor's EditPatient). This part loads the patient; an unknown one goes back to
 * the list, as in Blazor. The form only mounts once the patient is here, so it starts from their details.
 *
 * The patient banner shows the record as saved above the form, whatever is typed, and the save confirmation shows
 * the old and new value of any detail that says who the patient is (wrong-patient hazards H-01 and H-02). An NHS
 * number look-up here never overwrites the details: it shows who it found, to use or not (`usePatientLookUp`).
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
      loadingMessage="Loading patient..."
      message={error && !isMissing && <AlertMessage message="The patient could not be loaded. Please try again." />}
    />
  );
}

function EditPatientForm({id, patient}: {id: string; patient: Patient}) {
  const navigate = useNavigate();
  const update = useUpdatePatient(id);
  const remove = useDeletePatient(id);
  const announce = useAnnounce();
  const [form, setForm] = useState<PatientInput>(() => toInput(patient));
  const [dialog, setDialog] = useState<'save' | 'delete' | null>(null);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const lookUp = usePatientLookUp(form, setForm, 'edit');
  const changes = identityChanges(toInput(patient), form);
  const isBusy = update.isPending || remove.isPending;
  // A patient with procedures can't be deleted (their history is kept), so the delete dialog checks first.
  const procedureCount = useQuery({...patientProcedureCountQuery(id), enabled: dialog === 'delete'});
  const hasProcedures = (procedureCount.data ?? 0) > 0;
  const backToPatient = () => void navigate({to: '/patients/$id', params: {id}});

  const save = () =>
    update.mutate(form, {
      onSuccess: () => {
        announce('Patient saved');
        backToPatient();
      },
      onError: (e) => {
        setProblem(saveProblem(e, 'save'));
        setDialog(null);
      },
    });

  const deletePatient = () =>
    remove.mutate(undefined, {
      onSuccess: () => {
        announce('Patient deleted');
        void navigate({to: '/patients'});
      },
      onError: (e) => {
        setProblem(saveProblem(e, 'delete'));
        setDialog(null);
      },
    });

  return (
    <Page
      {...pageProps}
      onBack={backToPatient}
      // The record as it's saved, so it stays put while the fields below change.
      banner={<PatientBanner patient={patient} />}
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={PATIENT_FIELDS} />
        ) : problem.message ? (
          <AlertMessage message={problem.message} />
        ) : (
          // A restricted or anonymised record can't be changed: the API refuses, so its banners name the state before
          // anyone tries, with why over the page (and a disputed one names what the patient disputes when it's short).
          <PatientFlagBanners patient={patient} />
        )
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog === 'save'}
            title="Confirm Changes"
            message={
              changes.length > 0
                ? "This changes who the record identifies. Check it's the right patient before saving."
                : 'Are you sure you want to save these changes?'
            }
            intent={changes.length > 0 ? 'warning' : undefined}
            icon={AppIcon.Save}
            confirmText="Confirm Save"
            isProcessing={update.isPending}
            onConfirm={save}
            onCancel={() => setDialog(null)}>
            <VStack gap={4}>
              <PatientSummary {...form} />
              {changes.length > 0 && <IdentityChanges changes={changes} />}
            </VStack>
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
            <PatientSummary
              firstName={patient.firstName}
              lastName={patient.lastName}
              nhsNumber={patient.nhsNumber ?? null}
              showDateOfBirth={false}
              showEmail={false}
            />
          </ConfirmDialog>
        </>
      }>
      <FormCard
        onSave={() => {
          // Details a look-up filled for another number than the one now entered aren't saved (usePatientLookUp).
          if (lookUp.hold) {
            setProblem({validation: null, message: lookUp.hold});
            return;
          }
          setProblem(NO_PROBLEM);
          setDialog('save');
        }}
        onCancel={backToPatient}
        onDelete={() => setDialog('delete')}
        deleteText="Delete Patient"
        isProcessing={isBusy}
        // The form scrolls inside its card, under the banner, which stays in view.
        fill>
        <PatientFormFields value={form} onChange={setForm} lookUp={lookUp} errors={problem.validation} isDisabled={isBusy} />
      </FormCard>
    </Page>
  );
}

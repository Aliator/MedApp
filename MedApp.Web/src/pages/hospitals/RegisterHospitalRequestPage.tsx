import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Icon} from '@astryxdesign/core/Icon';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {TimeInput, type ISOTimeString} from '@astryxdesign/core/TimeInput';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {
  hospitalsQuery,
  LAB_REQUEST_PRIORITIES,
  useCreateHospitalRequest,
  useFindHospitalPatients,
  type CreateHospitalRequest,
  type HospitalPatientMatch,
} from '../../api/hospitals';
import {labTestsQuery} from '../../api/labTests';
import {specimenTypesQuery} from '../../api/specimens';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {DateOfBirthInput} from '../../components/forms/DateOfBirthInput';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {NhsNumberField} from '../../components/forms/NhsNumberField';
import {bannerName} from '../../components/patients/PatientBanner';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ukToday} from '../../utils/calendarSlots';
import {formatShortDate, WEEK_STARTS_ON} from '../../utils/dates';
import {formatNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {ukDateTimeToUtc, type UkDateTime} from '../../utils/specimenTimes';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/** The choice that adds a new patient with the request, rather than one of the matches. */
export const NEW_PATIENT = 'new';

/** The fields the form shows, whose errors show on them. */
const FIELDS = [
  'hospitalId',
  'hospitalPatientNumber',
  'nhsNumber',
  'surgeonName',
  'priority',
  'authoredOn',
  'reportNeededBy',
  'collectedAtUtc',
  'specimenTypeId',
  'labTestId',
  'patientId',
  'newPatient.firstName',
  'newPatient.lastName',
  'newPatient.dateOfBirth',
  'newPatient.nhsNumber',
];

interface RequestFields {
  hospitalId: string;
  hospitalPatientNumber: string;
  nhsNumber: string;
  surgeonName: string;
  /** Blank until chosen: MedApp never pre-selects a priority. */
  priority: string;
  isCancerPathway: boolean;
  authored: UkDateTime;
  reportNeededBy: string;
  collected: UkDateTime;
  specimenTypeId: string;
  /** The test the request asks for, from the lab's catalogue. */
  labTestId: string;
}

const EMPTY: RequestFields = {
  hospitalId: '',
  hospitalPatientNumber: '',
  nhsNumber: '',
  surgeonName: '',
  priority: '',
  isCancerPathway: false,
  authored: {date: '', time: ''},
  reportNeededBy: '',
  collected: {date: '', time: ''},
  specimenTypeId: '',
  labTestId: '',
};

interface NewPatient {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  nhsNumber: string;
}

/** A match's line under its name: date of birth and NHS number, to tell people with the same name apart. */
export const matchDescription = (m: HospitalPatientMatch) =>
  [m.dateOfBirth ? `Born ${formatShortDate(m.dateOfBirth)}` : null, m.nhsNumber ? `NHS No. ${formatNhsNumber(m.nhsNumber)}` : 'No NHS number', m.isRestricted ? 'Restricted' : null]
    .filter(Boolean)
    .join(' · ');

/** The request as the API takes it, from the form (times typed as UK days and clock times). */
export function requestBody(fields: RequestFields, choice: string, newPatient: NewPatient): CreateHospitalRequest {
  return {
    hospitalId: fields.hospitalId,
    patientId: choice && choice !== NEW_PATIENT ? choice : null,
    newPatient:
      choice === NEW_PATIENT
        ? {
            firstName: newPatient.firstName.trim(),
            lastName: newPatient.lastName.trim(),
            dateOfBirth: newPatient.dateOfBirth,
            nhsNumber: normaliseNhsNumber(newPatient.nhsNumber) || null,
          }
        : undefined,
    hospitalPatientNumber: fields.hospitalPatientNumber.trim(),
    nhsNumber: normaliseNhsNumber(fields.nhsNumber) || null,
    surgeonName: fields.surgeonName.trim(),
    priority: fields.priority || null,
    isCancerPathway: fields.isCancerPathway,
    authoredOn: ukDateTimeToUtc(fields.authored) ?? '',
    reportNeededBy: fields.reportNeededBy || null,
    collectedAtUtc: ukDateTimeToUtc(fields.collected),
    specimenTypeId: fields.specimenTypeId,
    labTestId: fields.labTestId,
  };
}

/** A UK day and a 24-hour clock time side by side, for when something happened (not in the future). */
function DayAndTime({
  label,
  value,
  onChange,
  isRequired = false,
  status,
}: {
  label: string;
  value: UkDateTime;
  onChange: (value: UkDateTime) => void;
  isRequired?: boolean;
  status?: ReturnType<typeof fieldStatus>;
}) {
  return (
    <HStack gap={3} vAlign="start" wrap="wrap">
      <StackItem size="fill">
        <DateInput
          label={`${label} on`}
          description="UK time."
          value={(value.date || undefined) as ISODateString | undefined}
          onChange={(date) => onChange({...value, date: date ?? ''})}
          max={ukToday() as ISODateString}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired={isRequired}
          isOptional={!isRequired}
          status={status}
        />
      </StackItem>
      <StackItem size="fill">
        <TimeInput
          label={`${label} at`}
          description="24-hour clock."
          value={(value.time || undefined) as ISOTimeString | undefined}
          onChange={(time) => onChange({...value, time: time ?? ''})}
          hourFormat="24h"
          isRequired={isRequired}
          isOptional={!isRequired}
        />
      </StackItem>
    </HStack>
  );
}

/**
 * Register Hospital Request (`/hospital-requests/new`, holders of ProcessSpecimens in the lab; MC18): a hospital's
 * request for the lab, typed in from the request as it came, shaped on FHIR UK Core `ServiceRequest-Lab` and `Specimen`:
 * the hospital, its patient number, the NHS number, the surgeon (as written), the priority and whether it's a cancer
 * pathway (as the request gives them: nothing is pre-selected), when it was made, when the report is needed by
 * (optional: without one, the lab's limit applies), when the sample was collected and its type.
 *
 * **The patient** is found by the NHS number, or the last name and date of birth, and **chosen by the person** from the
 * matches, each with their date of birth and NHS number; or added as a new patient. Nothing is matched automatically,
 * even when there's one match (hazard H-72). Saving opens the request's lab case, numbered from the lab's sequence.
 */
export function RegisterHospitalRequestPage() {
  const navigate = useNavigate();
  const announce = useAnnounce();
  const hospitals = useQuery(hospitalsQuery(false));
  const types = useQuery(specimenTypesQuery(false));
  const tests = useQuery(labTestsQuery(false));
  const create = useCreateHospitalRequest();
  const find = useFindHospitalPatients();
  const [fields, setFields] = useState<RequestFields>(EMPTY);
  const [lastName, setLastName] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState<string | undefined>();
  const [matches, setMatches] = useState<HospitalPatientMatch[] | null>(null);
  const [choice, setChoice] = useState('');
  const [newPatient, setNewPatient] = useState<NewPatient>({firstName: '', lastName: '', dateOfBirth: '', nhsNumber: ''});
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [findProblem, setFindProblem] = useState<SaveProblem>(NO_PROBLEM);
  const set = <K extends keyof RequestFields>(key: K, value: RequestFields[K]) => setFields((f) => ({...f, [key]: value}));
  const errors = problem.validation;
  const backToList = () => void navigate({to: '/hospital-requests'});

  const findMatches = () => {
    setFindProblem(NO_PROBLEM);
    find.mutate(
      {nhsNumber: normaliseNhsNumber(fields.nhsNumber) || null, lastName: lastName.trim() || null, dateOfBirth: dateOfBirth ?? null},
      {
        onSuccess: (found) => {
          setMatches(found);
          // A new search clears the choice: the person chooses again from what it found.
          setChoice('');
          announce(`${found.length} ${found.length === 1 ? 'match' : 'matches'} found`);
        },
        onError: (e) => setFindProblem(saveProblem(e, 'save', 'hospital request')),
      },
    );
  };

  const chooseNew = (value: string) => {
    setChoice(value);
    if (value === NEW_PATIENT)
      setNewPatient((p) => ({
        firstName: p.firstName,
        lastName: p.lastName || lastName,
        dateOfBirth: p.dateOfBirth || dateOfBirth || '',
        nhsNumber: p.nhsNumber || fields.nhsNumber,
      }));
  };

  const save = () => {
    setProblem(NO_PROBLEM);
    create.mutate(requestBody(fields, choice, newPatient), {
      onSuccess: (request) => {
        announce(`Hospital request registered: lab case ${request.labCaseNumber ?? ''}`);
        void navigate({to: '/hospital-requests/$id', params: {id: request.id ?? ''}});
      },
      onError: (e) => setProblem(saveProblem(e, 'add', 'hospital request')),
    });
  };

  return (
    <Page
      title="Register Hospital Request"
      backHref="/hospital-requests"
      backLabel="Back to hospital requests"
      fill
      message={errors ? <ValidationAlert errors={errors} fields={FIELDS} /> : problem.message && <AlertMessage label="Request not registered" message={problem.message} />}>
      <FormCard fill saveText="Register Request" onSave={save} onCancel={backToList} isProcessing={create.isPending}>
        <FormSection title="Request">
          <AnchoredSelector
            label="Hospital"
            placeholder={hospitals.isPending ? 'Loading hospitals...' : 'Choose the hospital'}
            options={(hospitals.data ?? []).map((h) => ({value: h.id ?? '', label: h.name ?? ''}))}
            value={fields.hospitalId || undefined}
            onChange={(v: string) => set('hospitalId', v)}
            isRequired
            status={fieldStatus(errors, 'hospitalId')}
          />
          <TextInput
            label="Hospital number"
            description="As on the request."
            value={fields.hospitalPatientNumber}
            onChange={(v) => set('hospitalPatientNumber', v)}
            autoComplete="off"
            isRequired
            status={fieldStatus(errors, 'hospitalPatientNumber')}
          />
          <NhsNumberField value={fields.nhsNumber} onChange={(v) => set('nhsNumber', v)} status={fieldStatus(errors, 'nhsNumber')} />
          <TextInput
            label="Surgeon"
            description="The name as written on the request."
            value={fields.surgeonName}
            onChange={(v) => set('surgeonName', v)}
            autoComplete="off"
            isRequired
            status={fieldStatus(errors, 'surgeonName')}
          />
          <AnchoredSelector
            label="Priority"
            description="As the request gives it."
            placeholder="Choose the priority on the request"
            options={LAB_REQUEST_PRIORITIES.map((p) => ({value: p.value, label: p.label}))}
            value={fields.priority || undefined}
            onChange={(v: string) => set('priority', v)}
            isRequired
            status={fieldStatus(errors, 'priority')}
          />
          <CheckboxInput
            label="The request says the patient is on the hospital's urgent cancer pathway"
            value={fields.isCancerPathway}
            onChange={(v: boolean) => set('isCancerPathway', v)}
          />
        </FormSection>
        <FormSection title="Dates" useGrid={false}>
          <DayAndTime label="Request made" value={fields.authored} onChange={(v) => set('authored', v)} isRequired status={fieldStatus(errors, 'authoredOn')} />
          <DateInput
            label="Report needed by"
            description="The date on the request, if it gives one. Without one, the lab's own limit applies."
            value={(fields.reportNeededBy || undefined) as ISODateString | undefined}
            onChange={(v) => set('reportNeededBy', v ?? '')}
            weekStartsOn={WEEK_STARTS_ON}
            isOptional
            status={fieldStatus(errors, 'reportNeededBy')}
          />
          <DayAndTime label="Sample collected" value={fields.collected} onChange={(v) => set('collected', v)} status={fieldStatus(errors, 'collectedAtUtc')} />
        </FormSection>
        <FormSection title="Sample">
          <AnchoredSelector
            label="Type of sample"
            placeholder={types.isPending ? 'Loading types...' : 'Choose the type on the request'}
            options={(types.data ?? []).map((t) => ({value: t.id ?? '', label: t.snomedCode ? `${t.name} (SNOMED CT ${t.snomedCode})` : (t.name ?? '')}))}
            value={fields.specimenTypeId || undefined}
            onChange={(v: string) => set('specimenTypeId', v)}
            isRequired
            status={fieldStatus(errors, 'specimenTypeId')}
          />
          <AnchoredSelector
            label="Test"
            placeholder={tests.isPending ? 'Loading tests...' : 'Choose the test the request asks for'}
            options={(tests.data ?? []).map((t) => ({value: t.id ?? '', label: t.name ?? ''}))}
            value={fields.labTestId || undefined}
            onChange={(v: string) => set('labTestId', v)}
            isRequired
            status={fieldStatus(errors, 'labTestId')}
          />
        </FormSection>
        <FormSection title="Patient" useGrid={false}>
          <VStack gap={3} hAlign="stretch">
            <Text type="body">
              Find the patient by the NHS number above, or their last name and date of birth, and choose them yourself: MedApp never matches a
              patient for you. Check the date of birth and NHS number against the request.
            </Text>
            <HStack gap={3} vAlign="end" wrap="wrap">
              <StackItem size="fill">
                <TextInput label="Last name" value={lastName} onChange={setLastName} autoComplete="off" isOptional />
              </StackItem>
              <StackItem size="fill">
                <DateOfBirthInput value={dateOfBirth} onChange={(v) => setDateOfBirth(v)} isRequired={false} showAge={false} />
              </StackItem>
              <Button label="Find Matches" variant="secondary" icon={<Icon icon={AppIcon.Search} />} onClick={findMatches} isLoading={find.isPending} />
            </HStack>
            {findProblem.validation && <ValidationAlert errors={findProblem.validation} />}
            {findProblem.message && <AlertMessage label="Search failed" message={findProblem.message} />}
            {matches !== null && (
              <RadioList label="The patient" value={choice} onChange={chooseNew} isRequired status={fieldStatus(errors, 'patientId')}>
                {[
                  ...matches.map((m) => (
                    <RadioListItem key={m.patientId} value={m.patientId ?? ''} label={bannerName(m)} description={matchDescription(m)} />
                  )),
                  <RadioListItem
                    key={NEW_PATIENT}
                    value={NEW_PATIENT}
                    label="Add a new patient"
                    description={matches.length === 0 ? 'No one matched: add them with this request.' : 'None of these is the patient on the request.'}
                  />,
                ]}
              </RadioList>
            )}
            {matches === null && fieldStatus(errors, 'patientId') && <AlertMessage label="Check the patient" message={fieldStatus(errors, 'patientId')!.message ?? ''} />}
            {choice === NEW_PATIENT && (
              <VStack gap={2} hAlign="stretch">
                <HStack gap={3} vAlign="start" wrap="wrap">
                  <StackItem size="fill">
                    <TextInput
                      label="First name"
                      value={newPatient.firstName}
                      onChange={(v) => setNewPatient((p) => ({...p, firstName: v}))}
                      autoComplete="off"
                      isRequired
                      status={fieldStatus(errors, 'newPatient.firstName')}
                    />
                  </StackItem>
                  <StackItem size="fill">
                    <TextInput
                      label="Last name"
                      value={newPatient.lastName}
                      onChange={(v) => setNewPatient((p) => ({...p, lastName: v}))}
                      autoComplete="off"
                      isRequired
                      status={fieldStatus(errors, 'newPatient.lastName')}
                    />
                  </StackItem>
                </HStack>
                <HStack gap={3} vAlign="start" wrap="wrap">
                  <StackItem size="fill">
                    <DateOfBirthInput
                      value={newPatient.dateOfBirth || undefined}
                      onChange={(v) => setNewPatient((p) => ({...p, dateOfBirth: v ?? ''}))}
                      status={fieldStatus(errors, 'newPatient.dateOfBirth')}
                    />
                  </StackItem>
                  <StackItem size="fill">
                    <NhsNumberField
                      value={newPatient.nhsNumber}
                      onChange={(v) => setNewPatient((p) => ({...p, nhsNumber: v}))}
                      status={fieldStatus(errors, 'newPatient.nhsNumber')}
                    />
                  </StackItem>
                </HStack>
              </VStack>
            )}
          </VStack>
        </FormSection>
      </FormCard>
    </Page>
  );
}

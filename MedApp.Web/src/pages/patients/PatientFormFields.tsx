import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import type {InputStatus} from '@astryxdesign/core/Field';
import {GridSpan} from '@astryxdesign/core/Grid';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useEffect, useRef, useState, type Dispatch, type SetStateAction} from 'react';
import {useAnnounce} from '../../app/announcements';
import {demographicsStatusQuery, useLookUpDemographics, type DemographicsResult} from '../../api/demographics';
import {ApiError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import type {PatientInput} from '../../api/patients';
import type {PracticeSummary} from '../../api/practices';
import {DateOfBirthInput} from '../../components/forms/DateOfBirthInput';
import {FormSection} from '../../components/forms/FormSection';
import {NhsNumberField} from '../../components/forms/NhsNumberField';
import {AppIcon} from '../../components/shared/icons';
import {formatDate} from '../../utils/dates';
import {formatMobile} from '../../utils/contactDetails';
import {formatNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {PatientContactFields, PatientPracticeField, PatientReminderField} from './PatientContactFields';

export const EMPTY_PATIENT: PatientInput = {
  firstName: '',
  lastName: '',
  dateOfBirth: '',
  email: '',
  nhsNumber: '',
  addressLines: ['', '', ''],
  postcode: '',
  mobilePhone: '',
  practiceId: '',
  unlistedPracticeName: '',
  unlistedPracticeAddressLines: ['', '', ''],
  unlistedPracticePostcode: '',
  unlistedPracticePhone: '',
  reminderChannel: 'None',
};

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const PATIENT_FIELDS = Object.keys(EMPTY_PATIENT);

const NOT_FOUND = 'Nobody with this NHS number was found. Check the number, or enter their details yourself.';
const LOOK_UP_FAILED = "The NHS number couldn't be looked up. Please try again, or enter their details yourself.";

/** What a look-up found, for the number it was looked up by (spaces taken out). */
type Found = DemographicsResult & {nhsNumber: string};

/** The details a look-up put in the form, and the number they came from. */
interface Filled {
  nhsNumber: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
}

/** "Ada Synthetic, born 17 May 1980", for saying who a look-up found or who the form has now. */
const describePerson = (p: {firstName?: string | null; lastName?: string | null; dateOfBirth?: string | null}) => {
  const name = [p.firstName, p.lastName].filter((part) => part?.trim()).join(' ') || 'No name';
  return `${name}, ${p.dateOfBirth ? `born ${formatDate(p.dateOfBirth)}` : 'no date of birth'}`;
};

const fromSource = (source: string | null | undefined) => (source ? ` from ${source}` : '');

/**
 * The contact details a look-up found, as the form holds them: only those the service has (an address, a postcode, a
 * mobile), so a look-up never blanks what's already typed.
 */
export function foundContact(person: DemographicsResult): Partial<Pick<PatientInput, 'addressLines' | 'postcode' | 'mobilePhone'>> {
  const lines = (person.addressLines ?? []).map((line) => line.trim()).filter(Boolean);
  return {
    ...(lines.length > 0 && {addressLines: [lines[0] ?? '', lines[1] ?? '', lines[2] ?? ''] as PatientInput['addressLines']}),
    ...(person.postcode && {postcode: person.postcode}),
    ...(person.mobilePhone && {mobilePhone: formatMobile(person.mobilePhone)}),
  };
}

/** The NHS number look-up a patient form offers, and what it has found or filled (see `usePatientLookUp`). */
export interface PatientLookUp {
  /** Whether look-ups are set up (the demographics service), so the form offers one. */
  isAvailable: boolean;
  isLookingUp: boolean;
  lookUp: (typed: string) => void;
  /** What to show under the NHS number: what the look-up said, or that the number no longer matches the details. */
  note?: InputStatus;
  /** On Edit Patient, who a look-up found, waiting for "Use these details". */
  found: Found | null;
  useFound: () => void;
  dismissFound: () => void;
  /**
   * Why the form can't be saved yet, or null: the name and date of birth came from a look-up of another number than
   * the one now entered. It clears when the new number is looked up, or when the person changes any of those details.
   */
  hold: string | null;
}

/**
 * The NHS number look-up for a patient form. On Add Patient (`add`), a look-up fills the name and date of birth
 * straight away. On Edit Patient (`edit`), it never overwrites them: it shows who it found beside who the form has
 * now, and fills them only on "Use these details". Either way, it remembers what it filled and from which number, and
 * if the number is then changed while those details are still in the form, it warns on the number and holds the save
 * (`hold`), so one person's details are never saved under another's number. It announces "filled in" only when it
 * filled something.
 */
export function usePatientLookUp(form: PatientInput, setForm: Dispatch<SetStateAction<PatientInput>>, mode: 'add' | 'edit'): PatientLookUp {
  const announce = useAnnounce();
  const status = useQuery(demographicsStatusQuery());
  const lookUpMutation = useLookUpDemographics();
  // What the look-up said, for the number it said it about.
  const [result, setResult] = useState<{nhsNumber: string; status: InputStatus} | null>(null);
  const [found, setFound] = useState<Found | null>(null);
  const [filled, setFilled] = useState<Filled | null>(null);
  // The form as it is now, for an answer that comes back after it has changed.
  const latest = useRef(form);
  useEffect(() => {
    latest.current = form;
  });

  const current = normaliseNhsNumber(form.nhsNumber);
  const isUnchanged = (nhsNumber: string) => normaliseNhsNumber(latest.current.nhsNumber) === nhsNumber;

  const tell = (nhsNumber: string, next: InputStatus) => {
    setResult({nhsNumber, status: next});
    announce(next.message ?? '');
  };

  const fill = (person: Found) => {
    const values: Filled = {
      nhsNumber: person.nhsNumber,
      firstName: person.firstName ?? latest.current.firstName,
      lastName: person.lastName ?? latest.current.lastName,
      dateOfBirth: person.dateOfBirth ?? latest.current.dateOfBirth,
    };
    const contact = foundContact(person);
    setForm((now) => ({...now, firstName: values.firstName, lastName: values.lastName, dateOfBirth: values.dateOfBirth, ...contact}));
    setFilled(values);
    const what = Object.keys(contact).length > 0 ? 'name, date of birth and contact details' : 'name and date of birth';
    tell(person.nhsNumber, {
      type: 'success',
      message: `Found: ${what} filled in${fromSource(person.source)}. Check they're right.`,
    });
  };

  const lookUp = (typed: string) => {
    const nhsNumber = normaliseNhsNumber(typed);
    setResult(null);
    setFound(null);
    lookUpMutation.mutate(nhsNumber, {
      onSuccess: (person) => {
        // The number was changed while it was being looked up: the answer is about someone else, so it's dropped.
        if (!isUnchanged(nhsNumber)) return;
        if (mode === 'add') {
          fill({...person, nhsNumber});
          return;
        }
        setFound({...person, nhsNumber});
        tell(nhsNumber, {
          type: 'success',
          message: `Found ${describePerson(person)}. Compare them with the details below before using them.`,
        });
      },
      onError: (error) => {
        if (!isUnchanged(nhsNumber)) return;
        tell(nhsNumber, {
          type: 'warning',
          message: error instanceof ApiError && error.status === 404 ? (error.title ?? NOT_FOUND) : problemMessage(error, LOOK_UP_FAILED),
        });
      },
    });
  };

  const shownFound = found && found.nhsNumber === current ? found : null;
  // Still the details a look-up filled, under a number that isn't the one they came from.
  const isHeld =
    filled !== null &&
    current !== '' &&
    current !== filled.nhsNumber &&
    form.firstName === filled.firstName &&
    form.lastName === filled.lastName &&
    form.dateOfBirth === filled.dateOfBirth;
  const from = filled ? formatNhsNumber(filled.nhsNumber) : '';

  return {
    isAvailable: status.data?.isAvailable === true,
    isLookingUp: lookUpMutation.isPending,
    lookUp,
    note: isHeld
      ? {type: 'warning', message: `The name and date of birth were filled in from ${from}. Look the new number up again, or check the details.`}
      : result && result.nhsNumber === current
        ? result.status
        : undefined,
    found: shownFound,
    useFound: () => {
      if (!shownFound) return;
      fill(shownFound);
      setFound(null);
    },
    dismissFound: () => {
      setFound(null);
      setResult(null);
    },
    hold: isHeld
      ? `Not saved: the name and date of birth were filled in from NHS number ${from}, not the number now entered. Look the new number up again, or change the details yourself.`
      : null,
  };
}

/** A change to the form: its new value, or how to get it from the latest one (as React's state setters take). */
export type PatientFormUpdate = PatientInput | ((current: PatientInput) => PatientInput);

export interface PatientFormFieldsProps {
  value: PatientInput;
  onChange: (update: PatientFormUpdate) => void;
  /** The page's NHS number look-up (`usePatientLookUp`). */
  lookUp: PatientLookUp;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /** On Edit Patient, the practice the patient has now, offered (marked archived) even if it's archived since. */
  currentPractice?: PracticeSummary | null;
}

/**
 * The patient fields shared by Add and Edit: personal details (with the NHS number), then contact details, as in
 * the Blazor forms, with the mobile and postal address (`PatientContactFields`), then the GP practice (`PatientPracticeField`). When NHS number look-ups are set up (the national demographics service), the NHS number has a
 * Look up button (see `usePatientLookUp`); on Edit Patient, who it found shows under the personal details, beside
 * who the form has now, to use or not.
 */
export function PatientFormFields({value, onChange, lookUp, errors, isDisabled = false, currentPractice}: PatientFormFieldsProps) {
  const set = <K extends keyof PatientInput>(key: K, next: PatientInput[K]) => onChange((current) => ({...current, [key]: next}));
  const found = lookUp.found;

  return (
    <>
      <FormSection title="Personal Information">
        <TextInput
          label="First Name"
          value={value.firstName}
          onChange={(v) => set('firstName', v)}
          placeholder="Enter first name"
          autoComplete="off"
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'firstName')}
        />
        <TextInput
          label="Last Name"
          value={value.lastName}
          onChange={(v) => set('lastName', v)}
          placeholder="Enter last name"
          autoComplete="off"
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'lastName')}
        />
        <DateOfBirthInput
          value={value.dateOfBirth || undefined}
          onChange={(v) => set('dateOfBirth', v ?? '')}
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'dateOfBirth')}
        />
        <NhsNumberField
          value={value.nhsNumber}
          onChange={(v) => set('nhsNumber', v)}
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'nhsNumber')}
          note={lookUp.note}
          onLookUp={lookUp.isAvailable ? lookUp.lookUp : undefined}
          isLookingUp={lookUp.isLookingUp}
        />
        {found && (
          <GridSpan columns="full">
            <Banner
              status="info"
              title={`Found${fromSource(found.source)}: ${describePerson(found)}`}
              description={`This record now: ${describePerson(value)}`}
              endContent={
                <HStack gap={2} wrap="wrap">
                  <Button label="Keep Current Details" variant="ghost" isDisabled={isDisabled} onClick={lookUp.dismissFound} />
                  <Button
                    label="Use These Details"
                    variant="secondary"
                    icon={<Icon icon={AppIcon.Check} />}
                    isDisabled={isDisabled}
                    onClick={lookUp.useFound}
                  />
                </HStack>
              }
            />
          </GridSpan>
        )}
      </FormSection>
      <FormSection title="Contact Information" useGrid={false}>
        <TextInput
          label="Email Address"
          type="email"
          value={value.email}
          onChange={(v) => set('email', v)}
          placeholder="patient@example.com"
          startIcon={AppIcon.Email}
          autoComplete="off"
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'email')}
        />
        <PatientContactFields value={value} onChange={onChange} errors={errors} isDisabled={isDisabled} />
        <PatientReminderField value={value} onChange={onChange} errors={errors} isDisabled={isDisabled} />
      </FormSection>
      <FormSection title="Registered GP Practice" useGrid={false}>
        <PatientPracticeField value={value} onChange={onChange} errors={errors} isDisabled={isDisabled} currentPractice={currentPractice} />
      </FormSection>
    </>
  );
}

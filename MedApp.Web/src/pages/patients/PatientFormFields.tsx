import type {InputStatus} from '@astryxdesign/core/Field';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {demographicsStatusQuery, useLookUpDemographics, type DemographicsResult} from '../../api/demographics';
import {ApiError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import type {PatientInput} from '../../api/patients';
import {DateOfBirthInput} from '../../components/forms/DateOfBirthInput';
import {FormSection} from '../../components/forms/FormSection';
import {NhsNumberField} from '../../components/forms/NhsNumberField';
import {AppIcon} from '../../components/shared/icons';
import {normaliseNhsNumber} from '../../utils/nhsNumber';

export const EMPTY_PATIENT: PatientInput = {firstName: '', lastName: '', dateOfBirth: '', email: '', nhsNumber: ''};

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const PATIENT_FIELDS = Object.keys(EMPTY_PATIENT);

/** A change to the form: its new value, or how to get it from the latest one (as React's state setters take). */
export type PatientFormUpdate = PatientInput | ((current: PatientInput) => PatientInput);

export interface PatientFormFieldsProps {
  value: PatientInput;
  onChange: (update: PatientFormUpdate) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
}

/** What a look-up said, for the number it was for (so it goes once the number changes). */
interface LookUpNote {
  nhsNumber: string;
  status: InputStatus;
}

const NOT_FOUND = 'Nobody with this NHS number was found. Check the number, or enter their details yourself.';
const LOOK_UP_FAILED = "The NHS number couldn't be looked up. Please try again, or enter their details yourself.";

const foundMessage = (person: DemographicsResult) =>
  `Found: name and date of birth filled in${person.source ? ` from ${person.source}` : ''}. Check they're right.`;

/**
 * The patient fields shared by Add and Edit: personal details (with the NHS number), then contact details, as in
 * the Blazor forms. When NHS number look-ups are set up (the national demographics service), the NHS number has a
 * Look up button that fills in the name and date of birth from it, keeping the number, and says what it found under
 * the field (and to screen readers).
 */
export function PatientFormFields({value, onChange, errors, isDisabled = false}: PatientFormFieldsProps) {
  const set = <K extends keyof PatientInput>(key: K, next: PatientInput[K]) => onChange((current) => ({...current, [key]: next}));
  const announce = useAnnounce();
  const lookUpStatus = useQuery(demographicsStatusQuery());
  const lookUp = useLookUpDemographics();
  const [note, setNote] = useState<LookUpNote | null>(null);

  const tell = (nhsNumber: string, status: InputStatus) => {
    setNote({nhsNumber, status});
    announce(status.message ?? '');
  };

  const lookUpNumber = (typed: string) => {
    const nhsNumber = normaliseNhsNumber(typed);
    setNote(null);
    lookUp.mutate(nhsNumber, {
      onSuccess: (person) => {
        // Only if the number is still the one looked up: someone may have changed it while it was being looked up.
        onChange((current) =>
          normaliseNhsNumber(current.nhsNumber) === nhsNumber
            ? {
                ...current,
                firstName: person.firstName ?? current.firstName,
                lastName: person.lastName ?? current.lastName,
                dateOfBirth: person.dateOfBirth ?? current.dateOfBirth,
              }
            : current,
        );
        tell(nhsNumber, {type: 'success', message: foundMessage(person)});
      },
      onError: (error) =>
        tell(nhsNumber, {
          type: 'warning',
          message: error instanceof ApiError && error.status === 404 ? (error.title ?? NOT_FOUND) : problemMessage(error, LOOK_UP_FAILED),
        }),
    });
  };

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
          note={note && normaliseNhsNumber(value.nhsNumber) === note.nhsNumber ? note.status : undefined}
          onLookUp={lookUpStatus.data?.isAvailable ? lookUpNumber : undefined}
          isLookingUp={lookUp.isPending}
        />
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
      </FormSection>
    </>
  );
}

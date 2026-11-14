import {TextInput} from '@astryxdesign/core/TextInput';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import type {PatientInput} from '../../api/patients';
import {DateOfBirthInput} from '../../components/forms/DateOfBirthInput';
import {FormSection} from '../../components/forms/FormSection';
import {AppIcon} from '../../components/shared/icons';

export const EMPTY_PATIENT: PatientInput = {firstName: '', lastName: '', dateOfBirth: '', email: ''};

export interface PatientFormFieldsProps {
  value: PatientInput;
  onChange: (value: PatientInput) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
}

/** The patient fields shared by Add and Edit: personal details, then contact details, as in the Blazor forms. */
export function PatientFormFields({value, onChange, errors, isDisabled = false}: PatientFormFieldsProps) {
  const set = <K extends keyof PatientInput>(key: K, next: PatientInput[K]) => onChange({...value, [key]: next});

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

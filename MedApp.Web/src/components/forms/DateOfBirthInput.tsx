import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import type {InputStatus} from '@astryxdesign/core/Field';
import {ageOn, todayIso, WEEK_STARTS_ON} from '../../utils/dates';

export interface DateOfBirthInputProps {
  value: string | undefined;
  onChange: (value: ISODateString | undefined) => void;
  label?: string;
  isRequired?: boolean;
  isDisabled?: boolean;
  /** Shows the age for the chosen date under the label. */
  showAge?: boolean;
  status?: InputStatus;
}

/**
 * A full date of birth (replaces Blazor's DatePickerFormGroup for add/edit patient, plan Q9). The value is an
 * ISO YYYY-MM-DD string, as the API takes it; dates after today can't be picked.
 */
export function DateOfBirthInput({
  value,
  onChange,
  label = 'Date of Birth',
  isRequired = true,
  isDisabled = false,
  showAge = true,
  status,
}: DateOfBirthInputProps) {
  const age = showAge && value ? ageOn(value) : null;

  return (
    <DateInput
      label={label}
      value={value as ISODateString | undefined}
      onChange={onChange}
      max={todayIso()}
      weekStartsOn={WEEK_STARTS_ON}
      isRequired={isRequired}
      isDisabled={isDisabled}
      description={age !== null ? `Age: ${age} ${age === 1 ? 'year' : 'years'}` : undefined}
      status={status}
    />
  );
}

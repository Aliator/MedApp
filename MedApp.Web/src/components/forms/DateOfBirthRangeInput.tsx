import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateRangeInput, type DateRange} from '@astryxdesign/core/DateRangeInput';
import {todayIso, WEEK_STARTS_ON} from '../../utils/dates';

export interface DateOfBirthRangeInputProps {
  value: DateRange | null;
  onChange: (value: DateRange | null) => void;
  label?: string;
  /** The latest date allowed; today by default (a date of birth). Null allows any date, e.g. for due dates. */
  max?: ISODateString | null;
}

/**
 * Patients born between two dates, both inclusive (replaces Blazor's partial-date search, plan Q9).
 * One month at a time so it fits the search dialog; clearing gives null.
 */
export function DateOfBirthRangeInput({value, onChange, label = 'Date of birth', max = todayIso()}: DateOfBirthRangeInputProps) {
  return (
    <DateRangeInput
      label={label}
      value={value}
      onChange={onChange}
      max={max ?? undefined}
      weekStartsOn={WEEK_STARTS_ON}
      numberOfMonths={1}
      hasClear
    />
  );
}

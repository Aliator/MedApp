import {DateInput} from '@astryxdesign/core/DateInput';
import type {InputStatus} from '@astryxdesign/core/Field';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {TimeInput, type ISOTimeString} from '@astryxdesign/core/TimeInput';
import {MAX_BACKDATING_DAYS} from '../../api/specimens';
import {addDays, ukToday} from '../../utils/calendarSlots';
import {WEEK_STARTS_ON} from '../../utils/dates';
import type {UkDateTime} from '../../utils/specimenTimes';

export interface StepTimeFieldsProps {
  /** What happened, for the fields' labels: "Taken" gives "Taken on" and "Time taken". */
  step: string;
  value: UkDateTime;
  onChange: (value: UkDateTime) => void;
  /** The API's 400 on the time (`takenAtUtc`, `atUtc`), shown under the day. */
  status?: InputStatus;
  isDisabled?: boolean;
}

/**
 * When a specimen's step happened, as a UK day and a 24-hour clock time, side by side: today or up to 7 days back
 * (the API's limit; it also refuses a time in the future, or before the step before, with a 400 shown here). The caller
 * starts it at now, and turns it into UTC with `ukDateTimeToUtc`.
 */
export function StepTimeFields({step, value, onChange, status, isDisabled = false}: StepTimeFieldsProps) {
  const today = ukToday();
  return (
    <HStack gap={3} vAlign="start" wrap="wrap">
      <StackItem size="fill">
        <DateInput
          label={`${step} on`}
          description="UK time, today or up to a week back."
          value={(value.date || undefined) as ISODateString | undefined}
          onChange={(date) => onChange({...value, date: date ?? ''})}
          min={addDays(today, -MAX_BACKDATING_DAYS) as ISODateString}
          max={today as ISODateString}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired
          isDisabled={isDisabled}
          status={status}
        />
      </StackItem>
      <StackItem size="fill">
        <TimeInput
          label={`Time ${step.toLowerCase()}`}
          description="24-hour clock."
          value={(value.time || undefined) as ISOTimeString | undefined}
          onChange={(time) => onChange({...value, time: time ?? ''})}
          hourFormat="24h"
          isRequired
          isDisabled={isDisabled}
        />
      </StackItem>
    </HStack>
  );
}

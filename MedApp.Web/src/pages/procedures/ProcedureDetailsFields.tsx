import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Selector} from '@astryxdesign/core/Selector';
import {TextInput} from '@astryxdesign/core/TextInput';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {PRIORITIES, type ProcedureDetailsInput} from '../../api/procedures';
import {BLANK_DESCRIPTION} from '../../components/forms/fieldSpace';
import {FormSection} from '../../components/forms/FormSection';
import {WEEK_STARTS_ON} from '../../utils/dates';
import {RichTextField} from '../../components/forms/RichTextField';

export const EMPTY_PROCEDURE_DETAILS: ProcedureDetailsInput = {title: '', notes: '', dueDate: '', priority: 'Medium'};

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const PROCEDURE_DETAILS_FIELDS = Object.keys(EMPTY_PROCEDURE_DETAILS);

export interface ProcedureDetailsFieldsProps {
  value: ProcedureDetailsInput;
  onChange: (value: ProcedureDetailsInput) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /** The earliest due date allowed (a new procedure can't be due in the past). */
  minDueDate?: ISODateString;
  /** Shown under the title, e.g. that a blank one takes the template's name. */
  titleDescription?: string;
}

/**
 * A procedure's details, shared by New procedure and Edit details: title, priority and due date, then the notes.
 * The stages aren't here; they come from the template and can't be changed once the procedure exists.
 */
export function ProcedureDetailsFields({
  value,
  onChange,
  errors,
  isDisabled = false,
  minDueDate,
  titleDescription,
}: ProcedureDetailsFieldsProps) {
  const set = <K extends keyof ProcedureDetailsInput>(key: K, next: ProcedureDetailsInput[K]) => onChange({...value, [key]: next});

  return (
    <>
      <FormSection title="Details">
        <TextInput
          label="Title"
          value={value.title}
          onChange={(v) => set('title', v)}
          description={titleDescription}
          autoComplete="off"
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'title')}
        />
        <Selector
          label="Priority"
          // Level with the title's input when the title has a description.
          description={titleDescription ? BLANK_DESCRIPTION : undefined}
          options={[...PRIORITIES]}
          value={value.priority}
          onChange={(v: string) => set('priority', v)}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'priority')}
        />
        <DateInput
          label="Due Date"
          description="Due by the end of the day, UK time."
          value={(value.dueDate || undefined) as ISODateString | undefined}
          onChange={(v) => set('dueDate', v ?? '')}
          min={minDueDate}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'dueDate')}
        />
      </FormSection>
      <FormSection title="Notes" useGrid={false}>
        <RichTextField
          label="Notes"
          isLabelHidden
          value={value.notes}
          onChange={(v) => set('notes', v)}
          placeholder="Anything staff should know about this procedure"
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'notes')}
        />
      </FormSection>
    </>
  );
}

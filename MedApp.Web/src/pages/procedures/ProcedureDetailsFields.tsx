import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Selector} from '@astryxdesign/core/Selector';
import {TextInput} from '@astryxdesign/core/TextInput';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {PRIORITIES, type NewProcedureDetailsInput, type ProcedureDetailsInput} from '../../api/procedures';
import {BLANK_DESCRIPTION} from '../../components/forms/fieldSpace';
import {FormSection} from '../../components/forms/FormSection';
import {WEEK_STARTS_ON} from '../../utils/dates';
import {RichTextField} from '../../components/forms/RichTextField';

export const EMPTY_PROCEDURE_DETAILS: NewProcedureDetailsInput = {title: '', notes: '', dueDate: '', priority: 'Medium'};

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const PROCEDURE_DETAILS_FIELDS = Object.keys(EMPTY_PROCEDURE_DETAILS);

/** The details, and for a new procedure its first note. */
type DetailsValue = ProcedureDetailsInput & {notes?: string};

export interface ProcedureDetailsFieldsProps<T extends DetailsValue> {
  /** With `notes` (a new procedure), a Notes box too, whose text becomes its first note. */
  value: T;
  onChange: (value: T) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /** The earliest due date allowed (a new procedure can't be due in the past). */
  minDueDate?: ISODateString;
  /** Shown under the title, e.g. that a blank one takes the template's name. */
  titleDescription?: string;
}

/**
 * A procedure's details, shared by New procedure and Edit details: title, priority and due date, then for a new one
 * a Notes box, whose text becomes its first note. Once it exists, notes are added and edited one at a time on its
 * page (`ProcedureNotes`), not here. The stages aren't here either; they come from the template and can't be changed
 * once the procedure exists.
 */
export function ProcedureDetailsFields<T extends DetailsValue>({
  value,
  onChange,
  errors,
  isDisabled = false,
  minDueDate,
  titleDescription,
}: ProcedureDetailsFieldsProps<T>) {
  const set = <K extends keyof T>(key: K, next: T[K]) => onChange({...value, [key]: next});

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
      {value.notes !== undefined && (
        <FormSection title="Notes" useGrid={false}>
          <RichTextField
            label="Notes"
            isLabelHidden
            description="Becomes the procedure's first note, by you. More can be added on its page."
            value={value.notes}
            onChange={(v) => set('notes', v as T['notes'])}
            placeholder="Anything staff should know about this procedure"
            isDisabled={isDisabled}
            status={fieldStatus(errors, 'notes')}
          />
        </FormSection>
      )}
    </>
  );
}

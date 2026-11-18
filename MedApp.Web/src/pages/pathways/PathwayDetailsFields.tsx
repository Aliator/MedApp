import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {TextInput} from '@astryxdesign/core/TextInput';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {PRIORITIES, type NewPathwayDetailsInput, type PathwayDetailsInput} from '../../api/pathways';
import {BLANK_DESCRIPTION} from '../../components/forms/fieldSpace';
import {FormSection} from '../../components/forms/FormSection';
import {formatDate, WEEK_STARTS_ON} from '../../utils/dates';
import {RichTextField} from '../../components/forms/RichTextField';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';

export const EMPTY_PATHWAY_DETAILS: NewPathwayDetailsInput = {title: '', notes: '', dueDate: '', priority: 'Medium'};

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const PATHWAY_DETAILS_FIELDS = Object.keys(EMPTY_PATHWAY_DETAILS);

/** The details, and for a new pathway its first note. */
type DetailsValue = PathwayDetailsInput & {notes?: string};

export interface PathwayDetailsFieldsProps<T extends DetailsValue> {
  /** With `notes` (a new pathway), a Notes box too, whose text becomes its first note. */
  value: T;
  onChange: (value: T) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /** The earliest target date allowed (a new pathway can't be due in the past). */
  minDueDate?: ISODateString;
  /** Shown under the title, e.g. that a blank one takes the template's name. */
  titleDescription?: string;
  /**
   * The target date comes from the referral's waiting-time clock (running or paused): it's shown read-only, as the
   * API refuses a different one, and moves with the clock and the priority.
   */
  isDueDateFromClock?: boolean;
}

/** What a target date set by the waiting-time clock says under it. */
export const DUE_DATE_FROM_CLOCK =
  'From the waiting-time clock: it moves with the clock, and with the priority, so it can\'t be changed here.';

/**
 * A pathway's details, shared by New pathway and Edit details: title, priority and target date, then for a new one
 * a Notes box, whose text becomes its first note. Once it exists, notes are added and edited one at a time on its
 * page (`PathwayNotes`), not here. The stages aren't here either; they come from the template and can't be changed
 * once the pathway exists.
 */
export function PathwayDetailsFields<T extends DetailsValue>({
  value,
  onChange,
  errors,
  isDisabled = false,
  minDueDate,
  titleDescription,
  isDueDateFromClock = false,
}: PathwayDetailsFieldsProps<T>) {
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
        <AnchoredSelector
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
        {isDueDateFromClock ? (
          <TextInput
            label="Target Date"
            description={DUE_DATE_FROM_CLOCK}
            value={value.dueDate ? formatDate(value.dueDate) : ''}
            onChange={() => undefined}
            isReadOnly
            status={fieldStatus(errors, 'dueDate')}
          />
        ) : (
          <DateInput
            label="Target Date"
            description="Due by the end of the day, UK time."
            value={(value.dueDate || undefined) as ISODateString | undefined}
            onChange={(v) => set('dueDate', v ?? '')}
            min={minDueDate}
            weekStartsOn={WEEK_STARTS_ON}
            isRequired
            isDisabled={isDisabled}
            status={fieldStatus(errors, 'dueDate')}
          />
        )}
      </FormSection>
      {value.notes !== undefined && (
        <FormSection title="Notes" useGrid={false}>
          {/* Labelled, so its hint shows: a hidden label hides the description with it. */}
          <RichTextField
            label="First note"
            description="Becomes the pathway's first note, by you. More can be added on its page."
            value={value.notes}
            onChange={(v) => set('notes', v as T['notes'])}
            placeholder="Anything staff should know about this pathway"
            isDisabled={isDisabled}
            status={fieldStatus(errors, 'notes')}
          />
        </FormSection>
      )}
    </>
  );
}

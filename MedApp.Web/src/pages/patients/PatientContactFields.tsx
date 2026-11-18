import type {InputStatus} from '@astryxdesign/core/Field';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {REMINDER_CHANNELS, reminderChannelLabel, wantsEmails, wantsTexts} from '../../api/patientMessages';
import {UNLISTED_PRACTICE, type PatientInput} from '../../api/patients';
import {practiceAddress, practiceChoices, practiceLabel, practicesQuery, type Practice, type PracticeSummary} from '../../api/practices';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {isValidMobile, isValidPostcode} from '../../utils/contactDetails';
import type {PatientFormUpdate} from './PatientFormFields';

export const CONTACT_MESSAGES = {
  postcode: 'Enter a UK postcode, such as ZZ1 1ZZ.',
  mobilePhone: 'Enter a UK mobile number: 07 and nine more digits, or +44 7.',
} as const;

/**
 * What's wrong with the form's postcode and mobile, if anything, as the API would say it (it checks both too, and its
 * word is final): each is optional, so blank is fine. Null when both are fine; otherwise errors keyed by field, so a
 * page can show them on their fields before sending anything.
 */
export function contactProblems(input: Pick<PatientInput, 'postcode' | 'mobilePhone'>): ValidationErrors | null {
  const fields: Record<string, string[]> = {};
  if (input.postcode.trim() && !isValidPostcode(input.postcode)) fields.postcode = [CONTACT_MESSAGES.postcode];
  if (input.mobilePhone.trim() && !isValidMobile(input.mobilePhone)) fields.mobilePhone = [CONTACT_MESSAGES.mobilePhone];
  return Object.keys(fields).length > 0 ? {title: 'Check the contact details.', fields, general: []} : null;
}

interface FieldsProps {
  value: PatientInput;
  onChange: (update: PatientFormUpdate) => void;
  /** A 400's errors (or `contactProblems`'), shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
}

/**
 * A patient's mobile and postal address (three lines and a postcode), all optional, inside the patient form's Contact
 * Information. The mobile and postcode are checked once the person leaves the field (`contactProblems`), so a mistyped
 * one shows before saving; the server's own error, after a save, wins.
 */
export function PatientContactFields({value, onChange, errors, isDisabled = false}: FieldsProps) {
  // Which fields the person has left, so a half-typed value isn't called wrong while they type.
  const [left, setLeft] = useState<{postcode?: boolean; mobilePhone?: boolean}>({});
  const own = contactProblems(value);
  const status = (field: 'postcode' | 'mobilePhone'): InputStatus | undefined =>
    fieldStatus(errors, field) ?? (left[field] ? fieldStatus(own, field) : undefined);
  const set = <K extends 'postcode' | 'mobilePhone'>(key: K, next: string) => {
    setLeft((current) => ({...current, [key]: false}));
    onChange((current) => ({...current, [key]: next}));
  };
  const setLine = (index: number, next: string) =>
    onChange((current) => {
      const addressLines: PatientInput['addressLines'] = [...current.addressLines];
      addressLines[index] = next;
      return {...current, addressLines};
    });

  return (
    <>
      <TextInput
        label="Mobile"
        description="A UK mobile, e.g. 07700 900123."
        type="tel"
        inputMode="tel"
        value={value.mobilePhone}
        onChange={(next) => set('mobilePhone', next)}
        onBlur={() => setLeft((current) => ({...current, mobilePhone: true}))}
        autoComplete="off"
        isOptional
        isDisabled={isDisabled}
        status={status('mobilePhone')}
      />
      {([0, 1, 2] as const).map((index) => (
        <TextInput
          key={index}
          label={`Address line ${index + 1}`}
          value={value.addressLines[index]}
          onChange={(next) => setLine(index, next)}
          autoComplete="off"
          isOptional
          isDisabled={isDisabled}
          // The API names the lines as one field.
          status={index === 0 ? fieldStatus(errors, 'addressLines') : undefined}
        />
      ))}
      <TextInput
        label="Postcode"
        value={value.postcode}
        onChange={(next) => set('postcode', next)}
        onBlur={() => setLeft((current) => ({...current, postcode: true}))}
        autoComplete="off"
        hasAutoCorrect={false}
        hasSpellCheck={false}
        isOptional
        isDisabled={isDisabled}
        status={status('postcode')}
      />
    </>
  );
}

/** What the reminder choice says under it. */
export const REMINDER_HINT = "Ask the patient. Texts can't be replied to: change it here when they ask.";

/**
 * Why nothing can be sent as the patient chose, or null: texts without a mobile, or emails without an address. It
 * doesn't stop the save; the server stores such a message as not sent, saying why.
 */
export function reminderContactGap(input: Pick<PatientInput, 'reminderChannel' | 'mobilePhone' | 'email'>): string | null {
  const noMobile = wantsTexts(input.reminderChannel) && !input.mobilePhone.trim();
  const noEmail = wantsEmails(input.reminderChannel) && !input.email.trim();
  if (noMobile && noEmail) return 'Nothing can be sent without a mobile or an email address.';
  if (noMobile) return 'Texts can’t be sent without a mobile number.';
  if (noEmail) return 'Emails can’t be sent without an email address.';
  return null;
}

/**
 * How the patient wants appointment reminders and other messages (centre plan decision 133): No reminders, Text,
 * Email, or Text and email. Staff ask the patient, and change it when they ask (texts can't be replied to). Choosing
 * texts without a mobile, or email without an address, shows a gentle warning, but doesn't stop the save.
 */
export function PatientReminderField({value, onChange, errors, isDisabled = false}: FieldsProps) {
  const gap = reminderContactGap(value);
  return (
    <AnchoredSelector
      label="Reminders"
      description={REMINDER_HINT}
      options={REMINDER_CHANNELS.map((channel) => ({value: channel, label: reminderChannelLabel(channel)}))}
      value={value.reminderChannel || 'None'}
      onChange={(next: string) => onChange((current) => ({...current, reminderChannel: next}))}
      isDisabled={isDisabled}
      status={fieldStatus(errors, 'reminderChannel') ?? (gap ? {type: 'warning', message: gap} : undefined)}
    />
  );
}

/** The picker's value for no practice. */
const NO_PRACTICE = 'none';

/** What the picker says under "Not listed here". */
export const UNLISTED_HINT = 'Its details are kept on this patient only, not added to the directory.';

/**
 * The GP practice a patient is registered with, chosen from the directory (`/practices`): "None", then every practice
 * not archived, by name, then "Not listed here". The one the patient has now (`currentPractice`) is offered even if it's
 * archived since, marked so: they keep it, but no one can newly choose it. The chosen practice's address shows under
 * the picker. "Not listed here" asks for the practice's name, address and phone, which are kept on this patient only
 * (`UnlistedPracticeFields`), never added to the directory.
 */
export function PatientPracticeField({value, onChange, errors, isDisabled = false, currentPractice}: FieldsProps & {currentPractice?: PracticeSummary | null}) {
  const practices = useQuery(practicesQuery(true));
  const listed: Practice[] = [...(practices.data ?? [])];
  if (currentPractice?.id && !listed.some((p) => p.id === currentPractice.id)) listed.push(currentPractice);
  // The patient's own practice stays on offer while the form is open, so choosing another can be undone.
  const choices = practiceChoices(listed, currentPractice?.id || value.practiceId);
  const chosen = choices.find((p) => p.id === value.practiceId);
  const isUnlisted = value.practiceId === UNLISTED_PRACTICE;

  return (
    <>
      <AnchoredSelector
        label="Practice"
        description={
          isUnlisted ? UNLISTED_HINT : chosen ? practiceAddress(chosen) || 'No address in the directory.' : 'From the GP practice directory.'
        }
        placeholder={practices.isPending ? 'Loading practices...' : undefined}
        options={[
          {value: NO_PRACTICE, label: 'None'},
          ...choices.map((p) => ({value: p.id ?? '', label: practiceLabel(p)})),
          {value: UNLISTED_PRACTICE, label: 'Not listed here'},
        ]}
        value={value.practiceId || NO_PRACTICE}
        onChange={(next: string) => onChange((current) => ({...current, practiceId: next === NO_PRACTICE ? '' : next}))}
        isOptional
        isDisabled={isDisabled}
        status={fieldStatus(errors, 'practiceId') ?? fieldStatus(errors, 'unlistedPractice')}
      />
      {isUnlisted && <UnlistedPracticeFields value={value} onChange={onChange} errors={errors} isDisabled={isDisabled} />}
    </>
  );
}

/**
 * A GP practice that isn't in the directory: its name (required), address (three lines and a postcode) and phone, kept
 * on this patient only. The API checks them (its errors show on these fields).
 */
export function UnlistedPracticeFields({value, onChange, errors, isDisabled = false}: FieldsProps) {
  const set = <K extends 'unlistedPracticeName' | 'unlistedPracticePostcode' | 'unlistedPracticePhone'>(key: K, next: string) =>
    onChange((current) => ({...current, [key]: next}));
  const setLine = (index: number, next: string) =>
    onChange((current) => {
      const lines: PatientInput['unlistedPracticeAddressLines'] = [...current.unlistedPracticeAddressLines];
      lines[index] = next;
      return {...current, unlistedPracticeAddressLines: lines};
    });

  return (
    <>
      <TextInput
        label="Practice name"
        value={value.unlistedPracticeName}
        onChange={(next) => set('unlistedPracticeName', next)}
        autoComplete="off"
        isRequired
        isDisabled={isDisabled}
        status={fieldStatus(errors, 'unlistedPracticeName')}
      />
      {([0, 1, 2] as const).map((index) => (
        <TextInput
          key={index}
          label={`Practice address line ${index + 1}`}
          value={value.unlistedPracticeAddressLines[index]}
          onChange={(next) => setLine(index, next)}
          autoComplete="off"
          isOptional
          isDisabled={isDisabled}
          status={index === 0 ? fieldStatus(errors, 'unlistedPracticeAddressLines') : undefined}
        />
      ))}
      <TextInput
        label="Practice postcode"
        value={value.unlistedPracticePostcode}
        onChange={(next) => set('unlistedPracticePostcode', next)}
        autoComplete="off"
        hasAutoCorrect={false}
        hasSpellCheck={false}
        isOptional
        isDisabled={isDisabled}
        status={fieldStatus(errors, 'unlistedPracticePostcode')}
      />
      <TextInput
        label="Practice phone"
        type="tel"
        inputMode="tel"
        value={value.unlistedPracticePhone}
        onChange={(next) => set('unlistedPracticePhone', next)}
        autoComplete="off"
        isOptional
        isDisabled={isDisabled}
        status={fieldStatus(errors, 'unlistedPracticePhone')}
      />
    </>
  );
}

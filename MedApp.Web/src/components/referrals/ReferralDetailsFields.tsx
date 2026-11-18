import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {MAX_REASON, REFERRAL_PRIORITIES, type ReferralDetailsInput} from '../../api/referrals';
import {specialtiesQuery} from '../../api/specialties';
import {ukDay, WEEK_STARTS_ON} from '../../utils/dates';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {BLANK_DESCRIPTION} from '../forms/fieldSpace';
import {FormSection} from '../forms/FormSection';

/** The fields shown, whose errors show on them (so the page's banner leaves them out). */
export const REFERRAL_DETAILS_FIELDS: (keyof ReferralDetailsInput)[] = [
  'specialtyId',
  'receivedOn',
  'referrerPractice',
  'referrerClinician',
  'referrerOdsCode',
  'requestedPriority',
  'reason',
];

/** Today, as a UK date (YYYY-MM-DD): a referral received today is the usual case. */
export const ukToday = (now: Date = new Date()) => ukDay(now.toISOString());

/** The same day a year earlier: the oldest a referral can have been received (the API's rule). */
export function aYearBefore(day: string): string {
  const [year, month, date] = day.split('-').map(Number);
  // 29 February a year back is 1 March, which is within the year either way.
  return new Date(Date.UTC(year - 1, month - 1, date)).toISOString().slice(0, 10);
}

/** A new referral's details: received today, Medium priority asked for, the rest blank. */
export const emptyReferralDetails = (today: string = ukToday()): ReferralDetailsInput => ({
  specialtyId: '',
  receivedOn: today,
  referrerPractice: '',
  referrerClinician: '',
  referrerOdsCode: '',
  requestedPriority: 'Medium',
  reason: '',
});

export interface ReferralDetailsFieldsProps {
  value: ReferralDetailsInput;
  onChange: (value: ReferralDetailsInput) => void;
  /** A 400's errors, shown on their fields. */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /**
   * A specialty to keep offering though it's archived (an edit of a referral to it): archived ones can't be newly
   * chosen, but a referral already to one keeps it.
   */
  currentSpecialty?: {id: string; name: string};
}

/**
 * A referral's details, as typed from the referrer's letter, shared by Register Referral and Edit Details: the
 * specialty asked for (not archived), when it was received (a UK date: not in the future, at most a year back), the
 * referring practice, clinician and the practice's ODS code, the priority the referrer asked for, and the reason for
 * referral. The server checks them all; its 400 shows on the fields.
 */
export function ReferralDetailsFields({value, onChange, errors, isDisabled = false, currentSpecialty}: ReferralDetailsFieldsProps) {
  const set = <K extends keyof ReferralDetailsInput>(key: K, next: ReferralDetailsInput[K]) => onChange({...value, [key]: next});
  const specialties = useQuery(specialtiesQuery(false));
  const options = (specialties.data ?? []).map((s) => ({value: s.id ?? '', label: s.name ?? ''}));
  if (currentSpecialty && !options.some((o) => o.value === currentSpecialty.id))
    options.push({value: currentSpecialty.id, label: `${currentSpecialty.name} (archived)`});
  const today = ukToday();

  return (
    <>
      <FormSection title="Referral">
        <AnchoredSelector
          label="Specialty"
          placeholder={specialties.isPending ? 'Loading specialties...' : 'Choose the specialty asked for'}
          options={options}
          value={value.specialtyId || undefined}
          onChange={(v: string) => set('specialtyId', v)}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'specialtyId')}
        />
        <DateInput
          label="Received on"
          description="The day the referral arrived, UK time."
          value={(value.receivedOn || undefined) as ISODateString | undefined}
          onChange={(v) => set('receivedOn', v ?? '')}
          min={aYearBefore(today) as ISODateString}
          max={today as ISODateString}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'receivedOn')}
        />
        <AnchoredSelector
          label="Priority asked for"
          description={BLANK_DESCRIPTION}
          options={[...REFERRAL_PRIORITIES]}
          value={value.requestedPriority}
          onChange={(v: string) => set('requestedPriority', v)}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'requestedPriority')}
        />
      </FormSection>
      <FormSection title="Referrer">
        <TextInput
          label="Practice"
          description="The referring practice or organisation, as on the letter."
          value={value.referrerPractice}
          onChange={(v) => set('referrerPractice', v)}
          autoComplete="off"
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'referrerPractice')}
        />
        <TextInput
          label="Clinician"
          description="As on the letter."
          isOptional
          value={value.referrerClinician}
          onChange={(v) => set('referrerClinician', v)}
          autoComplete="off"
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'referrerClinician')}
        />
        <TextInput
          label="ODS code"
          description="The practice's code, if known: 3 to 10 letters and digits."
          isOptional
          value={value.referrerOdsCode}
          onChange={(v) => set('referrerOdsCode', v)}
          autoComplete="off"
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'referrerOdsCode')}
        />
      </FormSection>
      <FormSection title="Reason for referral" useGrid={false}>
        <TextArea
          label="Reason"
          description="From the letter."
          value={value.reason}
          onChange={(v) => set('reason', v)}
          rows={6}
          maxLength={MAX_REASON}
          isRequired
          isDisabled={isDisabled}
          status={fieldStatus(errors, 'reason')}
        />
      </FormSection>
    </>
  );
}

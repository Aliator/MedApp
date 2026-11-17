import {VStack} from '@astryxdesign/core/Stack';
import type {Patient} from '../../api/patients';
import {formatDateTime} from '../../utils/dates';
import {SummaryAlert} from '../shared/SummaryAlert';

export type PatientFlags = Pick<
  Patient,
  'isRestricted' | 'restrictionReason' | 'restrictedAtUtc' | 'isDisputed' | 'disputeReason' | 'disputedAtUtc' | 'isAnonymised' | 'anonymisedAtUtc'
>;

const since = (atUtc: string | null | undefined) => (atUtc ? `Since ${formatDateTime(atUtc)}.` : null);

/** Whether a patient has any flag a banner shows. */
export const hasPatientFlags = (patient: PatientFlags) => Boolean(patient.isRestricted || patient.isDisputed || patient.isAnonymised);

/**
 * The banners a patient's record carries, for everyone who sees it: restricted (Article 18, nothing can be changed
 * but blocking or cancelling a procedure, or cancelling an appointment), disputed by the patient (Article 16), and
 * anonymised. Use it in a page's `message`, on every page about the patient: the patient's page, Edit Patient, the
 * patient's procedures and each procedure's page (from the procedure's `patientFlags`); and in the appointment
 * dialogs, under the patient. Shows nothing when the record has no flags.
 *
 * Each is a `SummaryAlert`: a short label naming the state ("Restricted record"), so the header stays small, with the
 * whole message (what it means, the reason, since when) over the page on hover, focus or tap, and read in full by
 * screen readers (the restriction as an alert). The label still names the state on every page about the patient: it's
 * a control for hazard H-22, beside `PatientBanner`'s tokens.
 */
export function PatientFlagBanners({patient}: {patient: PatientFlags}) {
  if (!hasPatientFlags(patient)) return null;

  return (
    <VStack gap={2} hAlign="stretch">
      {patient.isAnonymised && (
        <SummaryAlert
          status="info"
          label="Anonymised record"
          message="This record has been anonymised. Its identifying details and notes have been removed, and it can't be changed."
          details={[since(patient.anonymisedAtUtc)].filter((d): d is string => d !== null)}
        />
      )}
      {patient.isRestricted && (
        <SummaryAlert
          status="warning"
          label="Restricted record"
          message="Restricted record: nothing about this patient, their procedures or their appointments can be changed until an admin lifts the restriction, except that a procedure can be blocked or cancelled, with a reason, and an appointment cancelled."
          details={[patient.restrictionReason ? `Reason: ${patient.restrictionReason}` : null, since(patient.restrictedAtUtc)].filter(
            (d): d is string => d !== null,
          )}
        />
      )}
      {patient.isDisputed && (
        <SummaryAlert
          status="info"
          label="Disputed record"
          message="Disputed: the patient disputes something in this record."
          details={[patient.disputeReason ? `What's disputed: ${patient.disputeReason}` : null, since(patient.disputedAtUtc)].filter(
            (d): d is string => d !== null,
          )}
        />
      )}
    </VStack>
  );
}

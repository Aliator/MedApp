import {VStack} from '@astryxdesign/core/Stack';
import type {Patient} from '../../api/patients';
import {formatDateTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';

export type PatientFlags = Pick<
  Patient,
  'isRestricted' | 'restrictionReason' | 'restrictedAtUtc' | 'isDisputed' | 'disputeReason' | 'disputedAtUtc' | 'isAnonymised' | 'anonymisedAtUtc'
>;

const since = (atUtc: string | null | undefined) => (atUtc ? `Since ${formatDateTime(atUtc)}.` : null);

/** Whether a patient has any flag a banner shows. */
export const hasPatientFlags = (patient: PatientFlags) => Boolean(patient.isRestricted || patient.isDisputed || patient.isAnonymised);

/**
 * The banners a patient's record carries, for everyone who sees it: restricted (Article 18, nothing can be changed),
 * disputed by the patient (Article 16), and anonymised. Use it in a page's `message`, on every page about the patient.
 * Shows nothing when the record has no flags.
 */
export function PatientFlagBanners({patient}: {patient: PatientFlags}) {
  if (!hasPatientFlags(patient)) return null;

  return (
    <VStack gap={2} hAlign="stretch">
      {patient.isAnonymised && (
        <AlertMessage
          status="info"
          message="This record has been anonymised. Its identifying details and notes have been removed, and it can't be changed."
          details={[since(patient.anonymisedAtUtc)].filter((d): d is string => d !== null)}
        />
      )}
      {patient.isRestricted && (
        <AlertMessage
          status="warning"
          message="Restricted record: nothing about this patient or their procedures can be changed until an admin lifts the restriction."
          details={[patient.restrictionReason ? `Reason: ${patient.restrictionReason}` : null, since(patient.restrictedAtUtc)].filter(
            (d): d is string => d !== null,
          )}
        />
      )}
      {patient.isDisputed && (
        <AlertMessage
          status="info"
          message="Disputed: the patient disputes something in this record."
          details={[patient.disputeReason ? `What's disputed: ${patient.disputeReason}` : null, since(patient.disputedAtUtc)].filter(
            (d): d is string => d !== null,
          )}
        />
      )}
    </VStack>
  );
}

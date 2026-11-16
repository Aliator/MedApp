import {Skeleton} from '@astryxdesign/core/Skeleton';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {patientQuery} from '../../api/patients';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {PatientFlagBanners, type PatientFlags} from '../patients/PatientFlagBanners';

/** Who an appointment is for: who they are (as the patient banner shows them), their record's flags, and its ID. */
export type AppointmentPatientDetails = BannerPatient & PatientFlags & {id: string};

export interface AppointmentPatientState {
  /** The patient's details, once known. */
  patient: (BannerPatient & PatientFlags) | undefined;
  isLoading: boolean;
  isError: boolean;
}

/**
 * The details of the patient an appointment is for, for the dialogs to show who it is. An appointment carries only
 * the patient's name, so when the caller doesn't already have the patient (`known`, e.g. the patient's or the
 * procedure's page), they're read from the patient's record (`patientQuery`), which the activity log records as a
 * view. Nothing is read while `patientId` is undefined.
 */
export function useAppointmentPatient(patientId: string | undefined, known?: BannerPatient & PatientFlags): AppointmentPatientState {
  const read = useQuery({...patientQuery(patientId ?? ''), enabled: !known && !!patientId});
  if (known) return {patient: known, isLoading: false, isError: false};
  return {patient: read.data, isLoading: !!patientId && read.isPending, isError: read.isError};
}

/**
 * The patient at the top of an appointment dialog: their `PatientBanner` (compact), so staff can check who they're
 * booking or changing (hazard H-34), then the record's restricted, disputed or anonymised banners. While the details
 * load it holds their place; if they can't be read, it falls back to the name the appointment carries.
 */
export function AppointmentPatientBanner({state, name}: {state: AppointmentPatientState; name?: string | null}) {
  const {patient, isLoading, isError} = state;
  if (patient) {
    return (
      <VStack gap={2} hAlign="stretch">
        <PatientBanner patient={patient} isCompact />
        <PatientFlagBanners patient={patient} />
      </VStack>
    );
  }
  if (isLoading) return <Skeleton height="var(--spacing-12)" />;
  if (isError) {
    return (
      <Text type="body" color="secondary">
        {`${name ? `${name}: their` : 'The patient’s'} details could not be loaded.`}
      </Text>
    );
  }
  return null;
}

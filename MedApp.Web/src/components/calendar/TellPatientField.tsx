import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {useQuery} from '@tanstack/react-query';
import {patientMessagingStatusQuery} from '../../api/patientMessages';

/**
 * Whether this deployment texts and emails patients, so a dialog that moves or cancels appointments can offer "Tell
 * the patient" (centre plan decision 130). Read only while `isOpen`; false until known, and when it can't be read.
 */
export function usePatientMessagingOn(isOpen: boolean): boolean {
  const status = useQuery({...patientMessagingStatusQuery(), enabled: isOpen});
  return status.data?.isOn === true;
}

export interface TellPatientFieldProps {
  value: boolean;
  onChange: (value: boolean) => void;
  /** For a clinic session's cancellation: "Tell the patients". */
  isPlural?: boolean;
  isDisabled?: boolean;
}

/**
 * "Tell the patient by text or email" (decision 130), for moving or cancelling a booking, or cancelling a session.
 * Show it only while patient messages are on (`usePatientMessagingOn`); the caller ticks it by default when the
 * patient chose a channel (`tellsPatientByDefault`), or for a session. The message goes on the channels the patient
 * chose, and never gives the reason.
 */
export function TellPatientField({value, onChange, isPlural = false, isDisabled = false}: TellPatientFieldProps) {
  return (
    <CheckboxInput
      label={isPlural ? 'Tell the patients by text or email' : 'Tell the patient by text or email'}
      description="As they chose. The message gives the date, time and place, never the reason."
      value={value}
      onChange={onChange}
      isDisabled={isDisabled}
    />
  );
}

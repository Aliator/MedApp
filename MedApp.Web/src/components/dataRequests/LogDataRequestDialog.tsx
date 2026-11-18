import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCreateDataRequest} from '../../api/dataRequests';
import {ApiValidationError, fieldStatus, otherMessages, problemMessage, type ValidationErrors} from '../../api/errors';
import {todayIso, WEEK_STARTS_ON} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

const FIELDS = ['receivedOn', 'requestedByPatient', 'requesterName', 'requesterRelationship'];

export interface LogDataRequestDialogProps {
  isOpen: boolean;
  patientId: string;
  /** The patient as the banner shows them, so the request is logged against the right one. */
  patient: BannerPatient;
  onClose: () => void;
}

/**
 * Logging a patient's data request (a subject access request) as received, for holders of ManagePrivacy, from the
 * patient's page: the day it was received (today to begin with; it's due one calendar month on) and who asked: the
 * patient, or someone on their behalf, with their name and relationship or authority. It opens the new request's page.
 * A 400 shows on its fields; a 409 or 403 in the API's words. Mount it afresh for each opening.
 */
export function LogDataRequestDialog({isOpen, patientId, patient, onClose}: LogDataRequestDialogProps) {
  const announce = useAnnounce();
  const navigate = useNavigate();
  const create = useCreateDataRequest();
  const [receivedOn, setReceivedOn] = useState<string>(todayIso());
  const [askedBy, setAskedBy] = useState('');
  const [requesterName, setRequesterName] = useState('');
  const [requesterRelationship, setRequesterRelationship] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const byPatient = askedBy === 'patient';
  const isComplete = receivedOn !== '' && askedBy !== '' && (byPatient || (requesterName.trim() !== '' && requesterRelationship.trim() !== ''));
  const others = otherMessages(errors, FIELDS);

  const confirm = () => {
    setErrors(null);
    setMessage(null);
    create.mutate(
      {
        patientId,
        receivedOn,
        requestedByPatient: byPatient,
        requesterName: byPatient ? null : requesterName.trim(),
        requesterRelationship: byPatient ? null : requesterRelationship.trim(),
      },
      {
        onSuccess: (request) => {
          announce('Data request logged');
          onClose();
          void navigate({to: '/data-requests/$id', params: {id: request.id ?? ''}});
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setMessage(problemMessage(e, 'The data request could not be logged. Please try again.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Log Data Request"
      message="A request for a copy of the record (a subject access request). It's due one calendar month from the day it was received."
      icon={AppIcon.DataRequests}
      confirmText="Log Data Request"
      confirmDisabled={!isComplete}
      isProcessing={create.isPending}
      onConfirm={confirm}
      onCancel={onClose}
      hasSaveShortcut>
      <VStack gap={3} hAlign="stretch">
        <PatientBanner patient={patient} isCompact />
        {others.length > 0 && <AlertMessage message={errors?.title ?? 'The data request could not be logged.'} details={others} />}
        {message && <AlertMessage message={message} />}
        <DateInput
          label="Received on"
          value={receivedOn as ISODateString}
          onChange={(value) => setReceivedOn(value ?? '')}
          weekStartsOn={WEEK_STARTS_ON}
          format="date_weekday"
          isRequired
          status={fieldStatus(errors, 'receivedOn')}
        />
        <RadioList label="Asked by" value={askedBy} onChange={setAskedBy} isRequired status={fieldStatus(errors, 'requestedByPatient')}>
          <RadioListItem value="patient" label="The patient" />
          <RadioListItem value="other" label="Someone on their behalf" />
        </RadioList>
        {askedBy === 'other' && (
          <>
            <TextInput
              label="Their name"
              value={requesterName}
              onChange={setRequesterName}
              isRequired
              isDisabled={create.isPending}
              status={fieldStatus(errors, 'requesterName')}
            />
            <TextInput
              label="Relationship or authority"
              description="For example: parent, lasting power of attorney, solicitor with the patient's consent."
              value={requesterRelationship}
              onChange={setRequesterRelationship}
              isRequired
              isDisabled={create.isPending}
              status={fieldStatus(errors, 'requesterRelationship')}
            />
          </>
        )}
      </VStack>
    </ConfirmDialog>
  );
}

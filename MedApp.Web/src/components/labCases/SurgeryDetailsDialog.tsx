import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {MAX_SURGERY_PROCEDURE, MAX_SURGERY_TEXT, useLabCaseActions, type LabCase} from '../../api/labCases';
import {WEEK_STARTS_ON} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

const FIELDS = ['surgeryOn', 'procedure', 'outcome', 'noSurgeryExpected', 'detail'];

/**
 * The hospital's surgery details on its lab case (MC20), typed by the lab as the hospital wrote them: the date, the
 * procedure, the outcome and any other details, or "No surgery expected" with why. Added to a case awaiting them (its
 * report sent), they close it; arriving earlier, they're kept and the case stays open. The case's patient, number and
 * hospital show, so they go on the right case (hazard H-74). Nothing is worked out from them. Mount it afresh for each
 * opening.
 */
export function SurgeryDetailsDialog({labCase, onClose}: {labCase: LabCase; onClose: () => void}) {
  const announce = useAnnounce();
  const action = useLabCaseActions(labCase.id ?? '').surgeryDetails;
  const [surgeryOn, setSurgeryOn] = useState<string | undefined>();
  const [procedure, setProcedure] = useState('');
  const [outcome, setOutcome] = useState('');
  const [detail, setDetail] = useState('');
  const [noSurgeryExpected, setNoSurgeryExpected] = useState(false);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const closes = labCase.status === 'AwaitingSurgeryDetails';
  const isComplete = noSurgeryExpected ? detail.trim() !== '' : Boolean(surgeryOn) || [procedure, outcome, detail].some((v) => v.trim() !== '');

  const confirm = () => {
    setErrors(null);
    setMessage(null);
    action.mutate(
      {
        surgeryOn: noSurgeryExpected ? null : (surgeryOn ?? null),
        procedure: noSurgeryExpected ? null : procedure.trim() || null,
        outcome: noSurgeryExpected ? null : outcome.trim() || null,
        noSurgeryExpected,
        detail: detail.trim() || null,
        expectedVersion: labCase.version ?? 0,
      },
      {
        onSuccess: () => {
          announce(closes ? 'Surgery details added; the case is closed' : 'Surgery details added');
          onClose();
        },
        onError: (e: unknown) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setMessage(problemMessage(e, 'The surgery details could not be added.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Add Surgery Details"
      message={
        closes
          ? 'As the hospital sent them, typed as written. The report has gone back, so adding these closes the case.'
          : 'As the hospital sent them, typed as written. They stay with the case; it closes once its report has gone back and the final details are added.'
      }
      icon={AppIcon.Success}
      intent="accent"
      confirmText={closes ? 'Add and Close Case' : 'Add Details'}
      confirmDisabled={!isComplete}
      isProcessing={action.isPending}
      hasSaveShortcut
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {labCase.patient && <PatientBanner patient={labCase.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Lab case">{labCase.number}</MetadataListItem>
          <MetadataListItem label="Hospital">{labCase.hospitalName}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage message={message} />}
        {closes && (
          <Switch label="No surgery expected" value={noSurgeryExpected} onChange={setNoSurgeryExpected} />
        )}
        {!noSurgeryExpected && (
          <>
            <DateInput
              label="Date of surgery"
              isOptional
              value={surgeryOn as ISODateString | undefined}
              onChange={(value) => setSurgeryOn(value ?? undefined)}
              weekStartsOn={WEEK_STARTS_ON}
              format="date_weekday"
              status={fieldStatus(errors, 'surgeryOn')}
            />
            <TextInput
              label="Procedure"
              isOptional
              value={procedure}
              onChange={(value) => setProcedure(value.slice(0, MAX_SURGERY_PROCEDURE))}
              autoComplete="off"
              status={fieldStatus(errors, 'procedure')}
            />
            <TextArea
              label="Outcome"
              isOptional
              value={outcome}
              onChange={setOutcome}
              rows={2}
              maxLength={MAX_SURGERY_TEXT}
              status={fieldStatus(errors, 'outcome')}
            />
          </>
        )}
        <TextArea
          label={noSurgeryExpected ? 'Why no surgery is expected' : 'Other details'}
          description="As the hospital wrote it. Part of the patient's clinical record."
          isOptional={!noSurgeryExpected}
          isRequired={noSurgeryExpected}
          value={detail}
          onChange={setDetail}
          rows={3}
          maxLength={MAX_SURGERY_TEXT}
          status={fieldStatus(errors, 'detail')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

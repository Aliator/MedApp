import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {useOpenLabCase} from '../../api/labCases';
import {labTestsQuery} from '../../api/labTests';
import type {Specimen} from '../../api/specimens';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface OpenLabCaseDialogProps {
  specimen: Specimen;
  onClose: () => void;
}

/**
 * Opening a lab case by hand (MC15) for a received sample with none: the lab chooses the test from its catalogue,
 * by name, none chosen to start with (decision 191). The stage's other received samples without a case join it. On
 * success it opens the case.
 */
export function OpenLabCaseDialog({specimen, onClose}: OpenLabCaseDialogProps) {
  const announce = useAnnounce();
  const navigate = useNavigate();
  const tests = useQuery(labTestsQuery(false));
  const open = useOpenLabCase();
  const [labTestId, setLabTestId] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const confirm = () => {
    setErrors(null);
    setMessage(null);
    open.mutate(
      {specimenId: specimen.id ?? '', labTestId, expectedVersion: specimen.version ?? 0},
      {
        onSuccess: (labCase) => {
          announce(`Lab case ${labCase.number} opened`);
          onClose();
          void navigate({to: '/lab/cases/$id', params: {id: labCase.id ?? ''}});
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setMessage(problemMessage(e, 'The lab case could not be opened.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Open Lab Case"
      message="The lab works the sample in a case: its steps, its outcomes and its report. Choose the test it's for."
      icon={AppIcon.LabCases}
      confirmText="Open Lab Case"
      confirmDisabled={!labTestId}
      isProcessing={open.isPending}
      onConfirm={confirm}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {specimen.patient && <PatientBanner patient={specimen.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Sample">{specimen.number}</MetadataListItem>
          <MetadataListItem label="Type">{specimen.specimenTypeName}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={['labTestId']} />}
        {message && <AlertMessage message={message} />}
        <RadioList label="Test" value={labTestId} onChange={setLabTestId} isRequired status={fieldStatus(errors, 'labTestId')}>
          {[...(tests.data ?? [])]
            .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
            .map((t) => (
              <RadioListItem key={t.id} value={t.id ?? ''} label={t.name ?? ''} />
            ))}
        </RadioList>
      </VStack>
    </ConfirmDialog>
  );
}

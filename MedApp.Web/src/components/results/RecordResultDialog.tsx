import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import type {Attachment} from '../../api/attachments';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {useRecordExternalResult} from '../../api/results';
import {useAnnounce} from '../../app/announcements';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RESPONSIBLE_PERMISSION} from './ResultDialogs';

/** The fields the API's 400 can name, each shown on its field; anything else goes above them. */
const FIELDS = ['title', 'responsibleUserId', 'attachmentId', 'pathwayId'];

export interface RecordResultDialogProps {
  /** The attached file that is the report. */
  attachment: Attachment;
  /** The patient, shown as `PatientBanner` shows them (the dialog covers the page's), when the page has them. */
  patient?: BannerPatient;
  onClose: () => void;
}

/**
 * Recording an attached file as a result from elsewhere (Record as Result; decision 93), such as a scan report from
 * another hospital: what it is (a title, 200 characters) and who must sign it off (a holder of SignOffResults, nobody
 * chosen to start with). It belongs to the file's own pathway, shown read-only. The patient shows as the banner does,
 * with the file's name, so it's recorded on the right one. Once recorded, the result's page opens. A 400 shows on its
 * fields; a 409 or 403 in the API's words. Mount it afresh for each opening, so it starts empty.
 */
export function RecordResultDialog({attachment, patient, onClose}: RecordResultDialogProps) {
  const announce = useAnnounce();
  const navigate = useNavigate();
  const record = useRecordExternalResult();
  const staff = useQuery(staffWithPermissionQuery(RESPONSIBLE_PERMISSION));
  const [title, setTitle] = useState('');
  const [responsibleUserId, setResponsibleUserId] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const options = (staff.data ?? []).map((member) => ({value: member.userId ?? '', label: member.username ?? ''}));

  const save = () => {
    setErrors(null);
    setMessage(null);
    record.mutate(
      {attachmentId: attachment.id ?? '', title, pathwayId: attachment.pathwayId, responsibleUserId},
      {
        onSuccess: (result) => {
          announce('Result recorded');
          onClose();
          void navigate({to: '/results/$id', params: {id: result.id ?? ''}});
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setMessage(problemMessage(e, 'The result could not be recorded. Please try again.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Record as Result"
      message="A report from elsewhere, such as a scan report from another hospital. It awaits sign-off by the clinician you choose, who is told."
      icon={AppIcon.Results}
      confirmText="Record as Result"
      confirmDisabled={!title.trim() || !responsibleUserId}
      isProcessing={record.isPending}
      hasSaveShortcut
      onConfirm={save}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {patient && <PatientBanner patient={patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="File">{attachment.fileName}</MetadataListItem>
          <MetadataListItem label="Pathway">{attachment.pathwayId ? attachment.pathwayTitle : 'Not on a pathway'}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage label="Result not recorded" message={message} />}
        <TextInput
          label="Title"
          description="What it is, e.g. CT chest report from another hospital."
          value={title}
          onChange={setTitle}
          isRequired
          autoComplete="off"
          status={fieldStatus(errors, 'title')}
        />
        <AnchoredSelector
          label="Responsible clinician"
          description="Who must sign it off: someone who can sign results off."
          placeholder={staff.isPending ? 'Loading staff...' : 'Choose who signs it off'}
          options={options}
          value={responsibleUserId || undefined}
          onChange={(value: string) => setResponsibleUserId(value)}
          isRequired
          status={staff.isError ? {type: 'error', message: 'The staff could not be loaded.'} : fieldStatus(errors, 'responsibleUserId')}
        />
      </VStack>
    </ConfirmDialog>
  );
}

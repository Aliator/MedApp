import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {FileInput} from '@astryxdesign/core/FileInput';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {allowedTypesText, checkAttachmentFile, DEFAULT_ATTACHMENT_LIMITS} from '../../api/attachments';
import {CONSENT_SIGNED_BY, consentFormLabel, consentFormsQuery, signedByLabel, useRecordConsent} from '../../api/consent';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import type {Pathway, PathwayStage} from '../../api/pathways';
import {todayIso, WEEK_STARTS_ON} from '../../utils/dates';
import {formatFileSize} from '../../utils/fileSize';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** The fields the API's 400 can name, each shown on its field; anything else goes above them. */
const FIELDS = ['stageId', 'consentFormId', 'signedOn', 'signedBy', 'scanAttachmentId'];

export interface RecordConsentDialogProps {
  isOpen: boolean;
  pathway: Pathway;
  /** The stages consent can be recorded for now (the API's `canRecordConsent`), in the pathway's order. */
  stages: readonly PathwayStage[];
  onClose: () => void;
}

/**
 * Recording consent to a stage (Record Consent): the stage, the consent form (from the setup list, not archived), the
 * date it was signed (today, or up to a year back), who signed it, and optionally the scanned, signed form, attached to
 * the pathway as it's saved. The patient shows as the banner does, so it's recorded on the right one (hazard H-59).
 * MedApp records what the clinician states: it asks nothing about capacity or validity. A 400 shows on its fields; a 409
 * in the API's words. Mount it afresh for each opening.
 */
export function RecordConsentDialog({isOpen, pathway, stages, onClose}: RecordConsentDialogProps) {
  const announce = useAnnounce();
  const forms = useQuery(consentFormsQuery());
  const record = useRecordConsent(pathway.id ?? '');
  const [stageId, setStageId] = useState(stages.length === 1 ? (stages[0].id ?? '') : '');
  const [formId, setFormId] = useState('');
  const [signedOn, setSignedOn] = useState<string>(todayIso());
  const [signedBy, setSignedBy] = useState('');
  const [scan, setScan] = useState<File | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const limits = DEFAULT_ATTACHMENT_LIMITS;
  const scanProblem = scan ? checkAttachmentFile(scan, limits) : null;

  const save = () => {
    setErrors(null);
    setMessage(null);
    record.mutate(
      {stageId, consentFormId: formId, signedOn, signedBy, scan},
      {
        onSuccess: () => {
          announce('Consent recorded');
          onClose();
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setMessage(problemMessage(e, 'The consent could not be recorded.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Record Consent"
      message="What the clinician states: the form, when it was signed and by whom. MedApp doesn't assess the consent."
      icon={AppIcon.Consent}
      confirmText="Record Consent"
      confirmDisabled={!stageId || !formId || !signedOn || !signedBy || scanProblem !== null}
      isProcessing={record.isPending}
      onConfirm={save}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {pathway.patient && <PatientBanner patient={pathway.patient} isCompact />}
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage label="Consent not recorded" message={message} />}
        <AnchoredSelector
          label="Stage"
          placeholder="Choose the stage consented to"
          options={stages.map((s) => ({value: s.id ?? '', label: s.name ?? ''}))}
          value={stageId || undefined}
          onChange={(value: string) => setStageId(value)}
          isRequired
          status={fieldStatus(errors, 'stageId')}
        />
        <AnchoredSelector
          label="Consent form"
          placeholder={forms.isPending ? 'Loading consent forms...' : 'Choose the form'}
          options={(forms.data ?? []).map((f) => ({value: f.id ?? '', label: consentFormLabel(f.name, f.formVersion)}))}
          value={formId || undefined}
          onChange={(value: string) => setFormId(value)}
          isRequired
          status={forms.isError ? {type: 'error', message: 'The consent forms could not be loaded.'} : fieldStatus(errors, 'consentFormId')}
        />
        <DateInput
          label="Signed on"
          value={signedOn as ISODateString}
          onChange={(value) => setSignedOn(value ?? '')}
          weekStartsOn={WEEK_STARTS_ON}
          format="date_weekday"
          isRequired
          status={fieldStatus(errors, 'signedOn')}
        />
        <RadioList label="Signed by" value={signedBy} onChange={setSignedBy} isRequired status={fieldStatus(errors, 'signedBy')}>
          {CONSENT_SIGNED_BY.map((s) => (
            <RadioListItem key={s} value={s} label={signedByLabel(s)} />
          ))}
        </RadioList>
        <FileInput
          label="Scanned form"
          value={scan}
          onChange={(picked) => setScan([picked].flat()[0] ?? null)}
          placeholder="Choose the signed form's scan"
          description={`Optional: it can be added later. ${allowedTypesText(limits)}, up to ${formatFileSize(limits.maxFileBytes)}.`}
          status={scanProblem ? {type: 'error', message: scanProblem} : fieldStatus(errors, 'scanAttachmentId')}
          isDisabled={record.isPending}
        />
        {!scan && (
          <Text type="supporting" color="secondary">
            Without a scan, the record shows "No scan" until one is added.
          </Text>
        )}
      </VStack>
    </ConfirmDialog>
  );
}

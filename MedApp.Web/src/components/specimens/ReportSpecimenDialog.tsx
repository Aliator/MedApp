import {FileInput} from '@astryxdesign/core/FileInput';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {Button} from '@astryxdesign/core/Button';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useRef, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {allowedTypesText, checkAttachmentFile, DEFAULT_ATTACHMENT_LIMITS, isUploadCancelled, uploadErrorText} from '../../api/attachments';
import {useSpecimenActions, type Specimen} from '../../api/specimens';
import {formatFileSize} from '../../utils/fileSize';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface ReportSpecimenDialogProps {
  isOpen: boolean;
  specimen: Specimen;
  onClose: () => void;
}

/**
 * Attaching the lab's report to a received specimen (Report): one file, under every rule an attached file has (its
 * type and size checked here first, and by the server, whose word is final), from the version the page shows. It's
 * attached to the pathway (or the patient, if the pathway has closed), and the specimen is Reported. The patient shows
 * as the banner does, with the specimen's number, so the report goes on the right one (hazard H-47). Its progress
 * shows as it's sent, with Stop Sending. MedApp files the report as the lab sent it: it never reads it. Mount it afresh
 * for each opening (a `key`).
 */
export function ReportSpecimenDialog({isOpen, specimen, onClose}: ReportSpecimenDialogProps) {
  const announce = useAnnounce();
  const {report} = useSpecimenActions(specimen.id ?? '');
  const [file, setFile] = useState<File | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const sending = useRef<AbortController | null>(null);
  const limits = DEFAULT_ATTACHMENT_LIMITS;

  const choose = (picked: File | null) => {
    setProblem(picked ? checkAttachmentFile(picked, limits) : null);
    setFile(picked);
  };

  const send = () => {
    if (!file) return;
    setProblem(null);
    setProgress(0);
    const controller = new AbortController();
    sending.current = controller;
    report.mutate(
      {file, expectedVersion: specimen.version ?? 0, onProgress: setProgress, signal: controller.signal},
      {
        onSuccess: () => {
          announce('Report attached');
          onClose();
        },
        onError: (e) => {
          setProgress(null);
          if (!isUploadCancelled(e)) setProblem(uploadErrorText(e));
        },
      },
    );
  };

  /** Stops the file on its way; the dialog stays open, to choose another or close. */
  const stop = () => sending.current?.abort();

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Attach Report"
      message="The lab's report, as a file. It's attached to the pathway and the specimen is marked reported."
      icon={AppIcon.Upload}
      confirmText="Attach Report"
      confirmDisabled={!file || checkAttachmentFile(file, limits) !== null}
      isProcessing={report.isPending}
      onConfirm={send}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {specimen.patient && <PatientBanner patient={specimen.patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Specimen">{specimen.number}</MetadataListItem>
          <MetadataListItem label="Type">{specimen.specimenTypeName}</MetadataListItem>
          <MetadataListItem label="Lab">{specimen.laboratoryName}</MetadataListItem>
          <MetadataListItem label="Lab's reference">{specimen.labReference || '—'}</MetadataListItem>
        </MetadataList>
        {problem && <AlertMessage message={problem} />}
        <FileInput
          label="Report"
          value={file}
          onChange={(picked) => choose([picked].flat()[0] ?? null)}
          placeholder="Choose the report's file"
          description={`${allowedTypesText(limits)}, up to ${formatFileSize(limits.maxFileBytes)}.`}
          isDisabled={report.isPending}
        />
        {progress !== null && (
          <VStack gap={1} hAlign="stretch">
            <ProgressBar label={`Sending ${file?.name ?? 'the report'}`} value={Math.round(progress * 100)} max={100} hasValueLabel />
            <HStack gap={3} vAlign="center" wrap="wrap">
              <Text type="supporting" color="secondary">
                Keep this open until it has been sent.
              </Text>
              <Button label="Stop Sending" variant="secondary" size="sm" onClick={stop} />
            </HStack>
          </VStack>
        )}
      </VStack>
    </ConfirmDialog>
  );
}

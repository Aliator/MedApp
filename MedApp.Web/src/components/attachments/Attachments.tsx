import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {FileInput} from '@astryxdesign/core/FileInput';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {Thumbnail} from '@astryxdesign/core/Thumbnail';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState, type AnchorHTMLAttributes, type CSSProperties, type Ref} from 'react';
import {
  allowedTypesText,
  attachedToOf,
  attachmentContentUrl,
  attachmentKind,
  attachmentLimits,
  attachmentsQuery,
  attachmentTargetKey,
  attachmentThumbnailUrl,
  useDeleteAttachment,
  type Attachment,
  type AttachmentKind,
  type AttachmentTarget,
} from '../../api/attachments';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCan} from '../../auth/auth';
import {formatDateTime} from '../../utils/dates';
import {formatFileSize} from '../../utils/fileSize';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {useIsNarrow} from '../pages/Page';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {useAttachmentsState, type AttachmentsState, type AttachmentUpload} from './useAttachmentsState';

/** A file's thumbnail or type icon: a small square, the same for both, so names line up. */
const PREVIEW_SIZE = 'var(--spacing-10)';
const PREVIEW_BOX: CSSProperties = {width: PREVIEW_SIZE, height: PREVIEW_SIZE, flexShrink: 0};

const KIND_ICONS: Record<AttachmentKind, IconType> = {
  pdf: AppIcon.FilePdf,
  image: AppIcon.FileImage,
  word: AppIcon.FileWord,
  text: AppIcon.FileText,
  other: AppIcon.File,
};

/** What "Not scanned" means, on its token and above the list. */
export const NOT_SCANNED_NOTE = "No malware scanner is set up yet, so files aren't scanned. Open them with care.";

/**
 * The link a Download button renders: a plain `<a download>`, outside the router (which would try to open the API's
 * path as a page). The API sends every file as a download (`Content-Disposition: attachment`) too.
 */
function DownloadLink({ref, ...props}: AnchorHTMLAttributes<HTMLAnchorElement> & {ref?: Ref<HTMLAnchorElement>}) {
  return <a ref={ref} {...props} download />;
}

export interface AttachmentsProps {
  /** What the files are attached to. A patient's list shows all their files, each saying what it's attached to. */
  target: AttachmentTarget;
  /**
   * Whether files can change here now: shows the add area, and lets staff delete their own files. A patient's files
   * always can; a pathway's unless it's cancelled or archived (`canEditDetails`); a stage's only while its notes
   * can be edited (`canEdit`). The server enforces the same. Admins can delete any file, wherever it is.
   */
  canChange: boolean;
  /**
   * The patient's record is restricted or anonymised: nothing can be added or deleted, by admins either (the API
   * refuses with a 409), though the files can still be listed and downloaded. The page says why, in its own banner.
   */
  isLocked?: boolean;
  /**
   * Uploads in progress and Show deleted, held by the page (`useAttachmentsState`), so they carry on when the page
   * changes layout. Without it, the component keeps its own.
   */
  state?: AttachmentsState;
}

/**
 * Files attached to a patient, a pathway or a stage: an add area (drop files on it, or press it to choose them,
 * several at once), each file's progress as it's sent, and the list, newest first. Each file shows a thumbnail
 * (pictures) or its type's icon, its name, type, size, who attached it and when, and whether it was scanned, with
 * Download and (where allowed) Delete. Holders of CorrectRecords can show deleted files too; the add area is only
 * for holders of AttachFiles.
 *
 * Files are never shown inline: Download always downloads, and only the server's small re-encoded thumbnails are
 * shown as images. Use it in a card's section, padded by its container (it lays out no padding of its own).
 */
export function Attachments({target, canChange, isLocked = false, state}: AttachmentsProps) {
  const ownState = useAttachmentsState();
  const {uploads, add, cancel, dismissFailed, isShowingDeleted, setShowingDeleted} = state ?? ownState;
  // Deleting anyone's file, wherever it is, and seeing deleted ones; and attaching files (and deleting your own).
  const canCorrectRecords = useCan('CorrectRecords');
  const canAttach = useCan('AttachFiles');
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const remove = useDeleteAttachment();
  const targetKey = attachmentTargetKey(target);
  const targetKind = attachedToOf(target);
  const isShowingAll = canCorrectRecords && isShowingDeleted(targetKey);
  const list = useQuery(attachmentsQuery(target, isShowingAll));
  const limits = attachmentLimits(list.data);
  const items = list.data?.items ?? [];
  const total = list.data?.totalCount ?? items.length;
  const mine = uploads.filter((upload) => upload.targetKey === targetKey);
  const failed = mine.filter((upload) => upload.state === 'failed');
  const [toDelete, setToDelete] = useState<Attachment | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  /**
   * Holders of CorrectRecords may delete any file; staff their own (`canDelete`, which the API sets for holders of
   * AttachFiles), and only where files can change now. Nobody can while the record is locked.
   */
  const canDelete = (attachment: Attachment) =>
    !isLocked &&
    (attachment.canDelete ?? false) &&
    !attachment.isDeleted &&
    (canCorrectRecords || (canChange && attachment.attachedTo === targetKind));

  const confirmDelete = (attachment: Attachment) =>
    remove.mutate(attachment.id ?? '', {
      onSuccess: () => {
        announce(`${attachment.fileName} deleted`);
        setToDelete(null);
      },
      onError: (error) => {
        const message = `${attachment.fileName} could not be deleted. ${problemMessage(error, 'Please try again.')}`;
        setDeleteError(message);
        announce(message);
        setToDelete(null);
      },
    });

  return (
    <VStack gap={4}>
      {canChange && canAttach && !isLocked && (
        <FileInput
          label="Add files"
          mode="dropzone"
          isMultiple
          value={null}
          // Several at once (`isMultiple`): an array, or null when none were picked.
          onChange={(files) => add(target, [files].flat().filter((picked) => picked !== null), limits)}
          placeholder="Drop files here, or press to choose them"
          description={`${allowedTypesText(limits)}, up to ${formatFileSize(limits.maxFileBytes)} each.`}
        />
      )}
      {mine.some((upload) => upload.state !== 'failed') && (
        <VStack gap={3}>
          {mine
            .filter((upload) => upload.state !== 'failed')
            .map((upload) => (
              <UploadRow key={upload.id} upload={upload} onCancel={() => cancel(upload)} />
            ))}
        </VStack>
      )}
      {failed.length > 0 && (
        <VStack gap={2} hAlign="start">
          <AlertMessage
            message={failed.length === 1 ? "A file wasn't attached" : `${failed.length} files weren't attached`}
            details={failed.map((upload) => upload.error)}
          />
          <Button label="Dismiss" variant="ghost" size="sm" onClick={() => dismissFailed(targetKey)} />
        </VStack>
      )}
      {deleteError && (
        <VStack gap={2} hAlign="start">
          <AlertMessage message={deleteError} />
          <Button label="Dismiss" variant="ghost" size="sm" onClick={() => setDeleteError(null)} />
        </VStack>
      )}
      {canCorrectRecords && (
        <Switch
          label="Show deleted files"
          size="sm"
          value={isShowingAll}
          onChange={(isOn) => setShowingDeleted(targetKey, isOn)}
        />
      )}
      {list.isPending ? (
        <Spinner size="sm" label="Loading files..." />
      ) : list.isError ? (
        <AlertMessage message="The files could not be loaded. Please try again." />
      ) : items.length === 0 ? (
        <Text type="body" color="secondary">
          No files attached yet.
        </Text>
      ) : (
        <VStack gap={2}>
          {items.some((item) => !item.isDeleted && item.scanStatus === 'NotScanned') && (
            <HStack gap={2} vAlign="center">
              <Icon icon={AppIcon.Info} color="secondary" size="sm" />
              <Text type="supporting" color="secondary">
                {NOT_SCANNED_NOTE}
              </Text>
            </HStack>
          )}
          <List edge="flush" hasDividers aria-label="Files">
            {items.map((attachment) => (
              <AttachmentRow
                key={attachment.id}
                attachment={attachment}
                isNarrow={isNarrow}
                showsAttachedTo={targetKind === 'Patient'}
                canDelete={canDelete(attachment)}
                onDelete={() => {
                  setDeleteError(null);
                  setToDelete(attachment);
                }}
              />
            ))}
          </List>
          {total > items.length && (
            <Text type="supporting" color="secondary">
              {`Showing the newest ${items.length} of ${total} files.`}
            </Text>
          )}
        </VStack>
      )}
      <ConfirmDialog
        isOpen={toDelete !== null}
        title={toDelete ? `Delete ${toDelete.fileName}?` : 'Delete File?'}
        message="It's hidden at once and removed for good after the retention period. Admins can still see that it was attached."
        isDanger
        confirmText="Delete File"
        isProcessing={remove.isPending}
        onConfirm={() => toDelete && confirmDelete(toDelete)}
        onCancel={() => setToDelete(null)}
      />
    </VStack>
  );
}

/** A file on its way: waiting, or its progress, with Cancel. */
function UploadRow({upload, onCancel}: {upload: AttachmentUpload; onCancel: () => void}) {
  const percent = Math.round(upload.progress * 100);
  return (
    <HStack gap={3} vAlign="center">
      <Icon icon={AppIcon.Upload} color="secondary" />
      <StackItem size="fill">
        {upload.state === 'waiting' ? (
          <Text type="body" maxLines={1}>{`${upload.fileName}: waiting`}</Text>
        ) : (
          <ProgressBar
            label={`Attaching ${upload.fileName}`}
            value={percent}
            hasValueLabel
            formatValueLabel={(value) => `${value}%`}
          />
        )}
      </StackItem>
      <IconButton
        label={`Cancel ${upload.fileName}`}
        tooltip="Cancel"
        icon={<Icon icon={AppIcon.Cancel} />}
        variant="ghost"
        size="sm"
        onClick={onCancel}
      />
    </HStack>
  );
}

/** A picture's thumbnail, or the file type's icon (deleted files, and anything else). Decorative: the name says it. */
function Preview({attachment}: {attachment: Attachment}) {
  if (attachment.hasThumbnail && !attachment.isDeleted)
    return <Thumbnail src={attachmentThumbnailUrl(attachment.id ?? '')} alt="" aria-hidden style={PREVIEW_BOX} />;
  return (
    <Center style={PREVIEW_BOX}>
      <Icon icon={KIND_ICONS[attachmentKind(attachment)]} size="lg" color="secondary" />
    </Center>
  );
}

/** The scan's state, as a token: whether a malware scanner looked at the file. */
function ScanToken({status}: {status: string | null | undefined}) {
  if (status === 'Clean') return <Token size="sm" label="Scanned" color="green" description="A malware scanner checked this file." />;
  if (status === 'Infected') return <Token size="sm" label="Malware found" color="red" />;
  return <Token size="sm" label="Not scanned" color="orange" description={NOT_SCANNED_NOTE} />;
}

/** What a file in a patient's list is attached to: the patient, or a pathway (and stage), linked. */
function AttachedTo({attachment}: {attachment: Attachment}) {
  if (attachment.attachedTo === 'Patient' || !attachment.pathwayId)
    return (
      <Text type="supporting" color="secondary">
        Attached to the patient
      </Text>
    );
  const pathway = <Link href={`/pathways/${attachment.pathwayId}`}>{attachment.pathwayTitle}</Link>;
  return (
    <Text type="supporting" color="secondary" maxLines={2}>
      {attachment.attachedTo === 'Stage' ? (
        <>
          {`Attached to ${attachment.stageName ?? 'a stage'} in `}
          {pathway}
        </>
      ) : (
        <>Attached to {pathway}</>
      )}
    </Text>
  );
}

interface AttachmentRowProps {
  attachment: Attachment;
  /** A narrow window or half view: the buttons go under the details, leaving the name the row's width. */
  isNarrow: boolean;
  showsAttachedTo: boolean;
  canDelete: boolean;
  onDelete: () => void;
}

/** One file: its preview, name, details and scan state, with Download and Delete. */
function AttachmentRow({attachment, isNarrow, showsAttachedTo, canDelete, onDelete}: AttachmentRowProps) {
  const name = attachment.fileName ?? '';
  const actions = (
    <HStack gap={1} vAlign="center">
      {!attachment.isDeleted && (
        <IconButton
          label={`Download ${name}`}
          tooltip="Download"
          icon={<Icon icon={AppIcon.Download} />}
          variant="ghost"
          href={attachmentContentUrl(attachment.id ?? '')}
          as={DownloadLink}
        />
      )}
      {canDelete && (
        <IconButton label={`Delete ${name}`} tooltip="Delete" icon={<Icon icon={AppIcon.Delete} />} variant="ghost" onClick={onDelete} />
      )}
    </HStack>
  );
  return (
    <ListItem
      align="start"
      startContent={<Preview attachment={attachment} />}
      label={
        <Text type="body" weight="semibold" maxLines={2}>
          {name}
        </Text>
      }
      description={
        <VStack gap={1}>
          <Text type="supporting" color="secondary">
            {`${attachment.typeName ?? 'File'} · ${formatFileSize(attachment.sizeBytes ?? 0)}`}
          </Text>
          {showsAttachedTo && <AttachedTo attachment={attachment} />}
          <Text type="supporting" color="secondary">
            {`Added by ${attachment.uploadedByUsername ?? 'unknown'}, ${formatDateTime(attachment.uploadedAtUtc ?? '')}`}
          </Text>
          {attachment.isDeleted && (
            <Text type="supporting" color="secondary">
              {`Deleted by ${attachment.deletedByUsername ?? 'unknown'}, ${formatDateTime(attachment.deletedAtUtc ?? '')}`}
            </Text>
          )}
          <HStack gap={2} vAlign="center" wrap="wrap">
            {attachment.isDeleted ? <Token size="sm" label="Deleted" color="gray" /> : <ScanToken status={attachment.scanStatus} />}
          </HStack>
          {isNarrow && actions}
        </VStack>
      }
      endContent={isNarrow ? undefined : actions}
    />
  );
}

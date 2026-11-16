import {Collapsible} from '@astryxdesign/core/Collapsible';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Attachments, type AttachmentsProps} from './Attachments';

export interface AttachmentsFoldProps extends AttachmentsProps {
  /** Held by the page, so it stays open or folded when the page changes layout. */
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

/**
 * `Attachments` folded under a "Files" heading, as a stage's instructions fold: for a part of a page that has
 * several, such as each stage of a procedure. The files are fetched only once it's opened, so a page of folded
 * stages asks for none.
 */
export function AttachmentsFold({isOpen, onOpenChange, ...attachments}: AttachmentsFoldProps) {
  return (
    <Collapsible
      isOpen={isOpen}
      onOpenChange={onOpenChange}
      chevronPosition="end"
      trigger={
        <Text type="body" weight="semibold">
          Files
        </Text>
      }>
      <VStack gap={0} paddingBlockStart={2}>
        {isOpen && <Attachments {...attachments} />}
      </VStack>
    </Collapsible>
  );
}

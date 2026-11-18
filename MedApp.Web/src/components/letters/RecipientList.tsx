import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {dispatchMethodWords, recipientAddressLines, recipientKindWords, type LetterRecipient} from '../../api/letters';
import {formatDate} from '../../utils/dates';
import {AppIcon} from '../shared/icons';
import {DispatchStatusToken} from './LetterCells';
import {recipientRole} from './RecipientsEditor';

export interface RecipientListProps {
  recipients: readonly LetterRecipient[];
  /** Show each one's dispatch: once the letter is approved (or withdrawn after it was). */
  showsDispatch: boolean;
  /** "Send by Secure Email" was pressed for a recipient (shown only where `canSendSecureEmail`). */
  onSendSecureEmail?: (recipient: LetterRecipient) => void;
  /** "Record as Sent" was pressed for a recipient (shown only where `canRecordSent`). */
  onRecordSent?: (recipient: LetterRecipient) => void;
  isBusy?: boolean;
}

/** How and when it went, and who sent it: "Post, 18 November 2026, by k.ashford". */
export function dispatchWords(recipient: LetterRecipient): string | null {
  if (recipient.dispatchStatus === 'NotSent' || !recipient.method) return null;
  return [dispatchMethodWords(recipient.method), recipient.sentOn ? formatDate(recipient.sentOn) : null, recipient.sentByUsername ? `by ${recipient.sentByUsername}` : null]
    .filter(Boolean)
    .join(', ');
}

/**
 * A letter's recipients, in order (the first is the one it's addressed to; the rest get copies), each with its name,
 * kind and address. Once it's approved, each one's dispatch: Not sent, Sent (how, when, by whom) or Failed, with
 * **Send by Secure Email** where the API allows it (`canSendSecureEmail`), or why it can't (`whyNoSecureEmail`), and
 * **Record as Sent** (`canRecordSent`), for post, by hand or another mailbox.
 */
export function RecipientList({recipients, showsDispatch, onSendSecureEmail, onRecordSent, isBusy = false}: RecipientListProps) {
  if (recipients.length === 0)
    return (
      <Text type="body" color="secondary">
        No recipients.
      </Text>
    );
  return (
    <List edge="flush" hasDividers aria-label="Recipients">
      {recipients.map((recipient, index) => {
        const address = recipientAddressLines(recipient);
        const how = dispatchWords(recipient);
        return (
          <ListItem
            key={recipient.id}
            align="first-line"
            label={
              <Text type="body" weight="semibold">
                {`${recipientRole(index)}: ${recipient.name || recipientKindWords(recipient.kind)}`}
              </Text>
            }
            description={
              <VStack gap={2}>
                <VStack gap={0}>
                  <Text type="supporting" color="secondary">
                    {recipientKindWords(recipient.kind)}
                  </Text>
                  {address.map((line, i) => (
                    <Text key={i} type="body">
                      {line}
                    </Text>
                  ))}
                  {recipient.email && <Text type="body">{recipient.email}</Text>}
                </VStack>
                {showsDispatch && (
                  <VStack gap={2}>
                    <HStack gap={2} vAlign="center" wrap="wrap">
                      <DispatchStatusToken status={recipient.dispatchStatus} />
                      {how && (
                        <Text type="supporting" color="secondary">
                          {how}
                        </Text>
                      )}
                    </HStack>
                    {recipient.dispatchStatus !== 'Sent' && recipient.whyNoSecureEmail && !recipient.canSendSecureEmail && (
                      <Text type="supporting" color="secondary">
                        {recipient.whyNoSecureEmail}
                      </Text>
                    )}
                    {(recipient.canSendSecureEmail || recipient.canRecordSent) && (
                      <HStack gap={2} wrap="wrap">
                        {recipient.canSendSecureEmail && onSendSecureEmail && (
                          <Button
                            label="Send by Secure Email"
                            variant="primary"
                            size="sm"
                            icon={<Icon icon={AppIcon.Send} />}
                            isDisabled={isBusy}
                            onClick={() => onSendSecureEmail(recipient)}
                          />
                        )}
                        {recipient.canRecordSent && onRecordSent && (
                          <Button
                            label="Record as Sent"
                            variant="secondary"
                            size="sm"
                            icon={<Icon icon={AppIcon.Check} />}
                            isDisabled={isBusy}
                            onClick={() => onRecordSent(recipient)}
                          />
                        )}
                      </HStack>
                    )}
                  </VStack>
                )}
              </VStack>
            }
          />
        );
      })}
    </List>
  );
}

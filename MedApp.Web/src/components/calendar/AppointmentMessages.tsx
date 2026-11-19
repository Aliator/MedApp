import {Heading} from '@astryxdesign/core/Heading';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {
  appointmentMessagesQuery,
  messageChannelLabel,
  messageKindLabel,
  messageOutcome,
  patientMessagingStatusQuery,
  type PatientMessage,
} from '../../api/patientMessages';
import {formatMobile} from '../../utils/contactDetails';
import {formatDateTime} from '../../utils/dates';
import {AppIcon} from '../shared/icons';

export interface AppointmentMessagesProps {
  appointmentId: string;
  /** Whether to read them: only while the dialog is open, for an appointment with a time. */
  isEnabled: boolean;
}

/** Where a message went, as staff read it: a mobile in national form, an email address as it is. */
const destinationWords = (message: PatientMessage) =>
  message.destination ? (message.channel === 'Text' ? formatMobile(message.destination) : message.destination) : null;

/** When a message last changed: when its status was last heard, when it went, or when it was made. */
const lastMoment = (message: PatientMessage) => message.statusAtUtc ?? message.sentAtUtc ?? message.createdAtUtc ?? '';

/**
 * The texts and emails to the patient about an appointment (centre plan decisions 129 to 133), newest first: what
 * each was (a reminder, a move, a cancellation, a missed appointment), how it went and where, where it stands
 * ("Waiting to send", "Delivered", "Failed"), when, and its text. One that couldn't go says why: "No reminder: no
 * mobile number". With nothing to show it says "No messages."; while messages are off and
 * there are none, it shows nothing at all.
 */
export function AppointmentMessages({appointmentId, isEnabled}: AppointmentMessagesProps) {
  const status = useQuery({...patientMessagingStatusQuery(), enabled: isEnabled});
  const messages = useQuery({...appointmentMessagesQuery(appointmentId), enabled: isEnabled && appointmentId !== ''});
  if (!isEnabled || messages.isPending) return null;
  const items = messages.data ?? [];
  // Quiet while messages are off: nothing to say about an appointment that has none (or whose list failed).
  if (items.length === 0 && status.data?.isOn !== true) return null;

  return (
    <VStack gap={2} hAlign="stretch">
      <Heading level={3}>
        Messages to the patient
      </Heading>
      {messages.isError ? (
        <Text type="body" color="secondary">
          The messages could not be loaded.
        </Text>
      ) : items.length === 0 ? (
        <Text type="body" color="secondary">
          No messages.
        </Text>
      ) : (
        <VStack gap={3} hAlign="stretch" role="list" aria-label="Messages to the patient">
          {items.map((message) => {
            const to = destinationWords(message);
            return (
              <HStack key={message.id} gap={2} vAlign="start" role="listitem">
                <Icon icon={message.channel === 'Email' ? AppIcon.Email : AppIcon.PatientMessage} color="secondary" />
                <StackItem size="fill">
                  <VStack gap={0.5}>
                    <Text type="body" weight="semibold">
                      {`${messageKindLabel(message.kind)}: ${messageChannelLabel(message.channel)}${to ? ` to ${to}` : ''}`}
                    </Text>
                    <Text type="supporting">{`${messageOutcome(message)} · ${formatDateTime(lastMoment(message))}`}</Text>
                    {message.body && (
                      <Text type="supporting" color="secondary">
                        {message.body}
                      </Text>
                    )}
                  </VStack>
                </StackItem>
              </HStack>
            );
          })}
        </VStack>
      )}
    </VStack>
  );
}

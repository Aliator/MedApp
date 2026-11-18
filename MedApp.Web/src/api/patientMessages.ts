import {queryOptions} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {appointmentKeys} from './appointments';

/**
 * Texts and emails to patients about their appointments (centre plan decisions 128 to 133): reminders, and messages
 * when staff move or cancel a booking, or a patient misses one. Each is a stored row, with what was sent, where, and
 * what became of it; one that couldn't go is kept too, with why. Messages never say why the appointment is: no
 * specialty, clinician, pathway or note.
 */
export type PatientMessage = Schemas['PatientMessageResponse'];
export type PatientMessagingStatus = Schemas['PatientMessagingStatusResponse'];
export type ExportedPatientMessage = Schemas['ExportedPatientMessageResponse'];

/** How a patient wants appointment messages, by the names the API uses, in the order the form offers them. */
export const REMINDER_CHANNELS = ['None', 'Text', 'Email', 'TextAndEmail'] as const;
export type ReminderChannel = (typeof REMINDER_CHANNELS)[number];

const CHANNEL_WORDS: Record<ReminderChannel, string> = {
  None: 'No reminders',
  Text: 'Text',
  Email: 'Email',
  TextAndEmail: 'Text and email',
};

/** A patient's reminder choice in words: "No reminders", "Text", "Email" or "Text and email". */
export const reminderChannelLabel = (channel: string | null | undefined): string =>
  CHANNEL_WORDS[(channel ?? 'None') as ReminderChannel] ?? channel ?? CHANNEL_WORDS.None;

/** Whether a choice asks for texts, or emails. */
export const wantsTexts = (channel: string | null | undefined) => channel === 'Text' || channel === 'TextAndEmail';
export const wantsEmails = (channel: string | null | undefined) => channel === 'Email' || channel === 'TextAndEmail';

/**
 * Whether "Tell the patient by text or email" starts ticked for an appointment: when the patient chose a channel
 * (decision 130). The server still sends only on channels they have a number or address for.
 */
export const tellsPatientByDefault = (channel: string | null | undefined) => !!channel && channel !== 'None';

/** Whether this deployment texts and emails patients (`GET api/appointments/patient-messages/status`). It rarely changes. */
export function patientMessagingStatusQuery() {
  return queryOptions({
    queryKey: ['patient-messages', 'status'] as const,
    queryFn: () => unwrap(api.GET('/api/appointments/patient-messages/status')),
    staleTime: 5 * 60_000,
  });
}

/**
 * The messages to the patient about one appointment, newest first (`GET api/appointments/{id}/messages`). Keyed under
 * the appointment, so a change to it (a move, a cancellation) refreshes them.
 */
export function appointmentMessagesQuery(id: string) {
  return queryOptions({
    queryKey: [...appointmentKeys.one(id), 'messages'] as const,
    queryFn: () => unwrap(api.GET('/api/appointments/{id}/messages', {params: {path: {id}}})),
  });
}

/** What a message was about: "Reminder", "Moved", "Cancelled" or "Missed appointment". */
export function messageKindLabel(kind: string | null | undefined): string {
  switch (kind) {
    case 'AppointmentReminder':
      return 'Reminder';
    case 'AppointmentMoved':
      return 'Moved';
    case 'AppointmentCancelled':
      return 'Cancelled';
    case 'MissedAppointment':
      return 'Missed appointment';
    default:
      return kind ?? '';
  }
}

/** How a message went: "Text" or "Email". */
export const messageChannelLabel = (channel: string | null | undefined): string => channel ?? '';

/** Where a message stands, in words. */
export function messageStatusLabel(status: string | null | undefined): string {
  switch (status) {
    case 'Pending':
      return 'Waiting to send';
    case 'Cancelled':
      return 'Not sent: no longer applied';
    case 'NotSent':
      return 'Not sent';
    default:
      return status ?? '';
  }
}

/** Why a message didn't go (or failed), in words; a provider's own status is shown as it came. */
export function messageReasonLabel(reason: string | null | undefined): string | null {
  switch (reason) {
    case null:
    case undefined:
    case '':
      return null;
    case 'NoConsent':
      return 'the patient chose no messages this way';
    case 'NoMobile':
      return 'no mobile number';
    case 'NoEmail':
      return 'no email address';
    case 'NoLongerApplies':
      return 'no longer applied';
    case 'ChoiceChanged':
      return 'the patient changed their choice';
    case 'RecordClosed':
      return 'the record is restricted or anonymised';
    case 'Rejected':
      return 'the provider refused it';
    case 'TooManyAttempts':
      return 'it failed too many times';
    case 'Unconfirmed':
      return "the provider didn't confirm it in time, so it may have gone; it isn't sent again";
    default:
      return reason;
  }
}

/** The reasons a message can't go because the patient gave no consent or contact (decision 133). */
const NO_CONSENT_OR_CONTACT = new Set(['NoConsent', 'NoMobile', 'NoEmail']);

/**
 * A message's status with why, in words: "Delivered", "Failed: no mobile number", and for a message that couldn't go
 * for want of consent or contact, "No reminder: no consent or contact (no mobile number)".
 */
export function messageOutcome(message: Pick<PatientMessage, 'kind' | 'status' | 'statusReason'>): string {
  const reason = messageReasonLabel(message.statusReason);
  if (message.status === 'NotSent' && NO_CONSENT_OR_CONTACT.has(message.statusReason ?? '')) {
    const what = message.kind === 'AppointmentReminder' ? 'No reminder' : 'No message';
    return `${what}: no consent or contact (${reason})`;
  }
  // A cancelled one says why it no longer went: "Not sent: no longer applied", or that the patient changed their choice.
  if (message.status === 'Cancelled') return reason ? `Not sent: ${reason}` : messageStatusLabel(message.status);
  const status = messageStatusLabel(message.status);
  return reason ? `${status}: ${reason}` : status;
}

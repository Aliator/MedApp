import {
  messageChannelLabel,
  messageKindLabel,
  messageOutcome,
  messageReasonLabel,
  messageStatusLabel,
  reminderChannelLabel,
  REMINDER_CHANNELS,
  tellsPatientByDefault,
  wantsEmails,
  wantsTexts,
} from './patientMessages';

describe('patient messages in words', () => {
  it('words each reminder choice, a missing one as none, and an unknown one as it came', () => {
    expect(REMINDER_CHANNELS.map(reminderChannelLabel)).toEqual(['No reminders', 'Text', 'Email', 'Text and email']);
    expect(reminderChannelLabel(null)).toBe('No reminders');
    expect(reminderChannelLabel('Pigeon')).toBe('Pigeon');
  });

  it('knows which choices want texts or emails, and which start Tell the patient ticked', () => {
    expect(REMINDER_CHANNELS.filter(wantsTexts)).toEqual(['Text', 'TextAndEmail']);
    expect(REMINDER_CHANNELS.filter(wantsEmails)).toEqual(['Email', 'TextAndEmail']);
    expect(REMINDER_CHANNELS.filter(tellsPatientByDefault)).toEqual(['Text', 'Email', 'TextAndEmail']);
    expect(tellsPatientByDefault(undefined)).toBe(false);
  });

  it('words each kind, channel, status and reason', () => {
    expect(['AppointmentReminder', 'AppointmentMoved', 'AppointmentCancelled', 'MissedAppointment', 'Other', null].map(messageKindLabel)).toEqual([
      'Reminder',
      'Moved',
      'Cancelled',
      'Missed appointment',
      'Other',
      '',
    ]);
    expect(messageChannelLabel('Text')).toBe('Text');
    expect(messageChannelLabel(null)).toBe('');
    expect(['Pending', 'Sent', 'Delivered', 'Failed', 'Cancelled', 'NotSent', null].map(messageStatusLabel)).toEqual([
      'Waiting to send',
      'Sent',
      'Delivered',
      'Failed',
      'Not sent: no longer needed',
      'Not sent',
      '',
    ]);
    expect(
      ['NoConsent', 'NoMobile', 'NoEmail', 'NoLongerApplies', 'ChoiceChanged', 'RecordClosed', 'Rejected', 'TooManyAttempts', 'Unconfirmed', 'permanent-failure', '', null].map(
        messageReasonLabel,
      ),
    ).toEqual([
      "the patient didn't choose messages this way",
      'no mobile number',
      'no email address',
      'no longer needed',
      'the patient changed their choice',
      'the record is restricted or anonymised',
      'the provider refused it',
      'it failed too many times',
      "the provider didn't confirm it in time, so it may have gone; it isn't sent again",
      'permanent-failure',
      null,
      null,
    ]);
  });

  it("says where a message stands, with why, and a reminder that couldn't go for want of consent or contact", () => {
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'Delivered'})).toBe('Delivered');
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'Pending'})).toBe('Waiting to send');
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'NotSent', statusReason: 'NoMobile'})).toBe(
      'No reminder: no mobile number',
    );
    expect(messageOutcome({kind: 'AppointmentMoved', status: 'NotSent', statusReason: 'NoConsent'})).toBe(
      "No message: the patient didn't choose messages this way",
    );
    expect(messageOutcome({kind: 'AppointmentReminder', channel: 'Text', status: 'NotSent', statusReason: 'NoConsent'})).toBe(
      "No reminder: the patient didn't choose texts",
    );
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'NotSent', statusReason: 'RecordClosed'})).toBe(
      'Not sent: the record is restricted or anonymised',
    );
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'Cancelled', statusReason: 'NoLongerApplies'})).toBe('Not sent: no longer needed');
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'Cancelled', statusReason: 'ChoiceChanged'})).toBe('Not sent: the patient changed their choice');
    expect(messageOutcome({kind: 'AppointmentReminder', status: 'Cancelled'})).toBe('Not sent: no longer needed');
    expect(messageOutcome({kind: 'AppointmentCancelled', status: 'Failed', statusReason: 'permanent-failure'})).toBe('Failed: permanent-failure');
  });
});

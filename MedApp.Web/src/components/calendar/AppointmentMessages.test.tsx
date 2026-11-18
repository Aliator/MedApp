import {Text} from '@astryxdesign/core/Text';
import {screen, within} from '@testing-library/react';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {AppointmentMessages} from './AppointmentMessages';

const ON = {mode: 'Offline', isOn: true, sendsMissedAppointmentMessages: true, reminderDaysBefore: 2};
const STATUS = 'GET /api/appointments/patient-messages/status';
const MESSAGES = 'GET /api/appointments/appt-1/messages';

const message = {
  id: 'message-1',
  appointmentId: 'appt-1',
  kind: 'AppointmentMoved',
  channel: 'Email',
  destination: 'test.patient@example.org',
  body: 'Your appointment has moved to Tue 24 Nov at 10:00.',
  subject: 'Your appointment has moved',
  appointmentStartUtc: '2026-11-24T10:00:00Z',
  status: 'Pending',
  requestedByUsername: 'test-user',
  createdAtUtc: '2026-11-20T09:00:00Z',
};

describe('AppointmentMessages', () => {
  it('lists each message newest first, in words, with its text', async () => {
    mockApi({[STATUS]: () => json(ON), [MESSAGES]: () => json([message])});
    renderWithApp(<AppointmentMessages appointmentId="appt-1" isEnabled />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Messages to the patient'})).toBeInTheDocument();
    const item = within(screen.getByRole('list', {name: 'Messages to the patient'})).getByRole('listitem');
    expect(item).toHaveTextContent('Moved: Email to test.patient@example.org');
    expect(item).toHaveTextContent('Waiting to send · 20 Nov 2026, 09:00');
    expect(item).toHaveTextContent(message.body);
  });

  it("shows a message that couldn't go without where or what, saying why", async () => {
    mockApi({[STATUS]: () => json({...ON, isOn: false}), [MESSAGES]: () => json([{...message, destination: '', body: '', status: 'NotSent', statusReason: 'NoEmail'}])});
    renderWithApp(<AppointmentMessages appointmentId="appt-1" isEnabled />, {user: staffUser});

    const item = await screen.findByRole('listitem');
    expect(item).toHaveTextContent(/^Moved: EmailNo message: no consent or contact \(no email address\)/);
  });

  it('says when the list could not be loaded while messages are on', async () => {
    mockApi({[STATUS]: () => json(ON), [MESSAGES]: () => json({title: 'Not found'}, 404)});
    renderWithApp(<AppointmentMessages appointmentId="appt-1" isEnabled />, {user: staffUser});

    expect(await screen.findByText('The messages could not be loaded.')).toBeInTheDocument();
  });

  it('reads nothing until enabled', async () => {
    const fetch = mockApi({[STATUS]: () => json(ON), [MESSAGES]: () => json([])});
    renderWithApp(
      <>
        <Text type="body">Ready</Text>
        <AppointmentMessages appointmentId="appt-1" isEnabled={false} />
      </>,
      {user: staffUser},
    );

    expect(await screen.findByText('Ready')).toBeInTheDocument();
    expect(screen.queryByRole('heading', {name: 'Messages to the patient'})).not.toBeInTheDocument();
    expect(calls(fetch).filter((c) => c.includes('/api/appointments/'))).toEqual([]);
  });
});

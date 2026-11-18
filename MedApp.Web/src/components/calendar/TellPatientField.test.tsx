import {screen} from '@testing-library/react';
import {Text} from '@astryxdesign/core/Text';
import {useState} from 'react';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {TellPatientField, usePatientMessagingOn} from './TellPatientField';

function Harness({isPlural, isOpen = true}: {isPlural?: boolean; isOpen?: boolean}) {
  const [value, setValue] = useState(true);
  const isOn = usePatientMessagingOn(isOpen);
  return isOn ? <TellPatientField value={value} onChange={setValue} isPlural={isPlural} /> : <Text type="body">Messages are off</Text>;
}

describe('TellPatientField', () => {
  it('offers to tell the patient, as they chose, never the reason, while messages are on', async () => {
    mockApi({'GET /api/appointments/patient-messages/status': () => json({mode: 'Live', isOn: true})});
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    const tell = await screen.findByRole('checkbox', {name: 'Tell the patient by text or email'});
    expect(tell).toBeChecked();
    expect(tell).toHaveAccessibleDescription('As they chose. The message gives the date, time and place, never the reason.');
    await user.click(tell);
    expect(tell).not.toBeChecked();
  });

  it('speaks of the patients for a session', async () => {
    mockApi({'GET /api/appointments/patient-messages/status': () => json({mode: 'Offline', isOn: true})});
    renderWithApp(<Harness isPlural />, {user: staffUser});

    expect(await screen.findByRole('checkbox', {name: 'Tell the patients by text or email'})).toBeInTheDocument();
  });

  it('is off while messages are off, when the status can’t be read, and while the dialog is closed', async () => {
    mockApi({'GET /api/appointments/patient-messages/status': () => json({mode: 'Off', isOn: false})});
    const {unmount} = renderWithApp(<Harness />, {user: staffUser});
    expect(await screen.findByText('Messages are off')).toBeInTheDocument();
    unmount();

    const fetch = mockApi({'GET /api/appointments/patient-messages/status': () => json({mode: 'Live', isOn: true})});
    renderWithApp(<Harness isOpen={false} />, {user: staffUser});
    expect(await screen.findByText('Messages are off')).toBeInTheDocument();
    expect(calls(fetch)).not.toContain('GET /api/appointments/patient-messages/status');
  });
});

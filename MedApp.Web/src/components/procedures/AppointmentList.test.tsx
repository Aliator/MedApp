import {screen, within} from '@testing-library/react';
import {testAppointment, testRequested} from '../../test/calendar';
import {renderWithApp} from '../../test/render';
import {AppointmentList} from './AppointmentList';

const booked = testAppointment({id: 'booked', date: '2026-11-23', start: '10:00', end: '10:30', stageName: 'Assessment', procedureTitle: 'Knee review'});
const requested = testRequested({id: 'requested', stageName: null, procedureTitle: null});

describe('AppointmentList', () => {
  it('lists each with its status, when and with whom (a link to the calendar), and its stage', async () => {
    renderWithApp(<AppointmentList appointments={[booked, requested]} label="Appointments" />);

    const list = within(await screen.findByRole('list', {name: 'Appointments'}));
    expect(list.getByRole('link', {name: 'Mon 23 Nov 2026, 10:00 to 10:30 with test-user'})).toHaveAttribute('href', '/calendar?appointment=booked');
    expect(list.getByText('Booked')).toBeInTheDocument();
    expect(list.getByText('Assessment')).toBeInTheDocument();
    expect(list.queryByText(/Knee review/)).not.toBeInTheDocument();
    expect(list.getByRole('link', {name: 'Not booked yet'})).toHaveAttribute('href', '/calendar?appointment=requested');
    expect(list.getByText('Needs booking')).toBeInTheDocument();
  });

  it('names each one’s procedure when asked', async () => {
    renderWithApp(<AppointmentList appointments={[booked]} showProcedure label="Upcoming" />);

    expect(await screen.findByText('Knee review, Assessment')).toBeInTheDocument();
  });
});

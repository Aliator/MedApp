import {screen, waitFor} from '@testing-library/react';
import {testAppointment, testBlock, testCalendar, testMeeting} from '../../test/calendar';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {DayCalendar} from './DayCalendar';

const MONDAY = '2026-11-23';

function mockDay(extra: Partial<Parameters<typeof testCalendar>[0]> = {}) {
  return mockApi({
    'GET /api/calendar': (request) => {
      const from = new URL(request.url).searchParams.get('From')!;
      return json(testCalendar({from, dayCount: 1, today: MONDAY, ...(from === MONDAY ? extra : {})}));
    },
  });
}

describe('DayCalendar', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it("shows today's appointments, blocks and meetings; an appointment opens its pathway, or its patient", async () => {
    mockDay({
      appointments: [
        testAppointment({id: 'a1', date: MONDAY, start: '09:00', end: '09:30'}),
        testAppointment({id: 'a2', date: MONDAY, start: '11:00', end: '11:30', pathwayId: null, pathwayTitle: null, patientId: 'patient-7'}),
      ],
      blocks: [testBlock(MONDAY, '12:00', '13:00')],
      meetings: [testMeeting({date: MONDAY, start: '14:00', end: '15:00', title: 'MDT'})],
    });
    const {user, router} = renderWithApp(<DayCalendar />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Today, Monday 23 November'})).toBeInTheDocument();
    expect(screen.queryByText('Nothing booked')).not.toBeInTheDocument();
    // Blocked time is shown, but isn't a button.
    expect(await screen.findByText('Lunch, 12:00 to 13:00')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Lunch/})).not.toBeInTheDocument();
    // So is a meeting, with its icon.
    expect(screen.getByText('MDT, meeting, 14:00 to 15:00, 2 attending')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^MDT/})).not.toBeInTheDocument();
    expect(document.querySelector('[data-kind="meeting"] svg')).not.toBeNull();
    // The card's title dates the day, so the grid doesn't head its column again (it's still named for screen readers).
    expect(screen.queryByText('Monday')).not.toBeInTheDocument();
    expect(screen.getByRole('group', {name: 'Monday 23 November, today'})).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: /^Test Patient, Knee review/}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways/pathway-1'));
    await user.click(screen.getByRole('button', {name: /^Test Patient, appointment/}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients/patient-7'));
  });

  it('goes between working days, and opens the full calendar on the day shown', async () => {
    const fetch = mockDay();
    const {user} = renderWithApp(<DayCalendar />, {user: staffUser});

    expect(await screen.findByText('Nothing booked')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open Calendar'})).toHaveAttribute('href', '/calendar?date=2026-11-23');
    await user.click(screen.getByRole('button', {name: 'Previous day'}));
    expect(await screen.findByRole('heading', {name: 'Friday 20 November'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open Calendar'})).toHaveAttribute('href', '/calendar?date=2026-11-20');
    await user.click(screen.getByRole('button', {name: 'Next day'}));
    await user.click(screen.getByRole('button', {name: 'Next day'}));
    expect(await screen.findByRole('heading', {name: 'Tuesday 24 November'})).toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-20&To=2026-11-20');
  });

  it('says when a day is not a working day, or the calendar cannot be loaded', async () => {
    mockApi({
      'GET /api/calendar': (request) => {
        const from = new URL(request.url).searchParams.get('From')!;
        return from === '2026-11-28' ? json(testCalendar({from, dayCount: 1, today: MONDAY})) : json({}, 500);
      },
    });
    const {unmount} = renderWithApp(<DayCalendar initialDate="2026-11-28" />, {user: staffUser});
    expect(await screen.findAllByText('Not a working day')).toHaveLength(2);
    unmount();

    renderWithApp(<DayCalendar />, {user: staffUser});
    expect(await screen.findByText('The calendar could not be loaded. Please try again.')).toBeInTheDocument();
  });
  it('names a closed day, and still shows what was on it', async () => {
    const closed = testCalendar({
      from: '2026-12-25',
      dayCount: 1,
      today: MONDAY,
      appointments: [testAppointment({date: '2026-12-25', start: '09:00', end: '09:30'})],
    });
    closed.days = closed.days!.map((d) => ({...d, isWorkingDay: false, closedDayName: 'Christmas Day'}));
    mockApi({'GET /api/calendar': () => json(closed)});
    renderWithApp(<DayCalendar initialDate="2026-12-25" />, {user: staffUser});

    expect(await screen.findByText('Closed: Christmas Day')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: /Test Patient, .*, on a closed day$/})).toBeInTheDocument();
  });

  it('says a closed day with nothing on it is closed, by name', async () => {
    const closed = testCalendar({from: '2026-12-25', dayCount: 1, today: MONDAY});
    closed.days = closed.days!.map((d) => ({...d, isWorkingDay: false, closedDayName: 'Christmas Day'}));
    mockApi({'GET /api/calendar': () => json(closed)});
    renderWithApp(<DayCalendar initialDate="2026-12-25" />, {user: staffUser});

    expect(await screen.findAllByText('Closed: Christmas Day')).toHaveLength(2);
  });

});

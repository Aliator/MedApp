import {screen} from '@testing-library/react';
import {testCalendar} from '../../test/calendar';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {ukToday} from '../../utils/calendarSlots';
import {HomePage} from './HomePage';

describe('HomePage', () => {
  it("welcomes the signed-in user, with today's calendar and a way to the full one", async () => {
    const today = ukToday();
    mockApi({'GET /api/calendar': () => json(testCalendar({from: today, dayCount: 1, today}))});
    renderWithApp(<HomePage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: `Welcome, ${staffUser.username}`, level: 1})).toBeInTheDocument();
    expect(await screen.findByRole('heading', {name: /^Today, /})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open Calendar'})).toHaveAttribute('href', '/calendar');
  });
});

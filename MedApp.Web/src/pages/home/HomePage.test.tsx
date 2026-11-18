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
    expect(screen.getByRole('link', {name: 'Open Calendar'})).toHaveAttribute('href', `/calendar?date=${today}`);
  });

  it('shows what needs your attention above the day, linking to its page', async () => {
    const today = ukToday();
    mockApi({
      'GET /api/calendar': () => json(testCalendar({from: today, dayCount: 1, today})),
      'GET /api/referrals/awaiting-triage/count': () => json({count: 2, inYourTeams: false}),
    });
    renderWithApp(<HomePage />, {user: {...staffUser, permissions: [...staffUser.permissions, 'TriageReferrals']}});

    expect(await screen.findByRole('link', {name: '2 referrals awaiting triage'})).toHaveAttribute('href', '/referrals');
    expect(await screen.findByRole('heading', {name: /^Today, /})).toBeInTheDocument();
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import type {CurrentUser} from '../../auth/auth';
import type {Permission} from '../../auth/permissions';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {attentionLines, NeedsAttention} from './NeedsAttention';

const COUNTS = {
  'GET /api/results/count': () => json({awaitingMine: 4}),
  'GET /api/letters/count': () => json({toApprove: 2, toSend: 1}),
  'GET /api/referrals/awaiting-triage/count': () => json({count: 3, inYourTeams: true}),
};

/** Someone with exactly these permissions. */
const withPermissions = (...permissions: Permission[]): CurrentUser => ({...staffUser, permissions: ['UseApp', ...permissions]});

/** Someone who can act on every line. */
const EVERY_LINE = withPermissions('ViewPatients', 'SignOffResults', 'ApproveLetters', 'SendLetters', 'TriageReferrals');

const card = () => screen.findByRole('list', {name: 'Needs your attention'});

describe('NeedsAttention', () => {
  it('lists each count above zero as a link to its page, for someone who can act on them all', async () => {
    mockApi(COUNTS);
    renderWithApp(<NeedsAttention />, {user: EVERY_LINE});

    const list = within(await card());
    expect(await list.findByRole('link', {name: '4 results awaiting your sign-off'})).toHaveAttribute('href', '/results');
    expect(list.getByRole('link', {name: '2 letters to approve'})).toHaveAttribute('href', '/letters');
    expect(list.getByRole('link', {name: '1 letter to send'})).toHaveAttribute('href', '/letters');
    expect(list.getByRole('link', {name: '3 referrals awaiting triage in your teams'})).toHaveAttribute('href', '/referrals');
    expect(screen.getByRole('heading', {name: 'Needs your attention'})).toBeInTheDocument();
  });

  it('is not there when everything is zero', async () => {
    const fetch = mockApi({
      'GET /api/results/count': () => json({awaitingMine: 0}),
      'GET /api/letters/count': () => json({toApprove: 0, toSend: 0}),
      'GET /api/referrals/awaiting-triage/count': () => json({count: 0, inYourTeams: false}),
    });
    renderWithApp(<NeedsAttention />, {user: EVERY_LINE});

    await waitFor(() => expect(calls(fetch)).toContain('GET /api/referrals/awaiting-triage/count'));
    expect(screen.queryByRole('list', {name: 'Needs your attention'})).not.toBeInTheDocument();
  });

  it('shows only the lines someone can act on, and asks for no count they cannot', async () => {
    const fetch = mockApi(COUNTS);
    renderWithApp(<NeedsAttention />, {user: withPermissions('ViewPatients', 'SendLetters')});

    const list = within(await card());
    expect(await list.findByRole('link', {name: '1 letter to send'})).toBeInTheDocument();
    expect(list.queryByRole('link', {name: /to approve/})).not.toBeInTheDocument();
    expect(list.getAllByRole('link')).toHaveLength(1);
    expect(calls(fetch)).not.toContain('GET /api/results/count');
    expect(calls(fetch)).not.toContain('GET /api/referrals/awaiting-triage/count');
  });

  it('asks for nothing without ViewPatients, which every count needs', async () => {
    const fetch = mockApi(COUNTS);
    renderWithApp(<NeedsAttention />, {user: withPermissions('SignOffResults', 'ApproveLetters', 'SendLetters', 'TriageReferrals')});

    // Give any request a moment to be made.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(calls(fetch).filter((call) => call.includes('/count'))).toEqual([]);
    expect(screen.queryByRole('list', {name: 'Needs your attention'})).not.toBeInTheDocument();
  });
});

describe('attentionLines', () => {
  it('says one in the singular, and the triage count without "in your teams" for someone in none', () => {
    const lines = attentionLines({resultsAwaitingMine: 1, referralsAwaitingTriage: 1, inYourTeams: false});
    expect(lines.map((line) => `${line.count} ${line.words(line.count)}`)).toEqual([
      '1 result awaiting your sign-off',
      '1 referral awaiting triage',
    ]);
  });

  it('has no lines when nothing is known', () => {
    expect(attentionLines({})).toEqual([]);
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import type {CurrentUser} from '../../auth/auth';
import type {Permission} from '../../auth/permissions';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {attentionLines, NeedsAttention} from './NeedsAttention';

const COUNTS = {
  'GET /api/results/count': () => json({awaitingMine: 4}),
  'GET /api/letters/count': () => json({toApprove: 2, toSend: 1}),
  'GET /api/referrals/awaiting-triage/count': () => json({count: 3, inYourTeams: true}),
  'GET /api/lab-cases/count': () => json({atRisk: 2, overdue: 1, toReport: 5}),
  'GET /api/data-requests/count': () => json({open: 3, dueSoon: 1}),
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

  it('shows the lab its cases overdue, at risk and ready to report, each linking to its view', async () => {
    const fetch = mockApi(COUNTS);
    renderWithApp(<NeedsAttention />, {user: withPermissions('ViewPatients', 'ProcessSpecimens')});

    const list = within(await card());
    expect(await list.findByRole('link', {name: '1 lab case overdue'})).toHaveAttribute('href', '/lab/cases?view=Overdue');
    expect(list.getByRole('link', {name: '2 lab cases at risk'})).toHaveAttribute('href', '/lab/cases?view=AtRisk');
    expect(list.getByRole('link', {name: '5 lab cases ready to report'})).toHaveAttribute('href', '/lab/cases?view=ToReport');
    expect(list.getAllByRole('link')).toHaveLength(3);
    expect(calls(fetch)).not.toContain('GET /api/data-requests/count');
  });

  it('shows an admin the data requests due soon and open, and asks for no lab count', async () => {
    const fetch = mockApi(COUNTS);
    renderWithApp(<NeedsAttention />, {user: withPermissions('ViewPatients', 'ManagePrivacy')});

    const list = within(await card());
    expect(await list.findByRole('link', {name: '1 data request due within 7 days or overdue'})).toHaveAttribute('href', '/data-requests');
    expect(list.getByRole('link', {name: '3 data requests open'})).toHaveAttribute('href', '/data-requests');
    expect(list.getAllByRole('link')).toHaveLength(2);
    expect(calls(fetch)).not.toContain('GET /api/lab-cases/count');
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
    renderWithApp(<NeedsAttention />, {
      user: withPermissions('SignOffResults', 'ApproveLetters', 'SendLetters', 'TriageReferrals', 'ProcessSpecimens', 'ManagePrivacy'),
    });

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

  it('says a lab case and a data request in the singular too', () => {
    const lines = attentionLines({labCasesAtRisk: 1, labCasesToReport: 1, dataRequestsOpen: 1});
    expect(lines.map((line) => `${line.count} ${line.words(line.count)}`)).toEqual([
      '1 lab case at risk',
      '1 lab case ready to report',
      '1 data request open',
    ]);
  });

  it('has no lines when nothing is known', () => {
    expect(attentionLines({})).toEqual([]);
  });
});

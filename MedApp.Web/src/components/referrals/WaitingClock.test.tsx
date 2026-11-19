import {screen, within} from '@testing-library/react';
import type {ClockPause, WaitingClock} from '../../api/referrals';
import {renderWithApp, staffUser} from '../../test/render';
import {ClockPauseList, ClockTokens, reviewWords, WaitCell, WaitingClockDetails} from './WaitingClock';

const running: WaitingClock = {
  state: 'Running',
  targetDays: 126,
  waitStartOn: '2026-08-26',
  targetOn: '2026-12-30',
  daysWaited: 84,
  pausedDays: 0,
  isBreached: false,
  isReviewOverdue: false,
};

describe('WaitingClock', () => {
  afterEach(() => localStorage.clear());

  it('shows a running clock as its day of the target, with no flags', async () => {
    renderWithApp(<WaitCell clock={running} />, {user: staffUser});
    expect(await screen.findByText('Day 84 of 126')).toBeInTheDocument();
    expect(screen.queryByText('Wait breached')).not.toBeInTheDocument();
    expect(reviewWords(running)).toBeNull();
  });

  it('flags a breached, paused clock with its review date, overdue, with icons when asked for', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    const clock: WaitingClock = {
      ...running,
      state: 'Paused',
      daysWaited: 130,
      isBreached: true,
      pausedSinceOn: '2026-11-01',
      pauseReviewOn: '2026-11-10',
      isReviewOverdue: true,
    };
    renderWithApp(<WaitCell clock={clock} />, {user: staffUser});
    expect(await screen.findByText('Day 130 of 126')).toBeInTheDocument();
    expect(screen.getByText('Review 10 Nov 2026')).toBeInTheDocument();
    expect(screen.getByText('Wait breached')).toBeInTheDocument();
    expect(screen.getByText('Paused')).toBeInTheDocument();
    expect(screen.getByText('Review overdue')).toBeInTheDocument();
    expect(document.querySelectorAll('svg').length).toBeGreaterThanOrEqual(3);
  });

  it('says when there is no clock, and flags a stopped one', async () => {
    renderWithApp(
      <>
        <WaitCell clock={null} />
        <ClockTokens clock={{...running, state: 'Stopped'}} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('No clock')).toBeInTheDocument();
    expect(screen.getByText('Stopped')).toBeInTheDocument();
  });

  it('gives a paused clock in full', async () => {
    const clock: WaitingClock = {
      ...running,
      state: 'Paused',
      pausedDays: 3,
      waitStartOn: '2026-08-29',
      pausedSinceOn: '2026-11-01',
      pauseReviewOn: '2026-11-10',
      isReviewOverdue: true,
    };
    renderWithApp(<WaitingClockDetails clock={clock} />, {user: staffUser});
    expect(await screen.findByText('30 December 2026')).toBeInTheDocument();
    expect(screen.getByText('29 August 2026')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('1 November 2026')).toBeInTheDocument();
    expect(screen.getByText('10 November 2026')).toBeInTheDocument();
    expect(screen.getByText('Review overdue')).toBeInTheDocument();
    expect(screen.queryByText('Why it stopped')).not.toBeInTheDocument();
  });

  it('gives a stopped clock with when and why', async () => {
    renderWithApp(
      <WaitingClockDetails clock={{...running, state: 'Stopped', stoppedOn: '2026-11-12', stopReason: 'StopStageFinished'}} />,
      {user: staffUser},
    );
    expect(await screen.findByText('12 November 2026')).toBeInTheDocument();
    expect(screen.getByText('Clock-stop stage finished')).toBeInTheDocument();
    expect(screen.queryByText('Review date')).not.toBeInTheDocument();
  });

  it('shows dashes where a clock has no dates', async () => {
    renderWithApp(
      <>
        <WaitingClockDetails clock={{state: 'Paused'}} />
        <WaitingClockDetails clock={{state: 'Stopped'}} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findAllByText('—')).toHaveLength(8);
    expect(screen.getAllByText('Day 0 of 0')).toHaveLength(2);
  });

  it('lists pauses: why, who, when, and whether it still lasts', async () => {
    const pauses: ClockPause[] = [
      {
        id: 'p1',
        pausedOn: '2026-10-01',
        pausedByUsername: 'k.ashford',
        reason: 'PatientChoseToWait',
        detail: 'Away until\nOctober',
        reviewOn: '2026-10-20',
        resumedOn: '2026-10-05',
        resumedByUsername: 's.brightwell',
        days: 4,
      },
      {id: 'p2', pausedOn: '2026-10-06', pausedByUsername: '', reason: 'Other', reviewOn: '2026-10-30', resumedOn: '2026-10-07', days: 1},
      {id: 'p3', pausedOn: '2026-11-01', pausedByUsername: 'k.ashford', reason: 'PatientUnavailable', reviewOn: '2026-11-10'},
    ];
    renderWithApp(<ClockPauseList pauses={pauses} />, {user: staffUser});
    const list = await screen.findByRole('list', {name: 'Pauses'});
    expect(within(list).getByText('Patient chose to wait')).toBeInTheDocument();
    expect(within(list).getByText(/Away until/)).toBeInTheDocument();
    expect(within(list).getByText('Paused 1 Oct 2026 by k.ashford, review 20 Oct 2026')).toBeInTheDocument();
    expect(within(list).getByText('Resumed 5 Oct 2026 by s.brightwell (4 days)')).toBeInTheDocument();
    expect(within(list).getByText('Paused 6 Oct 2026 by someone, review 30 Oct 2026')).toBeInTheDocument();
    expect(within(list).getByText('Resumed 7 Oct 2026 (1 day)')).toBeInTheDocument();
    expect(within(list).getByText('Patient unavailable')).toBeInTheDocument();
    expect(within(list).getByText('Still paused')).toBeInTheDocument();
  });

  it('says when there are no pauses', async () => {
    renderWithApp(<ClockPauseList pauses={[]} />, {user: staffUser});
    expect(await screen.findByText('No pauses.')).toBeInTheDocument();
  });
});

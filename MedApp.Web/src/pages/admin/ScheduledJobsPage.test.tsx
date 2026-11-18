import {screen, waitFor, within} from '@testing-library/react';
import type {ScheduledJob} from '../../api/jobs';
import {adminUser, calls, expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {countWords, failureWords, jobLabels, runOutcomeWords, ScheduledJobsPage, whyCantRun} from './ScheduledJobsPage';

const JOBS = '/api/jobs';

/** The purge: it ran this morning, deleting 12 rows. */
const PURGE: ScheduledJob = {
  name: 'operational-purge',
  title: 'Operational data purge',
  description: 'Deletes sign-in events, sessions and other operational data past their retention period.',
  schedule: 'Daily at 04:30 (UK time)',
  enabled: true,
  isRunning: false,
  isOverdue: false,
  nextRunUtc: '2026-11-19T04:30:00Z',
  lastStartedAtUtc: '2026-11-18T04:30:00Z',
  lastFinishedAtUtc: '2026-11-18T04:30:05Z',
  lastOutcome: 'Succeeded',
  lastCount: 12,
  consecutiveFailures: 0,
  lastErrorType: null,
};

/** A job that has failed three times running, and is late. */
const REMINDERS: ScheduledJob = {
  name: 'appointment-reminders',
  title: 'Appointment reminders',
  description: 'Sends the reminders due in the next quarter of an hour.',
  schedule: 'Every 15 minutes',
  enabled: true,
  isRunning: false,
  isOverdue: true,
  nextRunUtc: '2026-11-18T09:15:00Z',
  lastStartedAtUtc: '2026-11-18T09:00:00Z',
  lastFinishedAtUtc: '2026-11-18T09:00:02Z',
  lastOutcome: 'Failed',
  lastCount: null,
  consecutiveFailures: 3,
  lastErrorType: 'SqlException',
};

function renderPage(jobs: ScheduledJob[] = [PURGE, REMINDERS], extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${JOBS}`]: () => json(jobs), ...extra});
  return {fetch, ...renderWithApp(<ScheduledJobsPage />, {path: '/admin/jobs', user: adminUser})};
}

const rowOf = async (title: string) => (await screen.findByText(title)).closest('tr')!;
const dialog = () => within(openDialog()!);
const runButton = (row: HTMLElement) => within(row).getByRole('button', {name: /^Run Now/});

describe('ScheduledJobsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T10:00:00Z')});
  });

  it('lists each job with its schedule, status, last run and next run, and says how jobs run', async () => {
    const {fetch} = renderPage();

    const purge = await rowOf('Operational data purge');
    expect(within(purge).getByText('operational-purge')).toBeInTheDocument();
    expect(within(purge).getByText(PURGE.description!)).toBeInTheDocument();
    expect(within(purge).getByText('Daily at 04:30 (UK time)')).toBeInTheDocument();
    expect(within(purge).getByText('Succeeded')).toBeInTheDocument();
    // In UK time (GMT in November), whatever the browser's zone.
    expect(within(purge).getByText('18 Nov 2026, 04:30')).toBeInTheDocument();
    expect(within(purge).getByText('12 items')).toBeInTheDocument();
    expect(within(purge).getByText('19 Nov 2026, 04:30')).toBeInTheDocument();

    const reminders = await rowOf('Appointment reminders');
    expect(within(reminders).getByText('Overdue')).toBeInTheDocument();
    expect(within(reminders).getByText('Failed')).toBeInTheDocument();
    expect(within(reminders).getByText('Failed 3 times in a row (SqlException)')).toBeInTheDocument();

    expect(screen.getByRole('heading', {name: 'Scheduled Jobs', level: 1})).toBeInTheDocument();
    expect(screen.getByText('2 jobs')).toBeInTheDocument();
    expect(screen.getByText(/Only one copy of the API runs each job at a time/)).toBeInTheDocument();
    expect(screen.getByText(/fails three times in a row notifies everyone who manages setup/)).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${JOBS}`]);
  });

  it('says when there are none', async () => {
    renderPage([]);
    expect(await screen.findByText('No scheduled jobs')).toBeInTheDocument();
  });

  it("says when the jobs can't be loaded", async () => {
    mockApi({[`GET ${JOBS}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<ScheduledJobsPage />, {path: '/admin/jobs', user: adminUser});
    expect(await screen.findByText('The scheduled jobs could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('runs a job now after asking, showing what it does, then shows and announces what came of it', async () => {
    withAnnouncements();
    const ran = {...PURGE, lastStartedAtUtc: '2026-11-18T10:00:00Z', lastFinishedAtUtc: '2026-11-18T10:00:03Z', lastCount: 4};
    let listed = 0;
    const {user, fetch} = renderPage([PURGE, REMINDERS], {
      [`GET ${JOBS}`]: () => json(++listed === 1 ? [PURGE, REMINDERS] : [ran, REMINDERS]),
      [`POST ${JOBS}/operational-purge/run`]: () => json(ran),
    });

    await user.click(runButton(await rowOf('Operational data purge')));
    expect(dialog().getByRole('heading', {name: 'Run Operational data purge now?'})).toBeInTheDocument();
    expect(dialog().getByText(PURGE.description!)).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Run Now'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch)).toContain(`POST ${JOBS}/operational-purge/run`);
    expect(await screen.findByText('Operational data purge succeeded: 4 items.')).toBeInTheDocument();
    await expectAnnounced('Operational data purge succeeded: 4 items.');
    // The row shows the run.
    expect(await within(await rowOf('Operational data purge')).findByText('18 Nov 2026, 10:00')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: /^Dismiss/}));
    await waitFor(() => expect(screen.queryByRole('button', {name: /^Dismiss/})).not.toBeInTheDocument());
  });

  it('says when a run now failed', async () => {
    const failed = {...PURGE, lastOutcome: 'Failed', lastCount: null, consecutiveFailures: 1, lastErrorType: 'TimeoutException'};
    const {user} = renderPage([PURGE], {[`POST ${JOBS}/operational-purge/run`]: () => json(failed)});

    await user.click(runButton(await rowOf('Operational data purge')));
    await user.click(dialog().getByRole('button', {name: 'Run Now'}));

    expect(await screen.findByText('Operational data purge failed (TimeoutException).')).toBeInTheDocument();
  });

  it('runs nothing when Run Now is called off', async () => {
    const {user, fetch} = renderPage();

    await user.click(runButton(await rowOf('Operational data purge')));
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
  });

  it("shows the API's reason when it refuses, and reads the jobs again", async () => {
    const {user, fetch} = renderPage([PURGE], {
      [`POST ${JOBS}/operational-purge/run`]: () => json({title: 'Operational data purge is already running on another copy of the API.', status: 409}, 409),
    });

    await user.click(runButton(await rowOf('Operational data purge')));
    await user.click(dialog().getByRole('button', {name: 'Run Now'}));

    expect(await screen.findByText('Operational data purge is already running on another copy of the API.')).toBeInTheDocument();
    await waitFor(() => expect(openDialog()).toBeNull());
    await waitFor(() => expect(calls(fetch).filter((c) => c === `GET ${JOBS}`)).toHaveLength(2));
  });

  it("can't run a job that's running or switched off, saying why", async () => {
    renderPage([
      {...PURGE, isRunning: true},
      {...REMINDERS, enabled: false, isOverdue: false, nextRunUtc: null},
    ]);

    const purge = await rowOf('Operational data purge');
    expect(within(purge).getByText('Running')).toBeInTheDocument();
    const running = runButton(purge);
    expect(isDisabled(running)).toBe(true);
    expect(running).toHaveAccessibleName('Run Now: Operational data purge, already running');

    const reminders = await rowOf('Appointment reminders');
    expect(within(reminders).getByText('Off')).toBeInTheDocument();
    expect(within(reminders).getByText('Not scheduled')).toBeInTheDocument();
    expect(runButton(reminders)).toHaveAccessibleName("Run Now: Appointment reminders, switched off in the API's settings");
    expect(isDisabled(runButton(reminders))).toBe(true);
  });

  it('reads the jobs again on Refresh', async () => {
    const {user, fetch} = renderPage();

    await rowOf('Operational data purge');
    await user.click(screen.getByRole('button', {name: 'Refresh'}));

    await waitFor(() => expect(calls(fetch).filter((c) => c === `GET ${JOBS}`)).toHaveLength(2));
  });
});

describe('jobLabels', () => {
  it('puts running, off and overdue before the last outcome, or Not run yet', () => {
    expect(jobLabels(PURGE).map((l) => l.label)).toEqual(['Succeeded']);
    expect(jobLabels(REMINDERS).map((l) => l.label)).toEqual(['Overdue', 'Failed']);
    expect(jobLabels({...PURGE, isRunning: true, enabled: false, lastOutcome: null}).map((l) => l.label)).toEqual(['Running', 'Off', 'Not run yet']);
    expect(jobLabels({...PURGE, lastOutcome: 'Skipped'}).map((l) => l.label)).toEqual(['Skipped']);
    expect(jobLabels({...PURGE, lastOutcome: 'Something new'}).map((l) => l.label)).toEqual(['Something new']);
  });
});

describe('words', () => {
  it('counts items', () => {
    expect(countWords(1)).toBe('1 item');
    expect(countWords(0)).toBe('0 items');
    expect(countWords(1284)).toBe('1,284 items');
  });

  it('says how many times running a job has failed, if any', () => {
    expect(failureWords(PURGE)).toBeNull();
    expect(failureWords({...REMINDERS, consecutiveFailures: 1})).toBe('Failed once (SqlException)');
    expect(failureWords({...REMINDERS, lastErrorType: null})).toBe('Failed 3 times in a row');
  });

  it("says why a job can't run now", () => {
    expect(whyCantRun(PURGE)).toBeNull();
    expect(whyCantRun({...PURGE, isRunning: true})).toBe('Already running');
    expect(whyCantRun({...PURGE, enabled: false})).toBe("Switched off in the API's settings");
  });

  it('says what a run did', () => {
    expect(runOutcomeWords({...PURGE, lastCount: null})).toBe('Operational data purge succeeded.');
    expect(runOutcomeWords({...PURGE, lastOutcome: 'Failed', lastErrorType: null})).toBe('Operational data purge failed.');
    expect(runOutcomeWords({...PURGE, lastOutcome: 'Skipped'})).toBe('Operational data purge was skipped.');
    expect(runOutcomeWords({...PURGE, lastOutcome: null, title: null})).toBe('operational-purge ran.');
  });
});

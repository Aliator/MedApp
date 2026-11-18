import {screen, waitFor, within} from '@testing-library/react';
import type {PathwayReferral} from '../../api/referrals';
import {addDays, ukToday} from '../../utils/calendarSlots';
import {calls, expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {REFERRAL_ID, REFERRAL_PATIENT} from '../../test/referrals';
import {PAUSE_EXPLANATION, ReferralWaitCard, reviewDateRange} from './ReferralWaitCard';

const PATHWAY_ID = '00000043-0000-0000-0000-000000000001';
const PAUSE = `POST /api/referrals/${REFERRAL_ID}/clock/pause`;
const RESUME = `POST /api/referrals/${REFERRAL_ID}/clock/resume`;

const running: PathwayReferral = {
  id: REFERRAL_ID,
  receivedOn: '2026-11-02',
  specialtyName: 'Dermatology',
  version: 4,
  clock: {state: 'Running', targetDays: 28, waitStartOn: '2026-11-02', targetOn: '2026-11-30', daysWaited: 16, pausedDays: 0, isBreached: false},
  pauses: [],
  canPauseClock: true,
  canResumeClock: false,
};

const paused: PathwayReferral = {
  ...running,
  clock: {...running.clock, state: 'Paused', pausedSinceOn: '2026-11-10', pauseReviewOn: '2026-12-01'},
  pauses: [{id: 'p1', pausedOn: '2026-11-10', pausedByUsername: 'k.ashford', reason: 'PatientUnavailable', reviewOn: '2026-12-01'}],
  canPauseClock: false,
  canResumeClock: true,
};

/** A UK date `days` from today, as typed into a date field (dd/mm/yyyy), and as sent (yyyy-mm-dd). */
function dayAhead(days: number) {
  const iso = addDays(ukToday(), days);
  const [y, m, d] = iso.split('-');
  return {iso, typed: `${d}/${m}/${y}`};
}

function render(referral: PathwayReferral, handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [PAUSE]: () => json(paused),
    [RESUME]: () => json(running),
    ...handlers,
  });
  const result = renderWithApp(<ReferralWaitCard referral={referral} pathwayId={PATHWAY_ID} patient={REFERRAL_PATIENT} />, {user: staffUser});
  return {...result, fetch};
}

const dialog = () => within(openDialog()!);
const posted = async (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!).json();

describe('ReferralWaitCard', () => {
  it('shows the referral, its clock and pauses, and Pause Clock while it runs', async () => {
    render(running);

    expect(await screen.findByText('Received 2 November 2026, Dermatology')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open the referral'})).toHaveAttribute('href', `/referrals/${REFERRAL_ID}`);
    expect(screen.getByText('Day 16 of 28')).toBeInTheDocument();
    expect(screen.getByText('30 November 2026')).toBeInTheDocument();
    expect(screen.getByText('No pauses.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Pause Clock'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Resume Clock'})).not.toBeInTheDocument();
  });

  it('says when there is no clock, and offers nothing the API does not allow', async () => {
    render({...running, clock: undefined, canPauseClock: false});
    expect(await screen.findByText('This referral has no waiting-time clock.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /Clock/})).not.toBeInTheDocument();
  });

  it('pauses it: a reason, a detail for Other, a review date tomorrow to 90 days ahead, from the version shown', async () => {
    withAnnouncements();
    const {user, fetch} = render(running);

    await user.click(await screen.findByRole('button', {name: 'Pause Clock'}));
    expect(await dialog().findByText(PAUSE_EXPLANATION)).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('Referral to Dermatology, received 2 November 2026: day 16 of 28.')).toBeInTheDocument();
    // Nothing is chosen for them.
    expect(dialog().getByRole('radio', {name: 'Patient chose to wait'})).not.toBeChecked();
    const confirm = () => dialog().getByRole('button', {name: 'Pause Clock'});
    expect(isDisabled(confirm())).toBe(true);

    await user.click(dialog().getByRole('radio', {name: 'Other'}));
    expect(dialog().getByRole('textbox', {name: /^Detail/})).toBeRequired();
    const review = dayAhead(10);
    await user.type(dialog().getByRole('combobox', {name: /Review date/}), review.typed);
    // Other needs a detail.
    expect(isDisabled(confirm())).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /^Detail/}), 'Synthetic: moving house.');
    await waitFor(() => expect(isDisabled(confirm())).toBe(false));
    await user.click(confirm());

    await expectAnnounced(/^Clock paused until its review on/);
    expect(await posted(fetch)).toEqual({reason: 'Other', detail: 'Synthetic: moving house.', reviewOn: review.iso, expectedVersion: 4});
    await waitFor(() => expect(openDialog()).toBeNull());
  }, 15_000);

  it('sends no detail when none is given, and keeps a too-long detail or a date out of range from being sent', async () => {
    const {user, fetch} = render(running);

    await user.click(await screen.findByRole('button', {name: 'Pause Clock'}));
    await user.click(await dialog().findByRole('radio', {name: 'Patient chose to wait'}));
    expect(dialog().getByRole('textbox', {name: 'Detail (optional)'})).not.toBeRequired();
    const confirm = () => dialog().getByRole('button', {name: 'Pause Clock'});
    await user.type(dialog().getByRole('combobox', {name: /Review date/}), dayAhead(91).typed);
    expect(isDisabled(confirm())).toBe(true);
    await user.clear(dialog().getByRole('combobox', {name: /Review date/}));
    await user.type(dialog().getByRole('combobox', {name: /Review date/}), dayAhead(90).typed);
    await waitFor(() => expect(isDisabled(confirm())).toBe(false));
    await user.click(dialog().getByRole('textbox', {name: 'Detail (optional)'}));
    await user.paste('x'.repeat(501));
    expect(await dialog().findByText('At most 500 characters.')).toBeInTheDocument();
    expect(isDisabled(confirm())).toBe(true);
    await user.clear(dialog().getByRole('textbox', {name: 'Detail (optional)'}));
    await user.click(confirm());

    await waitFor(() => expect(calls(fetch)).toContain(PAUSE));
    expect(await posted(fetch)).toMatchObject({reason: 'PatientChoseToWait', detail: null, reviewOn: dayAhead(90).iso});
  }, 15_000);

  it("shows a 400 on its fields and a 409 in the API's words, staying open", async () => {
    let tries = 0;
    const {user} = render(running, {
      [PAUSE]: () =>
        ++tries === 1
          ? json({title: 'Validation failed', status: 400, errors: {ReviewOn: ['The review date must be within 90 days.']}}, 400)
          : json({title: 'Someone changed this referral meanwhile.', status: 409}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'Pause Clock'}));
    await user.click(await dialog().findByRole('radio', {name: 'Patient unavailable'}));
    await user.type(dialog().getByRole('combobox', {name: /Review date/}), dayAhead(5).typed);
    await user.click(dialog().getByRole('button', {name: 'Pause Clock'}));
    expect((await dialog().findAllByText('The review date must be within 90 days.')).length).toBeGreaterThan(0);

    await user.click(dialog().getByRole('button', {name: 'Pause Clock'}));
    expect(await dialog().findByText('Someone changed this referral meanwhile.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  }, 15_000);

  it('resumes a paused clock after asking, from the version shown', async () => {
    withAnnouncements();
    const {user, fetch} = render(paused);

    expect(await screen.findByText('Paused')).toBeInTheDocument();
    expect(within(screen.getByRole('list', {name: 'Pauses'})).getByText('Patient unavailable')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Pause Clock'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Resume Clock'}));
    expect(await dialog().findByText('Paused since 10 November 2026, for review on 1 December 2026.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Resume Clock'}));

    await expectAnnounced('Clock resumed');
    expect(await posted(fetch)).toEqual({expectedVersion: 4});
  });

  it("shows a refused resume in the API's words, and cancels", async () => {
    const {user, fetch} = render(paused, {[RESUME]: () => json({title: 'The clock is no longer paused.', status: 409}, 409)});

    await user.click(await screen.findByRole('button', {name: 'Resume Clock'}));
    await user.click(await dialog().findByRole('button', {name: 'Resume Clock'}));
    expect(await dialog().findByText('The clock is no longer paused.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).filter((c) => c === RESUME)).toHaveLength(1);
  });

  it('copes with a referral the API sent with little in it', async () => {
    const fetch = mockApi({'POST /api/referrals//clock/resume': () => json(running)});
    const {user} = renderWithApp(
      <ReferralWaitCard referral={{clock: {state: 'Paused', pauseReviewOn: '2026-12-01'}, canPauseClock: true, canResumeClock: true}} pathwayId={PATHWAY_ID} />,
      {user: staffUser},
    );

    expect(await screen.findByText('No pauses.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Pause Clock'}));
    expect(await dialog().findByText(/: day 0 of 0\.$/)).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('button', {name: 'Resume Clock'}));
    await user.click(await dialog().findByRole('button', {name: 'Resume Clock'}));
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await posted(fetch)).toEqual({expectedVersion: 0});
  });

  it('allows review dates from tomorrow to 90 days ahead, as UK dates', () => {
    // Counted from today's UK date: 18 November 2026 gives 19 November to 16 February.
    expect(reviewDateRange(new Date('2026-11-18T12:00:00Z'))).toEqual({min: '2026-11-19', max: '2027-02-16'});
  });
});

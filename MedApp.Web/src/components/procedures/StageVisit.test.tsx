import {screen} from '@testing-library/react';
import type {Appointment} from '../../api/appointments';
import type {ProcedureStage} from '../../api/procedures';
import {testAppointment, testRequested} from '../../test/calendar';
import {renderWithApp} from '../../test/render';
import {appointmentHref, appointmentWhen, finishesWithoutVisit, involvesVisit, StageVisit, StageVisitTokens, stageVisit, type StageVisitProps} from './StageVisit';

const stage: ProcedureStage = {id: 'stage-1', stageOrder: 1, name: 'Assessment', visitRequirement: 'Required', state: 'InProgress', isCurrent: true, canEdit: true};

// 23 November 2026 is in winter: UK time is UTC.
const booked = testAppointment({id: 'booked', date: '2026-11-23', start: '10:00', end: '10:30'});
const attended = testAppointment({id: 'attended', status: 'Attended', date: '2026-11-20'});
const missed = (id: string, date: string) => testAppointment({id, status: 'DidNotAttend', date});
const cancelled = testAppointment({id: 'cancelled', status: 'Cancelled'});
const requested = testRequested({stageId: 'stage-1'});
const otherStage = testAppointment({id: 'other', stageId: 'stage-2', status: 'Attended'});

describe('stageVisit', () => {
  it('is Attended once any was, whatever else there is', () => {
    expect(stageVisit('stage-1', [booked, attended, missed('m', '2026-11-19')])).toEqual({status: 'Attended', appointment: attended});
  });

  it('is the open one next: Booked, or Requested', () => {
    expect(stageVisit('stage-1', [missed('m', '2026-11-19'), booked])).toEqual({status: 'Booked', appointment: booked});
    expect(stageVisit('stage-1', [requested])).toEqual({status: 'Requested', appointment: requested});
  });

  it('is the latest missed one next, then none; cancelled ones and other stages never count', () => {
    const later = missed('later', '2026-11-19');
    expect(stageVisit('stage-1', [missed('earlier', '2026-11-16'), later, cancelled])).toEqual({status: 'DidNotAttend', appointment: later});
    expect(stageVisit('stage-1', [cancelled, otherStage])).toEqual({status: 'None'});
  });

  it('copes with appointments without a time', () => {
    const noTime = testAppointment({id: 'x', status: 'DidNotAttend', startUtc: null});
    expect(stageVisit('stage-1', [noTime, missed('m', '2026-11-19')]).appointment?.id).toBe('m');
    expect(stageVisit('stage-1', [missed('m', '2026-11-19'), noTime]).appointment?.id).toBe('m');
  });
});

describe('finishing without a visit', () => {
  it('applies to a stage with a visit and none attended, unless it is being finished again', () => {
    expect(involvesVisit(stage)).toBe(true);
    expect(involvesVisit({...stage, visitRequirement: 'Suggested'})).toBe(true);
    expect(involvesVisit({...stage, visitRequirement: 'None'})).toBe(false);
    expect(finishesWithoutVisit(stage, {status: 'Booked'})).toBe(true);
    expect(finishesWithoutVisit(stage, {status: 'Attended'})).toBe(false);
    expect(finishesWithoutVisit({...stage, visitRequirement: 'None'}, {status: 'None'})).toBe(false);
    // Unlocked by an admin and finished again: it doesn't ask again.
    expect(finishesWithoutVisit({...stage, state: 'Completed'}, {status: 'None'})).toBe(false);
  });
});

describe('appointmentWhen and appointmentHref', () => {
  it('says when and with whom, or that it is not booked yet', () => {
    expect(appointmentWhen(booked)).toBe('Mon 23 Nov 2026, 10:00 to 10:30 with test-user');
    expect(appointmentWhen({...booked, staffUsername: null})).toBe('Mon 23 Nov 2026, 10:00 to 10:30');
    expect(appointmentWhen(requested)).toBe('Not booked yet');
    expect(appointmentHref(booked)).toBe('/calendar?appointment=booked');
  });
});

describe('StageVisitTokens', () => {
  it('shows the requirement and where the visit stands, with when for a booked one', async () => {
    renderWithApp(<StageVisitTokens stage={stage} appointments={[booked]} />);

    expect(await screen.findByText('Visit required')).toBeInTheDocument();
    expect(screen.getByText('Booked')).toBeInTheDocument();
    expect(screen.getByText('Mon 23 Nov 2026, 10:00 to 10:30 with test-user')).toBeInTheDocument();
  });

  it('shows only the requirement before there is an appointment, and nothing for a stage without a visit', async () => {
    const {container} = renderWithApp(
      <>
        <StageVisitTokens stage={{...stage, visitRequirement: 'Suggested'}} appointments={[]} />
        <StageVisitTokens stage={{...stage, id: 'none', visitRequirement: 'None'}} appointments={[]} />
      </>,
    );

    expect(await screen.findByText('Visit suggested')).toBeInTheDocument();
    expect(container.textContent).toBe('Visit suggested');
  });
});

describe('StageVisit', () => {
  function renderVisit(appointments: Appointment[], props: Partial<StageVisitProps> = {}) {
    const handlers = {onBook: vi.fn(), onReschedule: vi.fn(), onOutcome: vi.fn()};
    return {...handlers, ...renderWithApp(<StageVisit stage={stage} appointments={appointments} isBusy={false} {...handlers} {...props} />)};
  }

  it('books a visit that requires an appointment, saying why it matters for a required one', async () => {
    const {user, onBook} = renderVisit([requested]);

    expect(await screen.findByText('Requires appointment')).toBeInTheDocument();
    expect(screen.getByText(/the stage can't be finished until then/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Book Visit'}));

    expect(onBook).toHaveBeenCalledWith(requested);
  });

  it('books a new one when there is none, for a suggested visit', async () => {
    const {user, onBook} = renderVisit([cancelled], {stage: {...stage, visitRequirement: 'Suggested'}});

    expect(await screen.findByText('Book it, if the patient needs one.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Book Visit'}));

    expect(onBook).toHaveBeenCalledWith(undefined);
  });

  it('reschedules a booked visit, before it starts, and links to it on the calendar', async () => {
    const {user, onReschedule} = renderVisit([booked]);

    expect(await screen.findByRole('link', {name: 'Mon 23 Nov 2026, 10:00 to 10:30 with test-user'})).toHaveAttribute(
      'href',
      '/calendar?appointment=booked',
    );
    expect(screen.queryByRole('button', {name: 'Attended'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Reschedule'}));

    expect(onReschedule).toHaveBeenCalledWith(booked);
  });

  it('records whether the patient came, once it has started', async () => {
    const started = {...booked, canRecordOutcome: true};
    const {user, onOutcome} = renderVisit([started]);

    await user.click(await screen.findByRole('button', {name: 'Attended'}));
    await user.click(screen.getByRole('button', {name: 'Did Not Attend'}));

    expect(onOutcome).toHaveBeenNthCalledWith(1, started, 'Attended');
    expect(onOutcome).toHaveBeenNthCalledWith(2, started, 'DidNotAttend');
  });

  it('offers no reschedule when the API allows none', async () => {
    renderVisit([{...booked, canReschedule: false}]);

    expect(await screen.findByText('Booked')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reschedule'})).not.toBeInTheDocument();
  });

  it('books another after a missed visit, and has nothing to do once attended', async () => {
    const {user, onBook} = renderVisit([missed('m', '2026-11-19')]);

    expect(await screen.findByText('Did not attend')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Book Another'}));
    expect(onBook).toHaveBeenCalledWith();

    renderVisit([attended]);
    expect(await screen.findByText('Attended')).toBeInTheDocument();
    expect(screen.queryAllByRole('button', {name: /Book|Reschedule/})).toHaveLength(1);
  });

  it('records a past visit while nothing is booked, but not once it is booked or attended, nor without a handler', async () => {
    const onRecordPast = vi.fn();
    const first = renderVisit([requested], {onRecordPast});
    await first.user.click(await screen.findByRole('button', {name: 'Record Past Visit'}));
    expect(onRecordPast).toHaveBeenCalledTimes(1);
    first.unmount();

    const missedOne = renderVisit([missed('m', '2026-11-19')], {onRecordPast});
    expect(await screen.findByRole('button', {name: 'Record Past Visit'})).toBeInTheDocument();
    missedOne.unmount();

    for (const appointments of [[booked], [attended]]) {
      const shown = renderVisit(appointments, {onRecordPast});
      expect(await screen.findByRole('group', {name: 'Visit'})).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Record Past Visit'})).not.toBeInTheDocument();
      shown.unmount();
    }

    renderVisit([]);
    expect(await screen.findByRole('button', {name: 'Book Visit'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Record Past Visit'})).not.toBeInTheDocument();
  });

  it('waits while something is being saved', async () => {
    renderVisit([requested], {isBusy: true});

    expect(await screen.findByRole('button', {name: 'Book Visit'})).toHaveAttribute('aria-disabled', 'true');
  });
});

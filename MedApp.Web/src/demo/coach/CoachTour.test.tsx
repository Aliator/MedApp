import {screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {renderWithApp} from '../../test/render';
import {asCurrentUser, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {PATIENTS} from '../ids';
import {demoNavigation} from '../session';
import {TOUR} from '../tour';
import {CoachTour} from './CoachTour';
import {coachPosition, resetCoachForTests, setCoachPosition} from './coachState';

const stepIndex = (key: string) => TOUR.findIndex((s) => s.key === key);

describe('CoachTour', () => {
  beforeEach(() => resetCoachForTests());
  afterEach(() => vi.restoreAllMocks());

  it('shows nothing while no tour is under way', () => {
    startLightDemo(STAFF);
    renderWithApp(<CoachTour />, {user: asCurrentUser(STAFF), path: '/'});
    expect(screen.queryByRole('dialog', {name: /Guided tour/})).not.toBeInTheDocument();
  });

  it('opens the step’s page and explains its first highlight', async () => {
    startLightDemo(STAFF);
    setCoachPosition({step: stepIndex('pin-patient'), mark: 0});
    const {router} = renderWithApp(<CoachTour />, {user: asCurrentUser(STAFF), path: '/'});

    expect(await screen.findByRole('dialog', {name: 'Guided tour: Pin a patient'})).toBeInTheDocument();
    expect(screen.getByText(/While it’s open, he shows in the side nav/)).toBeInTheDocument();
    expect(screen.getByText('1 of 3')).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe(`/patients/${PATIENTS.samuelOkafor}`));
  });

  it('moves on with Next and back with Back', async () => {
    const user = userEvent.setup();
    startLightDemo(STAFF);
    const step = stepIndex('pin-patient');
    setCoachPosition({step, mark: 0, opened: `${step}:0`});
    renderWithApp(<CoachTour />, {user: asCurrentUser(STAFF), path: `/patients/${PATIENTS.samuelOkafor}`});

    await user.click(await screen.findByRole('button', {name: 'Next'}));
    expect(await screen.findByText('2 of 3')).toBeInTheDocument();
    expect(coachPosition()).toMatchObject({step, mark: 1});
    await user.click(screen.getByRole('button', {name: 'Back'}));
    expect(await screen.findByText('1 of 3')).toBeInTheDocument();
  });

  it('switches account first when the step needs another, with a full load to its page', async () => {
    const assign = vi.spyOn(demoNavigation, 'assign').mockImplementation(() => {});
    startLightDemo(ADMIN);
    setCoachPosition({step: stepIndex('team-walls'), mark: 0});
    renderWithApp(<CoachTour />, {user: asCurrentUser(ADMIN), path: '/'});

    expect(await screen.findByText(`Switching to ${LAB.fullName}…`)).toBeInTheDocument();
    await waitFor(() => expect(assign).toHaveBeenCalledWith(expect.stringContaining(`/patients/${PATIENTS.tobiasLund}`)));
    expect(assign).toHaveBeenCalledTimes(1);
  });

  it('says when what it points at isn’t on the page', async () => {
    startLightDemo(ADMIN);
    const step = stepIndex('colours');
    setCoachPosition({step, mark: 1, opened: `${step}:0`});
    renderWithApp(<CoachTour />, {user: asCurrentUser(ADMIN), path: '/'});
    expect(await screen.findByText('It isn’t showing on this page right now.', {}, {timeout: 8000})).toBeInTheDocument();
  }, 10000);

  it('ends with End Tour', async () => {
    const user = userEvent.setup();
    startLightDemo(ADMIN);
    setCoachPosition({step: stepIndex('colours'), mark: 1, opened: `${stepIndex('colours')}:0`});
    renderWithApp(<CoachTour />, {user: asCurrentUser(ADMIN), path: '/'});
    await user.click(await screen.findByRole('button', {name: 'End Tour'}));
    expect(coachPosition()).toBeNull();
    await waitFor(() => expect(screen.queryByRole('dialog', {name: /Guided tour/})).not.toBeInTheDocument());
  });
});

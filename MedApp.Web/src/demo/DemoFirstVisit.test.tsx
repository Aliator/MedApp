import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN} from './accounts';
import {coachPosition, resetCoachForTests, startCoachTour} from './coach/coachState';
import {DemoFirstVisit} from './DemoFirstVisit';
import {markReadMeSeen} from './readMeSeen';

describe('DemoFirstVisit', () => {
  beforeEach(() => resetCoachForTests());

  it('opens the Read Me on the first visit', async () => {
    startLightDemo(ADMIN);
    const {router} = renderWithApp(<DemoFirstVisit />, {user: asCurrentUser(ADMIN), path: '/patients'});
    await waitFor(() => expect(router.state.location.pathname).toBe('/read-me'));
    expect(coachPosition()).toBeNull();
  });

  it('stays put once the Read Me has been seen', async () => {
    startLightDemo(ADMIN);
    markReadMeSeen();
    const {router} = renderWithApp(<DemoFirstVisit />, {user: asCurrentUser(ADMIN), path: '/patients'});
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(router.state.location.pathname).toBe('/patients');
  });

  it('shows the guided tour once it is started from the menu', async () => {
    startLightDemo(ADMIN);
    markReadMeSeen();
    renderWithApp(<DemoFirstVisit />, {user: asCurrentUser(ADMIN), path: '/patients'});
    startCoachTour();
    expect(await screen.findByRole('dialog', {name: 'Guided tour: Two pages side by side'})).toBeInTheDocument();
  });
});

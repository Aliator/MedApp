import {screen} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN} from './accounts';
import {resetCoachForTests, startCoachTour} from './coach/coachState';
import {DemoWalkthrough} from './DemoWalkthrough';

describe('DemoWalkthrough', () => {
  beforeEach(() => resetCoachForTests());

  it('starts nothing by itself, and stays on the page it is on', async () => {
    startLightDemo(ADMIN);
    const {router} = renderWithApp(<DemoWalkthrough />, {user: asCurrentUser(ADMIN), path: '/patients'});
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.queryByRole('dialog', {name: /Guided tour/})).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/patients');
  });

  it('shows the walkthrough once it is started from the side nav', async () => {
    startLightDemo(ADMIN);
    renderWithApp(<DemoWalkthrough />, {user: asCurrentUser(ADMIN), path: '/patients'});
    startCoachTour();
    expect(await screen.findByRole('dialog', {name: 'Guided tour: Two pages side by side'})).toBeInTheDocument();
  });
});

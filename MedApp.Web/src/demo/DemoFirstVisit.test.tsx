import {screen} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN} from './accounts';
import {coachPosition, resetCoachForTests} from './coach/coachState';
import {DemoFirstVisit} from './DemoFirstVisit';
import {hasSeenReadMe, markReadMeSeen} from './readMeSeen';

describe('DemoFirstVisit', () => {
  beforeEach(() => resetCoachForTests());

  it('starts the guided tour on the first visit', async () => {
    startLightDemo(ADMIN);
    renderWithApp(<DemoFirstVisit />, {user: asCurrentUser(ADMIN), path: '/patients'});
    expect(await screen.findByRole('dialog', {name: 'Guided tour: Two pages side by side'})).toBeInTheDocument();
    expect(coachPosition()).toMatchObject({step: 0, mark: 0});
    expect(hasSeenReadMe()).toBe(true);
  });

  it('starts nothing once it has been seen', async () => {
    startLightDemo(ADMIN);
    markReadMeSeen();
    const {router} = renderWithApp(<DemoFirstVisit />, {user: asCurrentUser(ADMIN), path: '/patients'});
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(coachPosition()).toBeNull();
    expect(router.state.location.pathname).toBe('/patients');
  });
});

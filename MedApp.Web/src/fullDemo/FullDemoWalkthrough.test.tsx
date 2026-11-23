import {screen, waitFor} from '@testing-library/react';
import {PATIENTS} from '../demo/ids';
import {TOUR} from '../demo/tour';
import {resetCoachForTests, setCoachPosition} from '../demo/coach/coachState';
import {fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {json, mockApi, pageOf, renderWithApp, userWithRoles} from '../test/render';
import {FullDemoWalkthrough} from './FullDemoWalkthrough';

const nadia = {...userWithRoles('n.thornbury', 'Clinician')};

describe('FullDemoWalkthrough', () => {
  beforeEach(() => {
    resetCoachForTests();
    resetFullDemoForTests();
  });
  afterEach(() => resetFullDemoForTests());

  it('shows nothing while no tour is under way', () => {
    renderWithApp(<FullDemoWalkthrough />, {user: nadia});
    expect(screen.queryByRole('dialog', {name: /Guided tour/})).not.toBeInTheDocument();
  });

  it('opens a step’s record by its ID in the full demo’s database', async () => {
    fullDemoReady();
    mockApi({'GET /api/patients/search': () => json(pageOf([{id: 'samuel-full'}]))});
    setCoachPosition({step: TOUR.findIndex((s) => s.key === 'pin-patient'), mark: 0});
    const {router} = renderWithApp(<FullDemoWalkthrough />, {user: nadia, path: '/'});

    expect(await screen.findByRole('dialog', {name: 'Guided tour: Pin a patient'})).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients/samuel-full'));
    expect(router.state.location.pathname).not.toContain(PATIENTS.samuelOkafor);
  });
});

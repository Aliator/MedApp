import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {startLightDemo} from '../test/lightDemo';
import {EYE_CLINIC_ADMIN, STAFF} from './accounts';
import {DemoLoginPage} from './DemoLoginPage';

describe('DemoLoginPage', () => {
  it('offers the five demo accounts, and signs in as the one chosen', async () => {
    const {state} = startLightDemo(null);
    const {user, router} = renderWithApp(<DemoLoginPage />, {path: '/login'});

    expect(await screen.findByText('Synthetic data, kept only in this browser')).toBeInTheDocument();
    expect(screen.getAllByRole('button', {name: /^Sign in as /})).toHaveLength(5);
    expect(screen.getByRole('button', {name: 'Sign in as the admin'})).toBeInTheDocument();
    expect(screen.getByText('Clinic admin (Eye clinic). Clinician. Consultant ophthalmologist.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: `Sign in as ${EYE_CLINIC_ADMIN.fullName}`})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: `Sign in as ${STAFF.fullName}`}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(state.user).toBe(STAFF.username);
  });
});

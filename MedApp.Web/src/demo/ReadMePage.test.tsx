import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN, STAFF} from './accounts';
import {PATHWAYS, REFERRALS} from './ids';
import {ReadMePage} from './ReadMePage';
import {hasSeenReadMe} from './readMeSeen';
import {demoNavigation} from './session';
import {db} from './store';

describe('ReadMePage', () => {
  it('says what the light version is, the two accounts, the tour and what it leaves out, and counts as seen', async () => {
    startLightDemo(ADMIN);
    renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    expect(await screen.findByRole('heading', {name: 'Read Me', level: 1})).toBeInTheDocument();
    // The demo's banner sits at the top of the Read Me (not above every page).
    expect(screen.getByText('Light demo: synthetic data, kept only in this browser')).toBeInTheDocument();
    expect(screen.getByText(/runs entirely in your browser/)).toBeInTheDocument();
    expect(screen.getByText('Clinical lead (admin): you, now')).toBeInTheDocument();
    expect(screen.getByText(`${STAFF.fullName} (${STAFF.username})`)).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: /Triage a referral/})).toBeInTheDocument();
    expect(screen.getByText('Real-time notifications')).toBeInTheDocument();
    expect(hasSeenReadMe()).toBe(true);
  });

  it('opens a step’s page as the account already signed in', async () => {
    startLightDemo(ADMIN);
    const {user, router} = renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the list of pathways'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
  });

  it('switches account first when the step needs the other one', async () => {
    const {state} = startLightDemo(ADMIN);
    const assign = vi.spyOn(demoNavigation, 'assign').mockImplementation(() => {});
    const {user} = renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the referral to triage it'}));

    await waitFor(() => expect(assign).toHaveBeenCalledWith(`/referrals/${REFERRALS.dermatologyToTriage}`));
    expect(state.user).toBe(STAFF.username);
  });

  it('explains instead of opening a record that’s gone, and says when a step was done already', async () => {
    startLightDemo(STAFF);
    db().pathways = db().pathways.filter((p) => p.id !== PATHWAYS.histologyToFinish);
    db().referrals.find((r) => r.id === REFERRALS.dermatologyToTriage)!.status = 'Accepted';
    const {user, router} = renderWithApp(<ReadMePage />, {user: asCurrentUser(STAFF), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the pathway at its current stage'}));
    expect(await screen.findByText(/isn’t in your copy of the demo any more/)).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/read-me');

    await user.click(screen.getByRole('button', {name: 'Open the referral to triage it'}));
    expect(await screen.findByText(/You've triaged this referral already/)).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe(`/referrals/${REFERRALS.dermatologyToTriage}`));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {openDialog, renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN, STAFF} from './accounts';
import {DemoBanner} from './DemoBanner';
import {markReadMeSeen} from './readMeSeen';
import {demoNavigation} from './session';
import {db} from './store';

describe('DemoBanner', () => {
  it('says the data is synthetic and kept in this browser, and who is signed in', async () => {
    startLightDemo(ADMIN);
    markReadMeSeen();
    renderWithApp(<DemoBanner />, {user: asCurrentUser(ADMIN), path: '/patients'});

    expect(await screen.findByText('Light demo: synthetic data, kept only in this browser')).toBeInTheDocument();
    expect(screen.getByText(/Don’t enter real patient details\. You’re signed in as Clinical lead \(admin\)/)).toBeInTheDocument();
    // Only Reset: the Read Me it sits on has the account switch, and is the Read Me.
    expect(screen.queryByRole('link', {name: 'Read Me'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: `Switch to ${STAFF.fullName}`})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Reset'})).toBeInTheDocument();
  });

  it('resets the demo once confirmed', async () => {
    startLightDemo(STAFF);
    markReadMeSeen();
    db().pathways = [];
    const assign = vi.spyOn(demoNavigation, 'assign').mockImplementation(() => {});
    const {user} = renderWithApp(<DemoBanner />, {user: asCurrentUser(STAFF), path: '/pathways'});

    await user.click(await screen.findByRole('button', {name: 'Reset'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset'}));

    await waitFor(() => expect(assign).toHaveBeenCalledWith('/'));
    expect(db().pathways.length).toBeGreaterThan(0);
  });
});

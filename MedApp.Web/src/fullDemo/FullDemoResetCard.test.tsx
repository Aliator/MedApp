import {screen, waitFor, within} from '@testing-library/react';
import {fakeBackend, fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {openDialog, renderWithApp, staffUser} from '../test/render';
import {FullDemoResetCard} from './FullDemoResetCard';
import {fullDemoNavigation} from './session';

describe('FullDemoResetCard', () => {
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => resetFullDemoForTests());

  it('says when the data was published, and resets it after asking', async () => {
    fullDemoReady();
    const backend = fakeBackend();
    const assign = vi.spyOn(fullDemoNavigation, 'assign').mockImplementation(() => {});
    const {user} = renderWithApp(<FullDemoResetCard />, {user: staffUser, path: '/account'});

    expect(await screen.findByText(/own copy of the demo data \(published 2026-11-23\)/)).toBeInTheDocument();
    await user.click(await screen.findByRole('button', {name: 'Reset the Demo'}));
    const dialog = openDialog()!;
    expect(within(dialog).getByText(/The demo’s data is downloaded again/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', {name: 'Reset the Demo'}));

    await waitFor(() => expect(assign).toHaveBeenCalledWith('/'));
    expect(backend.forgetSavedDatabase).toHaveBeenCalled();
  });

  it('can be cancelled', async () => {
    fullDemoReady();
    const backend = fakeBackend();
    const {user} = renderWithApp(<FullDemoResetCard />, {user: staffUser, path: '/account'});
    await user.click(await screen.findByRole('button', {name: 'Reset the Demo'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();
    expect(backend.forgetSavedDatabase).not.toHaveBeenCalled();
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, openDialog, renderWithApp, staffUser} from '../../test/render';
import {KeyboardShortcuts} from './KeyboardShortcuts';

describe('KeyboardShortcuts', () => {
  beforeEach(() => localStorage.setItem('medapp.accessibility', JSON.stringify({shortcuts: true})));

  it('lists the shortcuts on ?, with the admin pages only for admins', async () => {
    const {user, unmount} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});

    await user.keyboard('?');
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByText('Go to Patients')).toBeInTheDocument();
    expect(dialog.queryByText('Go to Users')).not.toBeInTheDocument();
    unmount();

    const admin = renderWithApp(<KeyboardShortcuts />, {user: adminUser});
    await admin.user.keyboard('?');
    expect(within(await waitFor(() => openDialog()!)).getByText('Go to Users')).toBeInTheDocument();
  });

  it('goes to a page on G then its letter', async () => {
    const {user, router} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});

    await user.keyboard('gp');
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    await user.keyboard('gr');
    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures'));
  });

  it('leaves keys typed in a field alone', async () => {
    const {user, router} = renderWithApp(
      <>
        <input aria-label="Notes" />
        <KeyboardShortcuts />
      </>,
      {user: staffUser},
    );

    await user.type(await screen.findByRole('textbox', {name: 'Notes'}), 'gp?');
    expect(router.state.location.pathname).toBe('/');
    expect(openDialog()).toBeNull();
  });

  it('does nothing while off, as they start', async () => {
    localStorage.removeItem('medapp.accessibility');
    const {user, router} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});

    await user.keyboard('gp?');
    expect(router.state.location.pathname).toBe('/');
    expect(openDialog()).toBeNull();
  });
});

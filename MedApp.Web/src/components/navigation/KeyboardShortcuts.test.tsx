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
    expect(dialog.getByText('Save the form')).toBeInTheDocument();
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
    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
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

  it("closes the list of shortcuts from its close button", async () => {
    const {user} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});

    await user.keyboard('?');
    const dialog = await waitFor(() => openDialog()!);
    await user.click(within(dialog).getByRole('button', {name: /close/i}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("goes to the list's search on /, when the page has one", async () => {
    const {user, unmount} = renderWithApp(
      <>
        <main>
          <input role="combobox" aria-label="Search patients" aria-expanded="false" aria-controls="none" />
        </main>
        <KeyboardShortcuts />
      </>,
      {user: staffUser},
    );

    await screen.findByRole('combobox', {name: 'Search patients'});
    await user.keyboard('/');
    expect(screen.getByRole('combobox', {name: 'Search patients'})).toHaveFocus();
    unmount();

    // No search on the page: nothing happens.
    const other = renderWithApp(<KeyboardShortcuts />, {user: staffUser});
    await other.user.keyboard('/');
    expect(document.body).toHaveFocus();
  });

  it('leaves admin pages, letters without G, a late letter, and keys with a modifier alone', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const {user, router} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});

    await user.keyboard('gu');
    await user.keyboard('p');
    await user.keyboard('g');
    await vi.advanceTimersByTimeAsync(2000);
    await user.keyboard('p');
    await user.keyboard('{Control>}g{/Control}p');
    await user.keyboard('{Control>}?{/Control}');

    expect(router.state.location.pathname).toBe('/');
    expect(openDialog()).toBeNull();
  });

  it('lets a key something else has already handled be', async () => {
    const {user, router} = renderWithApp(<KeyboardShortcuts />, {user: staffUser});
    // Working (G then P goes to Patients)...
    await user.keyboard('gp');
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));

    // ...but a G something else handled first doesn't count.
    const handled = new KeyboardEvent('keydown', {key: 'g', bubbles: true, cancelable: true});
    handled.preventDefault();
    document.body.dispatchEvent(handled);
    await user.keyboard('r');

    expect(router.state.location.pathname).toBe('/patients');
  });
});

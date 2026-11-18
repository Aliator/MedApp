import {screen, waitFor} from '@testing-library/react';
import {api} from '../../api/client';
import {useCan, whoamiQuery} from '../../auth/auth';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {permissionRefusedText, PermissionMissingWatcher, whenNoDialogIsOpen} from './PermissionMissingWatcher';

const DETAIL = "Your roles don't allow this. It needs the permission to: Book appointments.";
const refused = () => json({title: 'Permission needed', detail: DETAIL, code: 'permission_missing', permission: 'BookAppointments'}, 403);

/** A request the API refuses for a missing permission, and whoami as it is now (BookAppointments taken away). */
function mockRefusal() {
  return mockApi({
    'POST /api/appointments': refused,
    'GET /api/auth/whoami': () =>
      json({isAuthenticated: true, name: staffUser.username, roles: staffUser.roles, permissions: staffUser.permissions.filter((p) => p !== 'BookAppointments')}),
  });
}

/** Shows whether the signed-in person can book, as a page's button would (it reads whoami, as the app always does). */
function CanBook() {
  return <p>{useCan('BookAppointments') ? 'Can book' : 'Cannot book'}</p>;
}

/** Renders the watcher, and waits for the router to have mounted it (it renders its routes a moment later). */
async function renderWatcher() {
  const result = renderWithApp(
    <>
      <PermissionMissingWatcher />
      <CanBook />
    </>,
    {user: staffUser},
  );
  await screen.findByText('Can book');
  return result;
}

describe('PermissionMissingWatcher', () => {
  it('says what is missing in a toast, and reads whoami again so what it allowed is hidden', async () => {
    const fetch = mockRefusal();
    const {queryClient} = await renderWatcher();

    await api.POST('/api/appointments', {body: {} as never});

    expect((await screen.findAllByText(DETAIL)).length).toBeGreaterThan(0);
    expect(await screen.findByText('Cannot book')).toBeInTheDocument();
    expect(queryClient.getQueryData(whoamiQuery.queryKey)?.permissions).not.toContain('BookAppointments');
    expect(fetch.mock.calls.filter(([r]) => new URL((r as Request).url).pathname === '/api/auth/whoami')).toHaveLength(1);
  });

  it('waits for an open dialog to close before reading whoami, so what was typed in it stays', async () => {
    const fetch = mockRefusal();
    const dialog = document.createElement('dialog');
    dialog.setAttribute('open', '');
    document.body.append(dialog);
    const {queryClient} = await renderWatcher();
    const whoamiCalls = () => fetch.mock.calls.filter(([r]) => new URL((r as Request).url).pathname === '/api/auth/whoami').length;

    await api.POST('/api/appointments', {body: {} as never});
    expect((await screen.findAllByText(DETAIL)).length).toBeGreaterThan(0);
    // Still as it was while the dialog is open.
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(whoamiCalls()).toBe(0);
    expect(queryClient.getQueryData(whoamiQuery.queryKey)?.permissions).toContain('BookAppointments');
    expect(screen.getByText('Can book')).toBeInTheDocument();

    dialog.removeAttribute('open');
    await waitFor(() => expect(whoamiCalls()).toBe(1));
    expect(await screen.findByText('Cannot book')).toBeInTheDocument();
    dialog.remove();
  });

  it('stops listening when it goes', async () => {
    const fetch = mockRefusal();
    const {unmount} = await renderWatcher();
    unmount();

    await api.POST('/api/appointments', {body: {} as never});

    expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname === '/api/auth/whoami')).toBe(false);
  });
});

describe('permissionRefusedText', () => {
  it("uses the API's words, or failing those the app's own for a permission it knows", () => {
    expect(permissionRefusedText('ViewInsights', 'The API says.')).toBe('The API says.');
    expect(permissionRefusedText('ViewInsights', undefined)).toBe("Your roles don't allow this. It needs the permission to: See Insights.");
    expect(permissionRefusedText('Newer', undefined)).toBe("Your roles don't allow this.");
  });
});

describe('whenNoDialogIsOpen', () => {
  it('runs at once with no dialog open', () => {
    document.querySelectorAll('dialog').forEach((dialog) => dialog.remove());
    const action = vi.fn();
    whenNoDialogIsOpen(action);
    expect(action).toHaveBeenCalledOnce();
  });

  it('can be cancelled while it waits', async () => {
    const dialog = document.createElement('dialog');
    dialog.setAttribute('open', '');
    document.body.append(dialog);
    const action = vi.fn();

    const cancel = whenNoDialogIsOpen(action);
    cancel();
    dialog.remove();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(action).not.toHaveBeenCalled();
  });
});

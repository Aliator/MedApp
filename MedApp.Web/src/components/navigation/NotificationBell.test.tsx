import {screen, waitFor, within} from '@testing-library/react';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {NotificationBell} from './NotificationBell';

const procedureId = '0000000c-0000-0000-0000-000000000001';
const unread = {
  id: '0000000c-0000-0000-0000-000000000002',
  kind: 'ProcedureAssigned',
  title: 'Test procedure',
  message: 'test.admin assigned you to this procedure.',
  procedureId,
  createdAtUtc: new Date().toISOString(),
  isRead: false,
};
const read = {
  ...unread,
  id: '0000000c-0000-0000-0000-000000000003',
  title: 'Older procedure',
  isRead: true,
};

/** The bell's popover, once open. */
const panel = () => waitFor(() => within(screen.getByRole('dialog', {name: 'Notifications'})));

describe('NotificationBell', () => {
  it('shows how many are unread, and lists the newest with the unread marked', async () => {
    mockApi({
      'GET /api/notifications': () => json({items: [unread, read], unreadCount: 1}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    const bell = await screen.findByRole('button', {
      name: 'Notifications, 1 unread',
    });
    await user.click(bell);
    const list = await panel();

    expect(list.getByText('Test procedure')).toBeInTheDocument();
    expect(list.getAllByText('test.admin assigned you to this procedure.')).toHaveLength(2);
    expect(list.getAllByText('Just now').length).toBeGreaterThan(0);
    expect(list.getAllByText('New')).toHaveLength(1);
    expect(list.getByRole('link', {name: /Test procedure/})).toHaveAttribute('href', `/procedures/${procedureId}`);
  });

  it('opens a procedure from its notification, marking it read', async () => {
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [unread], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {
      user: staffUser,
    });

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    await user.click((await panel()).getByRole('link', {name: /Test procedure/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${unread.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/procedures/${procedureId}`));
  });

  it('marks them all read', async () => {
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [unread], unreadCount: 1}),
      'POST /api/notifications/read': () => new Response(null, {status: 204}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    await user.click((await panel()).getByRole('button', {name: 'Mark all read'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/notifications/read'));
  });

  it('says so when there are none', async () => {
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Notifications'}));

    expect((await panel()).getByText(/Nothing yet/)).toBeInTheDocument();
  });
});

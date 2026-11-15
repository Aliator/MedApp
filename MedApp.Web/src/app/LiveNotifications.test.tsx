import {HubConnectionBuilder} from '@microsoft/signalr';
import {act, screen, waitFor} from '@testing-library/react';
import {NotificationBell} from '../components/navigation/NotificationBell';
import {json, mockApi, renderWithApp, staffUser} from '../test/render';
import {liveHub} from '../test/signalr';
import {LiveNotifications, NOTIFICATION_MESSAGE, NOTIFICATIONS_HUB} from './LiveNotifications';

const notification = {
  id: '0000000c-0000-0000-0000-000000000010',
  kind: 'ProcedureAssigned',
  title: 'Test procedure',
  message: 'test.admin assigned you to this procedure.',
  procedureId: '0000000c-0000-0000-0000-000000000011',
  createdAtUtc: new Date().toISOString(),
  isRead: false,
};

describe('LiveNotifications', () => {
  it('connects to the notifications hub, and closes the connection when it goes', async () => {
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    const {unmount} = renderWithApp(<LiveNotifications />, {
      user: staffUser,
    });

    await waitFor(() => expect(liveHub.connections).toHaveLength(1));
    expect(liveHub.connections[0].url).toBe(NOTIFICATIONS_HUB);
    unmount();
    await waitFor(() => expect(liveHub.connections).toHaveLength(0));
  });

  it('shows a new notification at once: the bell counts it, with no reload', async () => {
    let stored: (typeof notification)[] = [];
    mockApi({
      'GET /api/notifications': () => json({items: stored, unreadCount: stored.length}),
    });
    renderWithApp(
      <>
        <LiveNotifications />
        <NotificationBell />
      </>,
      {user: staffUser},
    );
    await screen.findByRole('button', {name: 'Notifications'});
    await waitFor(() => expect(liveHub.connections).toHaveLength(1));

    stored = [notification];
    act(() => liveHub.push(NOTIFICATION_MESSAGE, notification));

    expect(await screen.findByRole('button', {name: 'Notifications, 1 unread'})).toBeInTheDocument();
  });

  it('tells screen readers, when announcements are on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    renderWithApp(<LiveNotifications />, {user: staffUser});
    await waitFor(() => expect(liveHub.connections).toHaveLength(1));

    act(() => liveHub.push(NOTIFICATION_MESSAGE, notification));

    await waitFor(() =>
      expect(screen.getByTestId('announcer')).toHaveTextContent(
        'New notification: Test procedure. test.admin assigned you to this procedure.',
      ),
    );
  });

  it('refreshes the list for a notification about no procedure, leaving procedure lists alone', async () => {
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    const {queryClient} = renderWithApp(<LiveNotifications />, {user: staffUser});
    await waitFor(() => expect(liveHub.connections).toHaveLength(1));
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => liveHub.push(NOTIFICATION_MESSAGE, {...notification, procedureId: null}));

    expect(invalidate).toHaveBeenCalledWith({queryKey: ['notifications']});
    expect(invalidate).not.toHaveBeenCalledWith({queryKey: ['procedures']});
  });

  it('refreshes the list after reconnecting, in case one came while it was away', async () => {
    let reconnected: (() => void) | undefined;
    const build = HubConnectionBuilder.prototype.build;
    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockImplementation(function (this: HubConnectionBuilder) {
      const connection = build.call(this);
      connection.onreconnected = (handler: () => void) => {
        reconnected = handler;
      };
      return connection;
    });
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    const {queryClient} = renderWithApp(<LiveNotifications />, {user: staffUser});
    await waitFor(() => expect(reconnected).toBeDefined());
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => reconnected!());

    expect(invalidate).toHaveBeenCalledWith({queryKey: ['notifications']});
  });

  it('carries on without live notifications when the hub cannot be reached', async () => {
    const build = HubConnectionBuilder.prototype.build;
    const start = vi.fn(() => Promise.reject(new Error('Hub unreachable')));
    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockImplementation(function (this: HubConnectionBuilder) {
      const connection = build.call(this);
      connection.start = start;
      return connection;
    });
    mockApi({
      'GET /api/notifications': () => json({items: [], unreadCount: 0}),
    });
    const {unmount} = renderWithApp(
      <>
        <LiveNotifications />
        <NotificationBell />
      </>,
      {user: staffUser},
    );

    expect(await screen.findByRole('button', {name: 'Notifications'})).toBeInTheDocument();
    expect(start).toHaveBeenCalled();
    expect(liveHub.connections).toHaveLength(0);
    unmount();
  });
});

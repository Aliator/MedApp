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

  it("opens an appointment's notification on the calendar, marking it read", async () => {
    const appointmentId = '0000000c-0000-0000-0000-000000000009';
    const booked = {...unread, id: '0000000c-0000-0000-0000-000000000004', kind: 'AppointmentBooked', title: 'Knee review', message: 'test.admin booked an appointment with you for Monday 23 November at 10:00.', appointmentId};
    const cancelled = {...read, id: '0000000c-0000-0000-0000-000000000005', kind: 'AppointmentCancelled', title: 'Appointment', procedureId: null, appointmentId};
    const recordedId = '0000000c-0000-0000-0000-000000000010';
    const recorded = {...read, id: '0000000c-0000-0000-0000-000000000006', kind: 'AppointmentRecorded', title: 'Appointment recorded', message: 'test.admin recorded an appointment with you on Monday 16 November at 10:00, after the event.', procedureId: null, appointmentId: recordedId};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [booked, cancelled, recorded], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Appointmenttest/})).toHaveAttribute('href', `/calendar?appointment=${appointmentId}`);
    // One recorded after the event opens on the calendar too.
    expect(list.getByRole('link', {name: /^Appointment recorded/})).toHaveAttribute('href', `/calendar?appointment=${recordedId}`);
    // Each kind has its icon.
    expect(list.getAllByRole('listitem').every((item) => item.querySelector('svg'))).toBe(true);
    await user.click(list.getByRole('link', {name: /Knee review/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${booked.id}/read`));
    await waitFor(() => expect(router.state.location.href).toBe(`/calendar?appointment=${appointmentId}`));
  });

  it("opens a meeting's notification on its day of the calendar, marking it read, with the meeting icon", async () => {
    const meetingBase = {...unread, procedureId: null, appointmentId: null, calendarDate: '2026-11-24'};
    const invited = {...meetingBase, id: '0000000c-0000-0000-0000-000000000011', kind: 'MeetingInvited', title: 'Meeting invitation', message: 'test.admin invited you to Team huddle, Tuesday 24 November, 10:00 to 11:00.'};
    const moved = {...meetingBase, id: '0000000c-0000-0000-0000-000000000012', kind: 'MeetingChanged', title: 'Meeting moved', isRead: true, calendarDate: '2026-11-25'};
    const cancelled = {...meetingBase, id: '0000000c-0000-0000-0000-000000000013', kind: 'MeetingCancelled', title: 'Meeting cancelled', isRead: true, calendarDate: '2026-11-26'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [invited, moved, cancelled], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Meeting moved/})).toHaveAttribute('href', '/calendar?date=2026-11-25&view=day');
    expect(list.getByRole('link', {name: /^Meeting cancelled/})).toHaveAttribute('href', '/calendar?date=2026-11-26&view=day');
    // The three kinds share the meeting icon.
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons.every((icon) => icon !== undefined && icon === icons[0])).toBe(true);
    await user.click(list.getByRole('link', {name: /^Meeting invitation/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${invited.id}/read`));
    await waitFor(() => expect(router.state.location.href).toBe('/calendar?date=2026-11-24&view=day'));
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

  it('counts up to 99, then says 99+', async () => {
    mockApi({
      'GET /api/notifications': () => json({items: [unread], unreadCount: 120}),
    });
    renderWithApp(<NotificationBell />, {user: staffUser});

    expect(await screen.findByRole('button', {name: 'Notifications, 120 unread'})).toBeInTheDocument();
    expect(screen.getByText('99+')).toBeInTheDocument();
  });

  it('shows a notification about no procedure without a link, and clicking it changes nothing', async () => {
    const general = {...unread, id: '0000000c-0000-0000-0000-000000000004', title: 'General notice', procedureId: null, createdAtUtc: null};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [general], unreadCount: 1}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.queryByRole('link')).not.toBeInTheDocument();
    expect(list.queryByText('Just now')).not.toBeInTheDocument();

    await user.click(list.getByText('General notice'));
    expect(screen.getByRole('dialog', {name: 'Notifications'})).toBeInTheDocument();
    expect(calls(fetch).filter((call) => call.startsWith('POST'))).toEqual([]);
  });

  it('marks read only the unread notifications about the procedure opened', async () => {
    const otherProcedure = {...unread, id: '0000000c-0000-0000-0000-000000000005', title: 'Other procedure', procedureId: '0000000c-0000-0000-0000-000000000006'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [unread, read, otherProcedure], unreadCount: 2}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    await user.click((await panel()).getByRole('link', {name: /Test procedure/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${unread.id}/read`));
    expect(calls(fetch).filter((call) => call.startsWith('POST'))).toEqual([`POST /api/notifications/${unread.id}/read`]);
  });
});

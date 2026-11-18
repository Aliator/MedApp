import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {NotificationBell} from './NotificationBell';

const pathwayId = '0000000c-0000-0000-0000-000000000001';
const unread = {
  id: '0000000c-0000-0000-0000-000000000002',
  kind: 'PathwayAssigned',
  title: 'Test pathway',
  message: 'test.admin assigned you to this pathway.',
  pathwayId,
  createdAtUtc: new Date().toISOString(),
  isRead: false,
};
const read = {
  ...unread,
  id: '0000000c-0000-0000-0000-000000000003',
  title: 'Older pathway',
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

    expect(list.getByText('Test pathway')).toBeInTheDocument();
    expect(list.getAllByText('test.admin assigned you to this pathway.')).toHaveLength(2);
    expect(list.getAllByText('Just now').length).toBeGreaterThan(0);
    expect(list.getAllByText('New')).toHaveLength(1);
    expect(list.getByRole('link', {name: /Test pathway/})).toHaveAttribute('href', `/pathways/${pathwayId}`);
  });

  it("puts the count over the bell's corner, so the bell keeps its size, with the count said only by the bell's name", async () => {
    mockApi({'GET /api/notifications': () => json({items: [unread], unreadCount: 120})});
    renderWithApp(<NotificationBell />, {user: staffUser});

    const bell = await screen.findByRole('button', {name: 'Notifications, 120 unread'});
    const count = await screen.findByText('99+');
    expect(count.closest('[aria-hidden="true"]')).not.toBeNull();
    // Placed over the bell (taken out of the row), and letting a click through to it.
    const badge = count.closest<HTMLElement>('[style*="absolute"]')!;
    expect(badge.style.position).toBe('absolute');
    expect(badge.style.pointerEvents).toBe('none');
    expect(badge.parentElement).toContainElement(bell);
    expect(badge.parentElement!.style.position).toBe('relative');
  });

  it('shows no count when nothing is unread', async () => {
    mockApi({'GET /api/notifications': () => json({items: [read], unreadCount: 0})});
    renderWithApp(<NotificationBell />, {user: staffUser});

    expect(await screen.findByRole('button', {name: 'Notifications'})).toBeInTheDocument();
    expect(document.querySelector('[style*="absolute"]')).toBeNull();
  });

  it("opens an appointment's notification on the calendar, marking it read", async () => {
    const appointmentId = '0000000c-0000-0000-0000-000000000009';
    const booked = {...unread, id: '0000000c-0000-0000-0000-000000000004', kind: 'AppointmentBooked', title: 'Knee review', message: 'test.admin booked an appointment with you for Monday 23 November at 10:00.', appointmentId};
    const cancelled = {...read, id: '0000000c-0000-0000-0000-000000000005', kind: 'AppointmentCancelled', title: 'Appointment', pathwayId: null, appointmentId};
    const recordedId = '0000000c-0000-0000-0000-000000000010';
    const recorded = {...read, id: '0000000c-0000-0000-0000-000000000006', kind: 'AppointmentRecorded', title: 'Appointment recorded', message: 'test.admin recorded an appointment with you on Monday 16 November at 10:00, after the event.', pathwayId: null, appointmentId: recordedId};
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

  it("opens a closed day's notification on that day of the calendar, with the closed day icon", async () => {
    const closed = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000014',
      kind: 'DayClosed',
      title: 'Day closed',
      message: "test.admin closed Friday 27 November (Staff training day). You have 2 appointments and a meeting then. Move or cancel what's still to come, and tell any patients.",
      pathwayId: null,
      appointmentId: null,
      calendarDate: '2026-11-27',
    };
    const meeting = {...closed, id: '0000000c-0000-0000-0000-000000000015', kind: 'MeetingInvited', title: 'Meeting invitation', isRead: true};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [closed, meeting], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    const link = list.getByRole('link', {name: /^Day closed/});
    expect(link).toHaveAttribute('href', '/calendar?date=2026-11-27&view=day');
    // Its own icon, not the meeting's.
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons[0]).toBeDefined();
    expect(icons[0]).not.toBe(icons[1]);
    await user.click(link);

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${closed.id}/read`));
    await waitFor(() => expect(router.state.location.href).toBe('/calendar?date=2026-11-27&view=day'));
  });

  it("opens a withdrawn stage visit's notification on its pathway, with the entered in error icon", async () => {
    const withdrawn = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000016',
      kind: 'StageVisitWithdrawn',
      title: 'Stage visit withdrawn',
      message: "test.user marked the visit on Monday 16 November at 10:00 for the 'Bloods' stage entered in error.",
      appointmentId: null,
    };
    const assigned = {...read, id: '0000000c-0000-0000-0000-000000000017', title: 'Another pathway'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [withdrawn, assigned], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    const link = list.getByRole('link', {name: /^Stage visit withdrawn/});
    expect(link).toHaveAttribute('href', `/pathways/${pathwayId}`);
    // Its own icon, not the assignment's.
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons[0]).toBeDefined();
    expect(icons[0]).not.toBe(icons[1]);
    await user.click(link);

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${withdrawn.id}/read`));
    await waitFor(() => expect(router.state.location.href).toBe(`/pathways/${pathwayId}`));
  });

  it("opens a meeting's notification on its day of the calendar, marking it read, with the meeting icon", async () => {
    const meetingBase = {...unread, pathwayId: null, appointmentId: null, calendarDate: '2026-11-24'};
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

  it("opens a declined meeting itself, on its day, with an icon of its own", async () => {
    const meetingBase = {...unread, pathwayId: null, appointmentId: null, calendarDate: '2026-11-24'};
    const invited = {...meetingBase, id: '0000000c-0000-0000-0000-000000000021', kind: 'MeetingInvited', title: 'Meeting invitation', isRead: true};
    const declined = {...meetingBase, id: '0000000c-0000-0000-0000-000000000022', kind: 'MeetingDeclined', title: 'Meeting declined', meetingId: '0000000d-0000-0000-0000-000000000001', message: 'k.ashford declined Team huddle, Tuesday 24 November, 10:00 to 11:00.'};
    mockApi({'GET /api/notifications': () => json({items: [invited, declined], unreadCount: 1})});
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Meeting declined/})).toHaveAttribute('href', '/calendar?date=2026-11-24&view=day&meeting=0000000d-0000-0000-0000-000000000001');
    const [inviteIcon, declineIcon] = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(declineIcon).toBeDefined();
    expect(declineIcon).not.toBe(inviteIcon);
  });

  it('opens a pathway from its notification, marking it read', async () => {
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [unread], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {
      user: staffUser,
    });

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    await user.click((await panel()).getByRole('link', {name: /Test pathway/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${unread.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${pathwayId}`));
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

  it('shows a notification about no pathway without a link, and clicking it changes nothing', async () => {
    const general = {...unread, id: '0000000c-0000-0000-0000-000000000004', title: 'General notice', pathwayId: null, createdAtUtc: null};
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

  it("opens an admin's note deletion request on its pathway's page, and links the requester's outcome to the pathway", async () => {
    const requestId = '0000000c-0000-0000-0000-000000000021';
    const requested = {...unread, id: '0000000c-0000-0000-0000-000000000022', kind: 'NoteDeletionRequested', title: 'Note deletion requested', message: 's.nurse asked to delete a note on a pathway.', noteDeletionRequestId: requestId};
    const kept = {...read, id: '0000000c-0000-0000-0000-000000000023', kind: 'NoteDeletionDeclined', title: 'Note kept', message: 'test.admin kept the note you asked to delete.'};
    const authors = {...read, id: '0000000c-0000-0000-0000-000000000024', kind: 'AuthorsNoteDeleted', title: 'Your note was deleted', message: 'test.admin deleted a note you wrote on a pathway.'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [requested, kept, authors], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    const href = `/pathways/${pathwayId}?deletionRequest=${requestId}`;
    expect(list.getByRole('link', {name: /^Note deletion requested/})).toHaveAttribute('href', href);
    expect(list.getByRole('link', {name: /^Note kept/})).toHaveAttribute('href', `/pathways/${pathwayId}`);
    // An author told their note was deleted goes to the pathway too.
    expect(list.getByRole('link', {name: /^Your note was deleted/})).toHaveAttribute('href', `/pathways/${pathwayId}`);
    expect(list.getAllByRole('listitem').every((item) => item.querySelector('svg'))).toBe(true);

    await user.click(list.getByRole('link', {name: /^Note deletion requested/}));
    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${requested.id}/read`));
    await waitFor(() => expect(router.state.location.href).toBe(href));
  });

  it("opens a failing scheduled job's notification on Scheduled Jobs, with the scheduled jobs icon", async () => {
    const failing = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000031',
      kind: 'ScheduledJobFailing',
      title: 'Scheduled job failing',
      message: 'Operational data purge has failed 3 times in a row.',
      pathwayId: null,
      appointmentId: null,
    };
    const assigned = {...read, id: '0000000c-0000-0000-0000-000000000032', title: 'Another pathway'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [failing, assigned], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    const link = list.getByRole('link', {name: /^Scheduled job failing/});
    expect(link).toHaveAttribute('href', '/admin/jobs');
    // Its own icon, not the assignment's.
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons[0]).toBeDefined();
    expect(icons[0]).not.toBe(icons[1]);
    await user.click(link);

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${failing.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/jobs'));
  });

  it("opens a service alert's list, narrowed to the rule's specialty where the list takes one", async () => {
    const specialty = '0000000c-0000-0000-0000-0000000000a1';
    const base = {...unread, pathwayId: null, appointmentId: null, subjectId: specialty};
    const referrals = {...base, id: '0000000c-0000-0000-0000-000000000041', kind: 'MonitoredReferralNotTriaged', title: 'Referrals awaiting triage'};
    const specimens = {...base, id: '0000000c-0000-0000-0000-000000000042', kind: 'MonitoredSpecimenOverdue', title: 'Specimens overdue'};
    mockApi({
      'GET /api/notifications': () => json({items: [referrals, specimens], unreadCount: 2}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Referrals awaiting triage/})).toHaveAttribute('href', `/referrals?specialty=${specialty}`);
    expect(list.getByRole('link', {name: /^Specimens overdue/})).toHaveAttribute('href', '/lab?view=Overdue');
  });

  it("opens a triaged referral's notification on the referral, with the referrals icon", async () => {
    const referralId = '0000000c-0000-0000-0000-000000000041';
    const triaged = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000040',
      kind: 'ReferralTriaged',
      title: 'Referral triaged',
      message: 'k.ashford accepted a Dermatology referral.',
      pathwayId,
      referralId,
    };
    const assigned = {...read, id: '0000000c-0000-0000-0000-000000000042', title: 'Another pathway'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [triaged, assigned], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    // The referral, not the pathway it was accepted into: the registrant writes to the referrer from there.
    const link = list.getByRole('link', {name: /^Referral triaged/});
    expect(link).toHaveAttribute('href', `/referrals/${referralId}`);
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons[0]).toBeDefined();
    expect(icons[0]).not.toBe(icons[1]);
    await user.click(link);

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${triaged.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/referrals/${referralId}`));
  });

  it("opens a rejected or lost specimen's notification on the specimen, each with its own icon", async () => {
    const specimenId = '00000051-0000-0000-0000-000000000001';
    const rejected = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000041',
      kind: 'SpecimenRejectedByLab',
      title: 'Specimen rejected',
      message: 'The lab rejected a Skin biopsy (punch) specimen.',
      pathwayId,
      subjectId: specimenId,
    };
    const lost = {...rejected, id: '0000000c-0000-0000-0000-000000000042', kind: 'SpecimenLost', title: 'Specimen lost', isRead: true};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [rejected, lost], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Specimen lost/})).toHaveAttribute('href', `/specimens/${specimenId}`);
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(icons[0]).toBeDefined();
    expect(icons[0]).not.toBe(icons[1]);
    await user.click(list.getByRole('link', {name: /^Specimen rejected/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${rejected.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/specimens/${specimenId}`));
  });

  it("opens a result's notifications on the result, even with a pathway, each with its own icon", async () => {
    const resultId = '00000061-0000-0000-0000-000000000001';
    const ready = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000061',
      kind: 'ResultReady',
      title: 'Result ready',
      message: 'A result is ready for your sign-off.',
      pathwayId,
      subjectId: resultId,
    };
    const reassigned = {...ready, id: '0000000c-0000-0000-0000-000000000062', kind: 'ResultReassigned', title: 'Result reassigned to you', isRead: true};
    const signedOff = {...ready, id: '0000000c-0000-0000-0000-000000000063', kind: 'ResultSignedOffByOther', title: 'Result signed off', isRead: true};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [ready, reassigned, signedOff], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Result reassigned to you/})).toHaveAttribute('href', `/results/${resultId}`);
    expect(list.getByRole('link', {name: /^Result signed off/})).toHaveAttribute('href', `/results/${resultId}`);
    const icons = list.getAllByRole('listitem').map((item) => item.querySelector('svg')?.outerHTML);
    expect(new Set(icons).size).toBe(3);
    await user.click(list.getByRole('link', {name: /^Result ready/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${ready.id}/read`));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/results/${resultId}`));
  });

  it("opens a letter's notification on the letter it names", async () => {
    const letterId = '0000000c-0000-0000-0000-000000000051';
    const notification = {
      ...unread,
      id: '0000000c-0000-0000-0000-000000000050',
      kind: 'LetterAwaitingApproval',
      title: 'Letter awaiting approval',
      message: 'k.ashford submitted a discharge summary for you to approve.',
      pathwayId,
      subjectId: letterId,
    };
    const returned = {...read, id: '0000000c-0000-0000-0000-000000000052', kind: 'LetterReturned', title: 'Letter returned', subjectId: letterId};
    const approved = {...read, id: '0000000c-0000-0000-0000-000000000053', kind: 'LetterApproved', title: 'Letter approved', subjectId: letterId};
    mockApi({
      'GET /api/notifications': () => json({items: [notification, returned, approved], unreadCount: 1}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    const list = await panel();
    expect(list.getByRole('link', {name: /^Letter returned/})).toHaveAttribute('href', `/letters/${letterId}`);
    expect(list.getByRole('link', {name: /^Letter approved/})).toHaveAttribute('href', `/letters/${letterId}`);
    await user.click(list.getByRole('link', {name: /^Letter awaiting approval/}));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/letters/${letterId}`));
  });

  it('marks read only the unread notifications about the pathway opened', async () => {
    const otherPathway = {...unread, id: '0000000c-0000-0000-0000-000000000005', title: 'Other pathway', pathwayId: '0000000c-0000-0000-0000-000000000006'};
    const fetch = mockApi({
      'GET /api/notifications': () => json({items: [unread, read, otherPathway], unreadCount: 2}),
      'POST /api/notifications/*': () => new Response(null, {status: 204}),
    });
    const {user} = renderWithApp(<NotificationBell />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: /^Notifications/}));
    await user.click((await panel()).getByRole('link', {name: /Test pathway/}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/notifications/${unread.id}/read`));
    expect(calls(fetch).filter((call) => call.startsWith('POST'))).toEqual([`POST /api/notifications/${unread.id}/read`]);
  });
});

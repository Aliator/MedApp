import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {STAFF_ME, STAFF_OTHER, testAppointment, testBlock, testCalendar} from '../../test/calendar';
import {adminUser, calls, expectAnnounced, json, mockApi, openDialog, pageOf, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import type {CurrentUser} from '../../auth/auth';
import {CalendarPage, calendarRange, formatRange} from './CalendarPage';
import type {CalendarSearch} from './calendarSearch';

const MONDAY = '2026-11-23';
const STAFF = [STAFF_ME, STAFF_OTHER];
const WEEK_TEMPLATES = [
  {id: 'week-standard', name: 'Standard week', isArchived: false},
  {id: 'week-old', name: 'Old week', isArchived: true},
];
const DAY_TEMPLATES = [
  {id: 'day-standard', name: 'Standard day', isArchived: false},
  {id: 'day-short', name: 'Short day', isArchived: false},
];

type Calendar = Parameters<typeof testCalendar>[0];

function renderPage({
  search: initial = {},
  calendar = {},
  handlers = {},
  user = staffUser,
}: {search?: CalendarSearch; calendar?: Partial<Calendar>; handlers?: Parameters<typeof mockApi>[0]; user?: CurrentUser} = {}) {
  const onSearchChange = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json(STAFF),
    'GET /api/calendar/week-templates': () => json(WEEK_TEMPLATES),
    'GET /api/calendar/day-templates': () => json(DAY_TEMPLATES),
    'GET /api/calendar/activity-types': () => json([{id: 'activity-lunch', name: 'Lunch', colour: 'green', isArchived: false}]),
    'GET /api/appointments/search': () => json(pageOf([testAppointment()], 3)),
    'GET /api/calendar': (request) => {
      const url = new URL(request.url);
      const from = url.searchParams.get('From')!;
      const to = url.searchParams.get('To')!;
      const staffId = url.searchParams.get('StaffUserId');
      const staff = STAFF.find((s) => s.userId === staffId) ?? STAFF_ME;
      const dayCount = (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1;
      return json(testCalendar({from, dayCount, today: MONDAY, staff, ...calendar}));
    },
    ...handlers,
  });
  function Harness() {
    const [search, setSearch] = useState<CalendarSearch>(initial);
    return (
      <CalendarPage
        search={search}
        onSearchChange={(next) => {
          onSearchChange(next);
          setSearch(next);
        }}
      />
    );
  }
  const result = renderWithApp(<Harness />, {user});
  return {...result, fetch, onSearchChange};
}

/** The open dialog, or the whole page until one opens (for `findBy` queries, which wait). */
const dialog = () => within(openDialog() ?? document.body);
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string, path: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method && new URL(r.url).pathname === path)!;
  return request.clone().json();
};

/** Gives each day's column a box: 100px wide, 880px tall (22px a row), side by side. */
function layOutColumns() {
  document.querySelectorAll<HTMLElement>('.mc-col').forEach((element, i) => {
    element.getBoundingClientRect = () => ({left: i * 100, right: (i + 1) * 100, top: 0, bottom: 880, width: 100, height: 880, x: i * 100, y: 0, toJSON: () => ({})});
  });
}
const yOf = (row: number) => row * 22 + 11;

const select = (name: string) => {
  const slot = screen.getByRole('button', {name});
  fireEvent.pointerDown(slot, {button: 0, pointerId: 1});
  fireEvent.pointerUp(slot, {pointerId: 1});
};

describe('calendar ranges', () => {
  it('names a week, across months and years', () => {
    expect(formatRange('2026-11-23', '2026-11-29')).toBe('23 to 29 November 2026');
    expect(formatRange('2026-11-30', '2026-12-06')).toBe('30 November to 6 December 2026');
    expect(formatRange('2026-12-28', '2027-01-03')).toBe('28 December 2026 to 3 January 2027');
    expect(calendarRange('2026-11-25', 'week')).toEqual({from: MONDAY, to: '2026-11-29'});
    expect(calendarRange('2026-11-25', 'day')).toEqual({from: '2026-11-25', to: '2026-11-25'});
  });
});

describe('CalendarPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it('shows my week, with how many appointments need booking', async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Calendar', level: 1})).toBeInTheDocument();
    expect(screen.getByText('My calendar · 23 to 29 November 2026')).toBeInTheDocument();
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'Needs Booking (3)'})).toHaveAttribute('href', '/calendar/needs-booking');
    expect(screen.getByRole('button', {name: 'Default week: None'})).toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-23&To=2026-11-29');
  });

  it('goes between weeks and days, and back to today', async () => {
    const {user, onSearchChange} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'Next week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-30'});
    expect(await screen.findByText('My calendar · 30 November to 6 December 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Previous week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: MONDAY});

    await user.click(await screen.findByRole('button', {name: 'Wed 25: show the day'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-25', view: 'day'});
    expect(await screen.findByText('My calendar · Wednesday 25 November 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Next day'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-26', view: 'day'});
    await user.click(screen.getByRole('button', {name: 'Previous day'}));
    await user.click(screen.getByRole('button', {name: 'Today'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: undefined, view: 'day'});

    await user.click(screen.getByRole('radio', {name: 'Week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: undefined, view: undefined});
  });

  it("shows someone else's calendar, chosen by name", async () => {
    const {user, onSearchChange} = renderPage();

    await user.click(await screen.findByRole('combobox', {name: 'Whose calendar'}));
    await user.click(await screen.findByRole('option', {name: 'b.staff'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({staff: STAFF_OTHER.userId, appointment: undefined});
    expect(await screen.findByText("b.staff's calendar · 23 to 29 November 2026")).toBeInTheDocument();
    await user.click(screen.getByRole('combobox', {name: 'Whose calendar'}));
    await user.click(await screen.findByRole('option', {name: 'My calendar'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({staff: undefined, appointment: undefined});
  });

  it('offers to book or block selected time on a calendar I can change', async () => {
    const {user} = renderPage();

    await screen.findByRole('group', {name: /^My calendar,/});
    select('Tuesday 24 November, 10:00, free');
    await user.click(await screen.findByRole('button', {name: 'Book appointment'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    expect(await dialog().findByText('Tuesday 24 November, 10:00 to 10:15 with test-user')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    select('Tuesday 24 November, 11:00, free');
    await user.click(await screen.findByRole('button', {name: 'Block time'}));
    expect(await screen.findByRole('heading', {name: 'Block Time'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('11:00');
  });

  it("goes straight to booking on a calendar I can only book on", async () => {
    renderPage({search: {staff: STAFF_OTHER.userId}, calendar: {canEdit: false}});

    await screen.findByRole('group', {name: /^b.staff's calendar,/});
    select('Tuesday 24 November, 10:00, free');
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    expect(await dialog().findByText('Tuesday 24 November, 10:00 to 10:15 with b.staff')).toBeInTheDocument();
    // Neither a template for the day nor the default week can be changed here.
    expect(screen.queryByRole('button', {name: /^Default week/})).not.toBeInTheDocument();
  });

  it('books or blocks from New, without the grid', async () => {
    // Before the working day, with its first half hour blocked and an appointment at 09:00.
    const {user} = renderPage({
      calendar: {blocks: [testBlock(MONDAY, '08:00', '08:30')], appointments: [testAppointment({date: MONDAY, start: '09:00', end: '09:30'})]},
    });

    await screen.findByRole('button', {name: /^Test Patient/});
    await user.click(await screen.findByRole('button', {name: 'New'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Block time'}));
    expect(await screen.findByRole('heading', {name: 'Block Time'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Monday 23 November');
    // It starts in the first free time, and ends where the appointment starts.
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('08:30');
    expect(dialog().getByRole('combobox', {name: /End/})).toHaveTextContent('09:00');
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await user.click(screen.getByRole('button', {name: 'New'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Book appointment'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
  });

  it("opens an appointment from the grid, and keeps it in the address", async () => {
    const appointment = testAppointment({id: 'appt-1', date: '2026-11-24'});
    const {user, onSearchChange} = renderPage({
      calendar: {appointments: [appointment]},
      handlers: {'GET /api/appointments/appt-1': () => json(appointment)},
    });

    await user.click(await screen.findByRole('button', {name: /^Test Patient, Knee review/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({appointment: 'appt-1'});
    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onSearchChange).toHaveBeenLastCalledWith({appointment: undefined});
  });

  it("opens a linked appointment on its own day and calendar", async () => {
    const id = '00000000-0000-0000-0000-00000000c0de';
    const appointment = testAppointment({id, date: '2026-12-02', staffUserId: STAFF_OTHER.userId, staffUsername: 'b.staff'});
    const {onSearchChange} = renderPage({search: {appointment: id}, handlers: {[`GET /api/appointments/${id}`]: () => json(appointment)}});

    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    await waitFor(() => expect(onSearchChange).toHaveBeenCalledWith({appointment: id, date: '2026-12-02', staff: STAFF_OTHER.userId}));
  });

  it("says when a linked appointment can't be found", async () => {
    renderPage({search: {appointment: '00000000-0000-0000-0000-000000000404'}});

    expect(await screen.findByText("That appointment couldn't be found. It may have been deleted.")).toBeInTheDocument();
  });

  it('opens a block in its dialog', async () => {
    const {user} = renderPage({calendar: {blocks: [testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1'})]}});

    await user.click(await screen.findByRole('button', {name: /^Lunch, 12:00 to 13:00/}));
    expect(await screen.findByRole('heading', {name: 'Change Blocked Time'})).toBeInTheDocument();
  });

  it('moves an appointment by dragging it, after confirming the new time', async () => {
    withAnnouncements();
    const appointment = testAppointment({id: 'appt-1', date: MONDAY, start: '09:00', end: '09:30'});
    const {fetch} = renderPage({calendar: {appointments: [appointment]}, handlers: {'POST /api/appointments/appt-1/book': () => json(appointment)}});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 150, clientY: yOf(8)});
    fireEvent.pointerUp(card, {pointerId: 1});

    expect(
      await dialog().findByText("Move Test Patient's appointment from Monday 23 November, 09:00 to 09:30 with test-user to Tuesday 24 November, 10:00 to 10:30?"),
    ).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', {name: 'Move'}));

    await expectAnnounced('Appointment moved');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-1/book')).toEqual({
      staffUserId: STAFF_ME.userId,
      startUtc: '2026-11-24T10:00:00.000Z',
      endUtc: '2026-11-24T10:30:00.000Z',
    });
  });

  it("shows why a move was refused, and lets a block be moved", async () => {
    withAnnouncements();
    const appointment = testAppointment({id: 'appt-1', date: MONDAY, start: '09:00', end: '09:30'});
    const block = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1'});
    const template = testBlock(MONDAY, '15:00', '15:30', {isFromTemplate: true, templateBlockId: 'tb-1', activityName: 'Admin time'});
    const {fetch} = renderPage({
      calendar: {appointments: [appointment], blocks: [block, template]},
      handlers: {
        'POST /api/appointments/appt-1/book': () => json({title: 'That time overlaps Lunch, 12:00 to 13:00.'}, 409),
        'PUT /api/calendar/blocks/block-1': () => json(block),
        [`PUT /api/calendar/staff/${STAFF_ME.userId}/days/${MONDAY}/template-blocks/tb-1`]: () => json(template),
      },
    });
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 50, clientY: yOf(16)});
    fireEvent.pointerUp(card, {pointerId: 1});
    fireEvent.click(await dialog().findByRole('button', {name: 'Move'}));
    expect(await screen.findByText('That time overlaps Lunch, 12:00 to 13:00.')).toBeInTheDocument();

    const lunch = screen.getByRole('button', {name: /^Lunch/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(lunch, {button: 0, pointerId: 2, clientX: 50, clientY: yOf(16)});
    fireEvent.pointerMove(lunch, {pointerId: 2, clientX: 250, clientY: yOf(20)});
    fireEvent.pointerUp(lunch, {pointerId: 2});
    await expectAnnounced('Blocked time moved');
    expect(await bodyOf(fetch, 'PUT', '/api/calendar/blocks/block-1')).toMatchObject({startUtc: '2026-11-25T13:00:00.000Z', endUtc: '2026-11-25T14:00:00.000Z'});

    // A template block moves within its own day.
    const admin = screen.getByRole('button', {name: /^Admin time/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(admin, {button: 0, pointerId: 3, clientX: 50, clientY: yOf(28)});
    fireEvent.pointerMove(admin, {pointerId: 3, clientX: 150, clientY: yOf(30)});
    fireEvent.pointerUp(admin, {pointerId: 3});
    fireEvent.pointerDown(admin, {button: 0, pointerId: 4, clientX: 50, clientY: yOf(28)});
    fireEvent.pointerMove(admin, {pointerId: 4, clientX: 50, clientY: yOf(32)});
    fireEvent.pointerUp(admin, {pointerId: 4});
    await waitFor(() => expect(calls(fetch).filter((c) => c.includes('template-blocks'))).toHaveLength(1));
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/staff/${STAFF_ME.userId}/days/${MONDAY}/template-blocks/tb-1`)).toMatchObject({
      startUtc: '2026-11-23T16:00:00.000Z',
    });
  });

  it("applies a day template to the day shown, or goes back to the week's", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      search: {view: 'day', date: '2026-11-24'},
      calendar: {},
      handlers: {
        'GET /api/calendar': (request) => {
          const from = new URL(request.url).searchParams.get('From')!;
          const calendar = testCalendar({from, dayCount: 1, today: MONDAY});
          calendar.days = calendar.days!.map((d) => ({...d, dayTemplateId: 'day-short', dayTemplateName: 'Short day', isDayTemplateOverridden: true}));
          return json(calendar);
        },
        [`PUT /api/calendar/staff/${STAFF_ME.userId}/days/2026-11-24/day-template`]: () => json({}),
        [`DELETE /api/calendar/staff/${STAFF_ME.userId}/days/2026-11-24/day-template`]: () => new Response(null, {status: 204}),
      },
    });

    await user.click(await screen.findByRole('button', {name: 'This day: Short day'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Standard day'}));
    await expectAnnounced('Standard day applied to Tuesday 24 November');
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/staff/${STAFF_ME.userId}/days/2026-11-24/day-template`)).toEqual({dayTemplateId: 'day-standard'});

    await user.click(screen.getByRole('button', {name: 'This day: Short day'}));
    await user.click(await screen.findByRole('menuitem', {name: 'No template'}));
    await expectAnnounced('No template on this day');

    await user.click(screen.getByRole('button', {name: 'This day: Short day'}));
    await user.click(await screen.findByRole('menuitem', {name: "Back to the week's template"}));
    await expectAnnounced("Back to the week's template");
  });

  it('sets the default week, and shows when that fails', async () => {
    withAnnouncements();
    let status = 200;
    const {user, fetch} = renderPage({
      user: adminUser,
      handlers: {
        [`PUT /api/calendar/staff/${STAFF_ME.userId}/week-template`]: () => (status === 200 ? json({}) : json({title: 'That template is archived.'}, 409)),
      },
    });

    await user.click(await screen.findByRole('button', {name: 'Default week: None'}));
    expect(screen.queryByRole('menuitem', {name: 'Old week'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('menuitem', {name: 'Standard week'}));
    await expectAnnounced('Default week set to Standard week');
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/staff/${STAFF_ME.userId}/week-template`)).toEqual({weekTemplateId: 'week-standard'});

    status = 409;
    await user.click(screen.getByRole('button', {name: 'Default week: None'}));
    await user.click(await screen.findByRole('menuitem', {name: 'None'}));
    expect(await screen.findByText('That template is archived.')).toBeInTheDocument();
  });

  it('says when the calendar cannot be loaded', async () => {
    renderPage({handlers: {'GET /api/calendar': () => json({}, 500)}});

    expect(await screen.findByText('The calendar could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("leaves an appointment where it was when the move is called off, and says why a block couldn't move", async () => {
    const appointment = testAppointment({id: 'appt-1', date: MONDAY, start: '09:00', end: '09:30'});
    const block = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1'});
    const {fetch} = renderPage({
      calendar: {appointments: [appointment], blocks: [block]},
      handlers: {'PUT /api/calendar/blocks/block-1': () => json({title: 'That time overlaps Training, 14:00 to 15:00.'}, 409)},
    });
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 150, clientY: yOf(8)});
    fireEvent.pointerUp(card, {pointerId: 1});
    fireEvent.click(await dialog().findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);

    const lunch = screen.getByRole('button', {name: /^Lunch/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(lunch, {button: 0, pointerId: 2, clientX: 50, clientY: yOf(16)});
    fireEvent.pointerMove(lunch, {pointerId: 2, clientX: 50, clientY: yOf(24)});
    fireEvent.pointerUp(lunch, {pointerId: 2});
    expect(await screen.findByText('That time overlaps Training, 14:00 to 15:00.')).toBeInTheDocument();
  });

  it("says when the day's template can't be changed", async () => {
    const {user} = renderPage({
      search: {view: 'day', date: '2026-11-24'},
      handlers: {[`PUT /api/calendar/staff/${STAFF_ME.userId}/days/2026-11-24/day-template`]: () => json({title: 'That day has passed.'}, 409)},
    });

    await user.click(await screen.findByRole('button', {name: "This day's template"}));
    await user.click(await screen.findByRole('menuitem', {name: 'Short day'}));
    expect(await screen.findByText('That day has passed.')).toBeInTheDocument();
  });

  it("says when going back to the week's template fails", async () => {
    const {user} = renderPage({
      search: {view: 'day', date: '2026-11-24'},
      handlers: {
        'GET /api/calendar': (request) => {
          const from = new URL(request.url).searchParams.get('From')!;
          const calendar = testCalendar({from, dayCount: 1, today: MONDAY});
          calendar.days = calendar.days!.map((d) => ({...d, dayTemplateId: null, isDayTemplateOverridden: true}));
          return json(calendar);
        },
        [`DELETE /api/calendar/staff/${STAFF_ME.userId}/days/2026-11-24/day-template`]: () => json({title: 'That day has passed.'}, 409),
      },
    });

    await user.click(await screen.findByRole('button', {name: "This day's template"}));
    await user.click(await screen.findByRole('menuitem', {name: "Back to the week's template"}));
    expect(await screen.findByText('That day has passed.')).toBeInTheDocument();
  });

  it('goes to a date typed into the date picker', async () => {
    const {user, onSearchChange} = renderPage();

    const input = await screen.findByRole('combobox', {name: 'Go to date'});
    await user.clear(input);
    await user.type(input, '2 December 2026{Enter}');
    await waitFor(() => expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-12-02'}));
  });
});

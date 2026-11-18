import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {QueryClient} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import type {TeamCalendar} from '../../api/calendar';
import type {MeetingDialogProps} from '../../components/calendar/MeetingDialog';
import {HalfViewContext} from '../../components/pages/halfView';
import {calendarDay, STAFF_ME, STAFF_OTHER, testAppointment, testBlock, testCalendar, testMeeting, ukTime} from '../../test/calendar';
import {adminUser, calls, expectAnnounced, json, managerUser, mockApi, openDialog, pageOf, renderApp, renderWithApp, staffUser, userWithRoles, withAnnouncements} from '../../test/render';
import type {CurrentUser} from '../../auth/auth';
import {CalendarPage, calendarRange, formatRange, preloadCalendarPage, selectionMenuPlacement, shownView} from './CalendarPage';
import {givenWidth} from '../../test/layout';
import {testSession, testSessionDetail} from '../../test/clinics';
import type {CalendarSearch} from './calendarSearch';

// The meeting dialog has tests of its own: here, a stand-in that shows what it was opened with.
vi.mock('../../components/calendar/MeetingDialog', () => ({
  MeetingDialog: ({isOpen, meeting, initial, onClose}: MeetingDialogProps) =>
    isOpen ? (
      <section aria-label="Meeting dialog" data-meeting={meeting?.id ?? ''} data-initial={JSON.stringify(initial ?? null)}>
        <button type="button" onClick={onClose}>
          Close meeting
        </button>
      </section>
    ) : null,
}));

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

/** A third member of staff, after the others by name, so the team view's order shows. */
const STAFF_THIRD = {userId: '00000000-0000-0000-0000-0000000000c3', username: 'c.staff'};

/**
 * The team view's day: test-user with a meeting, b.staff with an appointment, a block and the meeting too, everyone
 * free from 10:00 to 11:00. Members come in no particular order (`members` overrides each, in this order); the page
 * puts them in name order.
 */
function teamDay(date: string, members: Partial<Calendar>[] = []): TeamCalendar {
  const meeting = testMeeting({id: 'meeting-1', date, start: '09:00', end: '09:30', title: 'MDT'});
  return {
    date,
    day: calendarDay(date, MONDAY),
    members: [
      testCalendar({from: date, dayCount: 1, today: MONDAY, staff: STAFF_ME, meetings: [meeting], ...members[0]}),
      testCalendar({
        from: date,
        dayCount: 1,
        today: MONDAY,
        staff: STAFF_OTHER,
        appointments: [testAppointment({id: 'appt-b', date, start: '12:00', end: '12:30', staffUserId: STAFF_OTHER.userId, staffUsername: STAFF_OTHER.username})],
        blocks: [testBlock(date, '14:00', '15:00', {blockId: 'block-b', staffUserId: STAFF_OTHER.userId})],
        meetings: [meeting],
        ...members[1],
      }),
    ],
    everyoneFree: [{startUtc: ukTime(date, '10:00'), endUtc: ukTime(date, '11:00')}],
  };
}

/** Answers the API as the Calendar page asks it: the staff, their templates, the team view and calendars. */
function answerCalendarApi(calendar: Partial<Calendar> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'GET /api/calendar/staff': () => json(STAFF),
    // Each person's own templates.
    'GET /api/calendar/staff/*': (request) => json(new URL(request.url).pathname.endsWith('/day-templates') ? DAY_TEMPLATES : WEEK_TEMPLATES),
    'GET /api/calendar/team': (request) => json(teamDay(new URL(request.url).searchParams.get('Date')!)),
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
}

function renderPage({
  search: initial = {},
  calendar = {},
  handlers = {},
  user = staffUser,
  isNarrow = false,
  cardWidth,
}: {
  search?: CalendarSearch;
  calendar?: Partial<Calendar>;
  handlers?: Parameters<typeof mockApi>[0];
  user?: CurrentUser;
  isNarrow?: boolean;
  /** The calendar's card, measured this wide (jsdom lays out nothing, so it's never measured otherwise). */
  cardWidth?: number;
} = {}) {
  if (cardWidth !== undefined) givenWidth(cardWidth);
  const onSearchChange = vi.fn();
  const fetch = answerCalendarApi(calendar, handlers);
  function Harness() {
    const [search, setSearch] = useState<CalendarSearch>(initial);
    return (
      <CalendarPage
        search={search}
        onSearchChange={(next, options) => {
          // Options only when given, so a plain change is checked as one argument.
          if (options) onSearchChange(next, options);
          else onSearchChange(next);
          setSearch(next);
        }}
      />
    );
  }
  const ui: ReactNode = <Harness />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{ui}</HalfViewContext.Provider> : ui, {user});
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

/** An item's card on the grid, by the start of its name. It has a grab cursor while it can be dragged. */
const cardOf = (name: RegExp) => screen.getByRole('button', {name}).closest<HTMLElement>('[data-kind]')!;

/** Drags a card with the pointer from one point to another (columns laid out by `layOutColumns`). */
function dragCard(card: HTMLElement, pointerId: number, [fromX, fromY]: [number, number], [toX, toY]: [number, number]) {
  fireEvent.pointerDown(card, {button: 0, pointerId, clientX: fromX, clientY: fromY});
  fireEvent.pointerMove(card, {pointerId, clientX: toX, clientY: toY});
  fireEvent.pointerUp(card, {pointerId});
}

/** The stand-in meeting dialog, once open. */
const meetingDialog = () => screen.findByRole('region', {name: 'Meeting dialog'});

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

// Slow under coverage on a loaded machine (CI): give it room.
describe('CalendarPage', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it('shows my week, with how many appointments need booking', async () => {
    const {fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Calendar', level: 1})).toBeInTheDocument();
    expect(screen.getByText('My calendar · 23 to 29 November 2026')).toBeInTheDocument();
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'Requires Appointment (3)'})).toHaveAttribute('href', '/calendar/requires-appointment');
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

    // Named in the address, so the week stays a week if the window narrows or a half view opens.
    await user.click(screen.getByRole('radio', {name: 'Week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: undefined, view: 'week'});
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

  it('opens the menu from the selected time, and gives the focus back to that time when it closes', async () => {
    const {user} = renderPage();

    await screen.findByRole('group', {name: /^My calendar,/});
    select('Tuesday 24 November, 10:00, free');

    // The held selection is the button the menu opens from (so it's placed beside it), and says the menu is open.
    const selection = await screen.findByRole('button', {name: 'Selected time, 10:00 to 10:15'});
    await waitFor(() => expect(selection).toHaveAttribute('aria-expanded', 'true'));
    expect(selection).toHaveAttribute('tabindex', '-1');
    await user.click(await screen.findByRole('button', {name: 'Block time'}));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(openDialog()).toBeNull());

    select('Tuesday 24 November, 11:00, free');
    expect(await screen.findByRole('button', {name: 'Book appointment'})).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('button', {name: /^Selected time/})).not.toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('button', {name: 'Tuesday 24 November, 11:00, free'})).toHaveFocus());
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

  it("records a past appointment from New, with the calendar's owner to start with", async () => {
    const {user} = renderPage({search: {staff: STAFF_OTHER.userId}});

    await user.click(await screen.findByRole('button', {name: 'New'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Record past appointment'}));

    expect(await screen.findByRole('heading', {name: 'Record Past Appointment'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /With/})).toHaveTextContent('b.staff'));
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
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

  it('opens a linked meeting in its dialog, even one not on the calendar shown, and closes it from the URL', async () => {
    const id = '00000000-0000-0000-0000-00000000e0de';
    // An organiser told of a decline, who isn't going: the meeting isn't on their calendar.
    const meeting = testMeeting({id, date: '2026-11-24', title: 'Synthetic governance', attendees: [{...STAFF_OTHER, reply: 'Declined'}]});
    const {user, onSearchChange} = renderPage({search: {date: '2026-11-24', view: 'day', meeting: id}, handlers: {[`GET /api/calendar/meetings/${id}`]: () => json(meeting)}});

    const shown = await screen.findByRole('region', {name: 'Meeting dialog'});
    expect(shown).toHaveAttribute('data-meeting', id);
    await user.click(within(shown).getByRole('button', {name: 'Close meeting'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-11-24', view: 'day', meeting: undefined});
  });

  it("says when a linked meeting can't be found, as when it was cancelled", async () => {
    renderPage({search: {meeting: '00000000-0000-0000-0000-000000000404'}});

    expect(await screen.findByText("That meeting couldn't be found. It may have been cancelled.")).toBeInTheDocument();
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

  it('opens a clinic session from the grid, books one of its slots, or changes it', async () => {
    const session = testSession({date: MONDAY, booked: 1, staffUserId: STAFF_ME.userId});
    const {user} = renderPage({
      calendar: {sessions: [session]},
      user: adminUser,
      handlers: {
        'GET /api/clinics/sessions/session-1': () => json(testSessionDetail(session)),
        'GET /api/clinics/sessions': () => json([session]),
        'GET /api/rooms': () => json([]),
        'GET /api/specialties': () => json([]),
      },
    });

    // Clinics is deprecated: no button for it; the MDT list's is there instead.
    expect(await screen.findByRole('link', {name: 'MDT'})).toHaveAttribute('href', '/mdt');
    expect(screen.queryByRole('link', {name: 'Clinics'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('button', {name: /^Synthetic dermatology clinic session/}));
    expect(await screen.findByRole('heading', {name: 'Clinic Session'})).toBeInTheDocument();
    await user.click(await dialog().findByRole('button', {name: 'Book 09:20'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();
    expect(await dialog().findByRole('button', {name: '09:20', pressed: true})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await user.click(await screen.findByRole('button', {name: /^Synthetic dermatology clinic session/}));
    await user.click(await dialog().findByRole('button', {name: 'Edit'}));
    expect(await screen.findByRole('heading', {name: 'Edit Clinic Session'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(await screen.findByRole('button', {name: /^Synthetic dermatology clinic session/}));
    await dialog().findByRole('button', {name: 'Edit'});
    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    await waitFor(() => expect(openDialog()).toBeNull());
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
      // A move of the Booked appointment the calendar showed, so a change by someone else meanwhile is refused.
      expectedStatus: 'Booked',
    });
  });

  it("shows why an appointment's move was refused, and doesn't move blocked time while Edit blocks is off", async () => {
    const appointment = testAppointment({id: 'appt-1', date: MONDAY, start: '09:00', end: '09:30'});
    const block = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1'});
    const {fetch} = renderPage({
      calendar: {appointments: [appointment], blocks: [block]},
      handlers: {'POST /api/appointments/appt-1/book': () => json({title: 'That time overlaps Lunch, 12:00 to 13:00.'}, 409)},
    });
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    // Appointments move whatever the mode.
    dragCard(card, 1, [50, yOf(4)], [50, yOf(16)]);
    fireEvent.click(await dialog().findByRole('button', {name: 'Move'}));
    expect(await screen.findByText('That time overlaps Lunch, 12:00 to 13:00.')).toBeInTheDocument();

    // Edit blocks starts off: blocked time stays put.
    expect(screen.getByRole('button', {name: 'Edit blocks'})).toHaveAttribute('aria-pressed', 'false');
    const lunch = cardOf(/^Lunch/);
    expect(lunch.style.cursor).toBe('');
    dragCard(lunch, 2, [50, yOf(16)], [250, yOf(20)]);
    expect(calls(fetch).filter((c) => c.startsWith('PUT '))).toEqual([]);
  });

  it('moves blocked time by dragging it while Edit blocks is on: a stored block to another day, a template block within its own', async () => {
    withAnnouncements();
    let release = () => {};
    const saved = new Promise<void>((resolve) => (release = resolve));
    const block = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1', note: 'Canteen'});
    const template = testBlock(MONDAY, '15:00', '15:30', {isFromTemplate: true, templateBlockId: 'tb-1', activityName: 'Admin time'});
    const {user, fetch} = renderPage({
      calendar: {blocks: [block, template]},
      handlers: {
        // Held until the test lets it go, so the block can be seen where it was dropped while it saves.
        'PUT /api/calendar/blocks/block-1': async () => {
          await saved;
          return json(block);
        },
        [`PUT /api/calendar/staff/${STAFF_ME.userId}/days/${MONDAY}/template-blocks/tb-1`]: () => json(template),
      },
    });
    await user.click(await screen.findByRole('button', {name: 'Edit blocks'}));
    expect(screen.getByRole('button', {name: 'Edit blocks'})).toHaveAttribute('aria-pressed', 'true');
    layOutColumns();

    expect(cardOf(/^Lunch/).style.cursor).toBe('grab');
    dragCard(cardOf(/^Lunch/), 1, [50, yOf(16)], [250, yOf(20)]);
    // Shown where it was dropped, on Wednesday, while it saves; no other block can be picked up meanwhile.
    const wednesday = screen.getByRole('group', {name: /^Wednesday 25 November/});
    expect(await within(wednesday).findByRole('button', {name: /^Lunch, Canteen, 13:00 to 14:00/})).toBeInTheDocument();
    expect(cardOf(/^Admin time/).style.cursor).toBe('');
    release();
    await expectAnnounced('Lunch moved to Wednesday 25 November, 13:00 to 14:00');
    // The same block at the new time: its activity and note as they were.
    expect(await bodyOf(fetch, 'PUT', '/api/calendar/blocks/block-1')).toEqual({
      activityTypeId: 'activity-lunch',
      startUtc: '2026-11-25T13:00:00.000Z',
      endUtc: '2026-11-25T14:00:00.000Z',
      note: 'Canteen',
    });

    // A template block is changed for its own day only, so it can't go to another day; within it, it moves.
    await waitFor(() => expect(cardOf(/^Admin time/).style.cursor).toBe('grab'));
    const admin = cardOf(/^Admin time/);
    dragCard(admin, 2, [50, yOf(28)], [150, yOf(30)]);
    dragCard(admin, 3, [50, yOf(28)], [50, yOf(32)]);
    await expectAnnounced('Admin time moved to 16:00 to 16:30');
    expect(calls(fetch).filter((c) => c.includes('template-blocks'))).toHaveLength(1);
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/staff/${STAFF_ME.userId}/days/${MONDAY}/template-blocks/tb-1`)).toEqual({
      activityTypeId: 'activity-lunch',
      startUtc: '2026-11-23T16:00:00.000Z',
      endUtc: '2026-11-23T16:30:00.000Z',
      note: null,
    });

    // Off again: blocks stay put.
    await user.click(screen.getByRole('button', {name: 'Edit blocks'}));
    expect(cardOf(/^Lunch/).style.cursor).toBe('');
  });

  it("puts a refused block back, with the API's words, and loads the calendar again", async () => {
    const block = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-1'});
    const {user, fetch} = renderPage({
      calendar: {blocks: [block]},
      handlers: {'PUT /api/calendar/blocks/block-1': () => json({title: 'That time overlaps Test Patient, 13:00 to 13:30.'}, 409)},
    });
    await user.click(await screen.findByRole('button', {name: 'Edit blocks'}));
    layOutColumns();
    const calendarLoads = () => calls(fetch).filter((c) => c.startsWith('GET /api/calendar?')).length;
    const loadsBefore = calendarLoads();

    dragCard(cardOf(/^Lunch/), 1, [50, yOf(16)], [50, yOf(20)]);
    expect(await screen.findByText('That time overlaps Test Patient, 13:00 to 13:30.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: /^Lunch, 12:00 to 13:00/})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Lunch, 13:00 to 14:00/})).not.toBeInTheDocument();
    await waitFor(() => expect(calendarLoads()).toBeGreaterThan(loadsBefore));
  });

  it('never lets blocked time on a past day, or a meeting, move by Edit blocks', async () => {
    // Wednesday 25 November: Monday is past, and today's Lunch can still move.
    vi.setSystemTime(new Date('2026-11-25T07:00:00Z'));
    const past = testBlock(MONDAY, '12:00', '13:00', {blockId: 'block-past', activityName: 'Training'});
    const today = testBlock('2026-11-25', '12:00', '13:00', {blockId: 'block-today'});
    const meeting = testMeeting({id: 'meeting-1', date: '2026-11-25', start: '09:00', end: '09:30', title: 'MDT', canEdit: false});
    const {user} = renderPage({calendar: {blocks: [past, today], meetings: [meeting], today: '2026-11-25'}});
    await user.click(await screen.findByRole('button', {name: 'Edit blocks'}));

    expect(cardOf(/^Lunch/).style.cursor).toBe('grab');
    // The calendar says Monday is past (the API's canEdit would say so too; this one claims it can change).
    expect(cardOf(/^Training/).style.cursor).toBe('');
    // A meeting moves by its own rule (an admin), not by Edit blocks.
    expect(cardOf(/^MDT/).style.cursor).toBe('');
  });

  it('offers Edit blocks only to someone who can change the calendar, never in the team view, and under View options when narrow', async () => {
    withAnnouncements();
    const other = renderPage({search: {staff: STAFF_OTHER.userId}, calendar: {canEdit: false}});
    await screen.findByRole('group', {name: /^b.staff's calendar,/});
    expect(screen.queryByRole('button', {name: 'Edit blocks'})).not.toBeInTheDocument();
    other.unmount();

    const team = renderPage({user: adminUser, search: {view: 'team'}});
    await screen.findByRole('group', {name: 'b.staff, Monday 23 November, today'});
    expect(screen.queryByRole('button', {name: 'Edit blocks'})).not.toBeInTheDocument();
    team.unmount();

    const {user} = renderPage({isNarrow: true, cardWidth: 500, search: {view: 'week'}, calendar: {blocks: [testBlock(MONDAY, '12:00', '13:00')]}});
    await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'});
    expect(screen.queryByRole('button', {name: 'Edit blocks'})).not.toBeInTheDocument();
    expect(cardOf(/^Lunch/).style.cursor).toBe('');
    await user.click(screen.getByRole('button', {name: 'View options'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Edit blocks: Off'}));
    await expectAnnounced('Edit blocks on: drag blocked time to move it');
    expect(cardOf(/^Lunch/).style.cursor).toBe('grab');
    await user.click(screen.getByRole('button', {name: 'View options'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Edit blocks: On'}));
    await expectAnnounced('Edit blocks off');
    expect(cardOf(/^Lunch/).style.cursor).toBe('');
  });

  it('offers Apply Template in the day, week and compact week, to someone who can change the calendar', async () => {
    const week = renderPage();
    expect(await screen.findByRole('button', {name: 'Apply Template'})).toBeInTheDocument();
    // The old menu for the day shown is gone.
    expect(screen.queryByRole('button', {name: /^This day/})).not.toBeInTheDocument();
    week.unmount();

    const day = renderPage({search: {view: 'day', date: '2026-11-24'}});
    expect(await screen.findByRole('button', {name: 'Apply Template'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^This day/})).not.toBeInTheDocument();
    day.unmount();

    const compact = renderPage({isNarrow: true, cardWidth: 500, search: {view: 'week'}});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-compact');
    expect(screen.getByRole('button', {name: 'Apply Template'})).toBeInTheDocument();
    compact.unmount();

    // Not on a calendar they can't change.
    renderPage({search: {staff: STAFF_OTHER.userId}, calendar: {canEdit: false}});
    await screen.findByRole('group', {name: /^b.staff's calendar,/});
    expect(screen.queryByRole('button', {name: 'Apply Template'})).not.toBeInTheDocument();
  });

  it('leaves Apply Template out of the team view', async () => {
    renderPage({user: adminUser, search: {view: 'team'}});

    await screen.findByRole('group', {name: /^Team,/});
    expect(screen.queryByRole('button', {name: 'Apply Template'})).not.toBeInTheDocument();
  });

  it('starts Apply Template with the day shown ticked in the day view, and in the week the date the address names', async () => {
    const {user, fetch, unmount} = renderPage({search: {view: 'day', date: '2026-11-25'}});

    await user.click(await screen.findByRole('button', {name: 'Apply Template'}));
    expect(await dialog().findByRole('checkbox', {name: /^Wed 25/})).toBeChecked();
    expect(dialog().getByRole('checkbox', {name: /^Mon 23/})).not.toBeChecked();
    // The dialog reads the week's days without their appointments, so it records no patients as listed.
    expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-23&To=2026-11-29&WithoutAppointments=true');
    unmount();

    // A week with a date in the address: that day, not today.
    const dated = renderPage({search: {date: '2026-11-26'}});
    await dated.user.click(await screen.findByRole('button', {name: 'Apply Template'}));
    expect(await dialog().findByRole('checkbox', {name: /^Thu 26/})).toBeChecked();
    expect(dialog().getAllByRole('checkbox').filter((c) => (c as HTMLInputElement).checked)).toHaveLength(1);
    dated.unmount();

    // A week with no date in the address: nothing ticked, not even today.
    const week = renderPage();
    await week.user.click(await screen.findByRole('button', {name: 'Apply Template'}));
    expect(await dialog().findByRole('checkbox', {name: /^Mon 23/})).not.toBeChecked();
    expect(dialog().getAllByRole('checkbox').filter((c) => (c as HTMLInputElement).checked)).toHaveLength(0);
    week.unmount();

    // Another week, from the address: its date there.
    const next = renderPage({search: {date: '2026-12-01'}});
    await next.user.click(await screen.findByRole('button', {name: 'Apply Template'}));
    expect(await dialog().findByRole('checkbox', {name: /^Tue 1/})).toBeChecked();
    expect(dialog().getAllByRole('checkbox').filter((c) => (c as HTMLInputElement).checked)).toHaveLength(1);
  });

  it("applies one of the person's day templates to days of the week, and gives the focus back to the button", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      user: adminUser,
      search: {staff: STAFF_OTHER.userId},
      handlers: {
        [`PUT /api/calendar/staff/${STAFF_OTHER.userId}/days/2026-11-23/day-template`]: () => json({}),
        [`PUT /api/calendar/staff/${STAFF_OTHER.userId}/days/2026-11-24/day-template`]: () => json({}),
      },
    });

    const button = await screen.findByRole('button', {name: 'Apply Template'});
    await user.click(button);
    await user.click(await dialog().findByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: /^Short day/}));
    await user.click(await dialog().findByRole('checkbox', {name: /^Mon 23/}));
    await user.click(dialog().getByRole('checkbox', {name: /^Tue 24/}));
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    await expectAnnounced('Short day applied to Mon 23 and Tue 24');
    await waitFor(() => expect(openDialog()).toBeNull());
    await waitFor(() => expect(button).toHaveFocus());
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/staff/${STAFF_OTHER.userId}/days/2026-11-24/day-template`)).toEqual({dayTemplateId: 'day-short'});
    // b.staff's own day templates.
    expect(calls(fetch)).toContain(`GET /api/calendar/staff/${STAFF_OTHER.userId}/day-templates?includeArchived=true`);
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

  it('leaves an appointment where it was when the move is called off', async () => {
    const appointment = testAppointment({id: 'appt-1', date: MONDAY, start: '09:00', end: '09:30'});
    const {fetch} = renderPage({calendar: {appointments: [appointment]}});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 150, clientY: yOf(8)});
    fireEvent.pointerUp(card, {pointerId: 1});
    fireEvent.click(await dialog().findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
    expect(screen.getByRole('button', {name: /^Test Patient, Knee review: Assessment, 09:00 to 09:30/})).toBeInTheDocument();
  });

  it('goes to a date typed into the date picker', async () => {
    const {user, onSearchChange} = renderPage();

    const input = await screen.findByRole('combobox', {name: 'Go to date'});
    await user.clear(input);
    await user.type(input, '2 December 2026{Enter}');
    await waitFor(() => expect(onSearchChange).toHaveBeenLastCalledWith({date: '2026-12-02'}));
  });

  it("leads to the person's own calendar templates: mine, or for an admin, theirs", async () => {
    const {unmount} = renderPage();
    expect(await screen.findByRole('link', {name: 'My calendar templates'})).toHaveAttribute('href', '/calendar/templates');
    unmount();

    const admin = renderPage({user: adminUser, search: {staff: STAFF_OTHER.userId, view: 'day', date: '2026-11-24'}});
    expect(await screen.findByRole('link', {name: "b.staff's calendar templates"})).toHaveAttribute('href', `/calendar/templates?staff=${STAFF_OTHER.userId}`);
    // The default week comes from b.staff's own week templates.
    await waitFor(() => expect(calls(admin.fetch)).toContain(`GET /api/calendar/staff/${STAFF_OTHER.userId}/week-templates?includeArchived=true`));
    admin.unmount();

    // Someone else's, for anyone but an admin: nothing to lead to.
    renderPage({search: {staff: STAFF_OTHER.userId}, calendar: {canEdit: false}});
    await screen.findByRole('group', {name: /^b.staff's calendar,/});
    expect(screen.queryByRole('link', {name: /calendar templates/})).not.toBeInTheDocument();
  });

  it('shows meetings, opens one in its dialog, and lets an admin move one by dragging it', async () => {
    withAnnouncements();
    let status = 200;
    const meeting = testMeeting({id: 'meeting-1', date: MONDAY, start: '09:00', end: '09:30', title: 'MDT', notes: 'Bring the rota'});
    const {user, fetch} = renderPage({
      user: adminUser,
      calendar: {meetings: [meeting]},
      handlers: {
        'PUT /api/calendar/meetings/meeting-1': () => (status === 200 ? json(meeting) : json({title: 'b.staff has Lunch, 10:00 to 11:00.'}, 409)),
      },
    });

    await user.click(await screen.findByRole('button', {name: 'MDT, meeting, 09:00 to 09:30, 2 attending'}));
    expect(await meetingDialog()).toHaveAttribute('data-meeting', 'meeting-1');
    await user.click(screen.getByRole('button', {name: 'Close meeting'}));
    expect(screen.queryByRole('region', {name: 'Meeting dialog'})).not.toBeInTheDocument();

    const card = screen.getByRole('button', {name: /^MDT/}).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();
    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 150, clientY: yOf(8)});
    fireEvent.pointerUp(card, {pointerId: 1});
    await expectAnnounced('Meeting moved');
    // The same meeting at the new time: its title, notes and attendees kept, and the version the calendar showed, so
    // the API can refuse it if someone has changed the meeting since.
    expect(await bodyOf(fetch, 'PUT', '/api/calendar/meetings/meeting-1')).toEqual({
      title: 'MDT',
      notes: 'Bring the rota',
      attendeeUserIds: [STAFF_ME.userId, STAFF_OTHER.userId],
      startUtc: '2026-11-24T10:00:00.000Z',
      endUtc: '2026-11-24T10:30:00.000Z',
      expectedLastUpdated: meeting.lastUpdated,
    });

    // A clash, or a change made meanwhile, is the API's 409, in its words; the calendar is loaded again, so a retry
    // starts from the meeting as it is now.
    status = 409;
    const calendarLoads = () => calls(fetch).filter((c) => c.startsWith('GET /api/calendar?')).length;
    const loadsBefore = calendarLoads();
    fireEvent.pointerDown(card, {button: 0, pointerId: 2, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 2, clientX: 150, clientY: yOf(8)});
    fireEvent.pointerUp(card, {pointerId: 2});
    expect(await screen.findByText('b.staff has Lunch, 10:00 to 11:00.')).toBeInTheDocument();
    await waitFor(() => expect(calendarLoads()).toBeGreaterThan(loadsBefore));
  });

  it("offers in New only what the roles allow: a manager arranges meetings and sees the team, but books nothing", async () => {
    const {user} = renderPage({user: managerUser});
    await user.click(await screen.findByRole('button', {name: 'New'}));
    expect(await screen.findByRole('menuitem', {name: 'New meeting'})).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: 'Book appointment'})).not.toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: 'Record past appointment'})).not.toBeInTheDocument();
    expect(screen.getByRole('radio', {name: 'Team'})).toBeInTheDocument();
  });

  it('has the MDT list beside Requires Appointment for those who can view patients, and no Clinics (deprecated)', async () => {
    const staff = renderPage();
    expect(await screen.findByRole('link', {name: 'MDT'})).toHaveAttribute('href', '/mdt');
    expect(screen.queryByRole('link', {name: 'Clinics'})).not.toBeInTheDocument();
    staff.unmount();

    renderPage({user: userWithRoles('test-nobody')});
    expect(await screen.findByRole('link', {name: /^Requires Appointment/})).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'MDT'})).not.toBeInTheDocument();
  });

  it("has no New at all for someone who can't book, on a calendar they can't change", async () => {
    renderPage({user: userWithRoles('test-lab', 'Lab'), search: {staff: STAFF_OTHER.userId}, calendar: {canEdit: false}});

    expect(await screen.findByRole('group', {name: /^b.staff's calendar,/})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'New'})).not.toBeInTheDocument();
    expect(screen.queryByRole('radio', {name: 'Team'})).not.toBeInTheDocument();
  });

  it('offers admins a new meeting, with the person whose calendar it is', async () => {
    const own = renderPage({user: adminUser});
    await own.user.click(await screen.findByRole('button', {name: 'New'}));
    await own.user.click(await screen.findByRole('menuitem', {name: 'New meeting'}));
    expect(await meetingDialog()).toHaveAttribute('data-initial', 'null');
    own.unmount();

    const theirs = renderPage({user: adminUser, search: {staff: STAFF_OTHER.userId}});
    await screen.findByRole('group', {name: /^b.staff's calendar,/});
    await theirs.user.click(screen.getByRole('button', {name: 'New'}));
    await theirs.user.click(await screen.findByRole('menuitem', {name: 'New meeting'}));
    expect(JSON.parse((await meetingDialog()).getAttribute('data-initial')!)).toEqual({attendeeUserIds: [STAFF_OTHER.userId]});
    theirs.unmount();

    // Staff who aren't admins can't arrange one.
    const staff = renderPage();
    await staff.user.click(await screen.findByRole('button', {name: 'New'}));
    expect(await screen.findByRole('menuitem', {name: 'Book appointment'})).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: 'New meeting'})).not.toBeInTheDocument();
  });

  it('shows admins the team view: when everyone is free, then each member of staff by name', async () => {
    const {fetch} = renderPage({user: adminUser, search: {view: 'team'}});

    expect(await screen.findByRole('group', {name: 'Team, Monday 23 November 2026'})).toBeInTheDocument();
    expect(screen.getByText('Team · Monday 23 November 2026')).toBeInTheDocument();
    expect(screen.getByRole('radio', {name: 'Team'})).toBeChecked();
    expect(document.querySelector('.mc-head')).toHaveTextContent('Everyone freeb.stafftest-user');
    expect(screen.getByRole('group', {name: 'Everyone free, Monday 23 November, today'})).toBeInTheDocument();
    expect(screen.getByRole('group', {name: 'b.staff, Monday 23 November, today'})).toHaveTextContent('Test Patient');
    expect(within(screen.getByRole('group', {name: 'test-user, Monday 23 November, today'})).getByRole('button', {name: /^MDT, meeting/})).toBeInTheDocument();
    expect(calls(fetch)).toContain('GET /api/calendar/team?Date=2026-11-23');
    expect(calls(fetch).some((c) => c.startsWith('GET /api/calendar?'))).toBe(false);
    // Who it shows, instead of whose calendar; nothing that belongs to one person's calendar.
    expect(screen.getByRole('combobox', {name: 'Who'})).toBeInTheDocument();
    expect(screen.queryByRole('combobox', {name: 'Whose calendar'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Default week/})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /calendar templates/})).not.toBeInTheDocument();
  });

  it('greys "Everyone free" on a closed day, labelled as the person columns are, with nothing to select', async () => {
    const closed = (c: NonNullable<TeamCalendar['members']>[number]) => ({
      ...c,
      days: c.days!.map((d) => ({...d, isWorkingDay: false, closedDayName: 'Staff training day'})),
    });
    renderPage({
      user: adminUser,
      search: {view: 'team'},
      handlers: {
        'GET /api/calendar/team': () => {
          const team = teamDay(MONDAY);
          return json({...team, members: team.members!.map(closed), everyoneFree: []});
        },
      },
    });

    const everyone = await screen.findByRole('group', {name: 'Everyone free, Monday 23 November, closed: Staff training day'});
    expect(everyone).toHaveTextContent('Closed: Staff training day');
    expect(within(everyone).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByRole('group', {name: 'b.staff, Monday 23 November, closed: Staff training day'})).toBeInTheDocument();
  });

  it("offers a meeting when everyone shown is free, and booking or blocking in a person's column", async () => {
    const {user} = renderPage({user: adminUser, search: {view: 'team'}});
    await screen.findByRole('group', {name: /^Team,/});

    select('Everyone free, Monday 23 November, 10:00, free');
    await user.click(await screen.findByRole('button', {name: 'New meeting'}));
    expect(JSON.parse((await meetingDialog()).getAttribute('data-initial')!)).toEqual({
      attendeeUserIds: [STAFF_OTHER.userId, STAFF_ME.userId],
      startUtc: '2026-11-23T10:00:00.000Z',
      endUtc: '2026-11-23T10:15:00.000Z',
    });
    await user.click(screen.getByRole('button', {name: 'Close meeting'}));

    select('b.staff, Monday 23 November, 10:00, free');
    await user.click(await screen.findByRole('button', {name: 'Book appointment'}));
    expect(await dialog().findByText('Monday 23 November, 10:00 to 10:15 with b.staff')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    select('b.staff, Monday 23 November, 11:00, free');
    await user.click(await screen.findByRole('button', {name: 'Block time'}));
    expect(await screen.findByRole('heading', {name: 'Block Time'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('11:00');
  });

  it('opens New meeting from the selected time in Everyone free, as from any column, and gives the focus back when it closes', async () => {
    const {user} = renderPage({user: adminUser, search: {view: 'team'}});
    await screen.findByRole('group', {name: /^Team,/});

    select('Everyone free, Monday 23 November, 10:00, free');

    // The held selection is the button the menu opens from (PR #96), in this column too.
    const selection = await screen.findByRole('button', {name: 'Selected time, 10:00 to 10:15'});
    await waitFor(() => expect(selection).toHaveAttribute('aria-expanded', 'true'));
    expect(selection).toHaveAttribute('tabindex', '-1');
    expect(await screen.findByRole('button', {name: 'New meeting'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book appointment'})).not.toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('button', {name: /^Selected time/})).not.toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('button', {name: 'Everyone free, Monday 23 November, 10:00, free'})).toHaveFocus());
  });

  it("goes straight to booking in the column of someone the admin can only book for", async () => {
    renderPage({
      user: adminUser,
      search: {view: 'team'},
      handlers: {'GET /api/calendar/team': () => json(teamDay(MONDAY, [{}, {canEdit: false}]))},
    });
    await screen.findByRole('group', {name: /^Team,/});

    select('b.staff, Monday 23 November, 10:00, free');
    expect(await dialog().findByText('Monday 23 November, 10:00 to 10:15 with b.staff')).toBeInTheDocument();
  });

  it("opens what's in a person's column, and narrows who the team view shows", async () => {
    const appointment = teamDay(MONDAY).members![1].appointments![0];
    const {user, fetch, onSearchChange} = renderPage({
      user: adminUser,
      search: {view: 'team'},
      handlers: {
        'GET /api/calendar/staff': () => json([...STAFF, STAFF_THIRD]),
        'GET /api/appointments/appt-b': () => json(appointment),
      },
    });
    const bStaff = await screen.findByRole('group', {name: 'b.staff, Monday 23 November, today'});

    await user.click(within(bStaff).getByRole('button', {name: /^Test Patient/}));
    // Still the team view, on its day.
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'team', appointment: 'appt-b'});
    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(within(bStaff).getByRole('button', {name: /^Lunch/}));
    expect(await screen.findByRole('heading', {name: 'Change Blocked Time'})).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(within(bStaff).getByRole('button', {name: /^MDT/}));
    expect(await meetingDialog()).toHaveAttribute('data-meeting', 'meeting-1');
    await user.click(screen.getByRole('button', {name: 'Close meeting'}));

    await user.click(screen.getByRole('combobox', {name: 'Who'}));
    await user.click(await screen.findByRole('option', {name: 'c.staff'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'team', appointment: undefined, who: [STAFF_THIRD.userId]});
    await waitFor(() => expect(calls(fetch)).toContain(`GET /api/calendar/team?Date=2026-11-23&StaffUserIds=${STAFF_THIRD.userId}`));
    await user.keyboard('{Escape}');

    // A new meeting from New is with the people shown.
    await user.click(screen.getByRole('button', {name: 'New'}));
    await user.click(await screen.findByRole('menuitem', {name: 'New meeting'}));
    expect(JSON.parse((await meetingDialog()).getAttribute('data-initial')!)).toEqual({attendeeUserIds: [STAFF_THIRD.userId]});
    await user.click(screen.getByRole('button', {name: 'Close meeting'}));
    // Booking from New asks with whom: no one person's calendar is shown.
    await user.click(screen.getByRole('button', {name: 'New'}));
    expect(screen.queryByRole('menuitem', {name: 'Block time'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('menuitem', {name: 'Book appointment'}));
    expect(await screen.findByRole('heading', {name: 'Book Appointment'})).toBeInTheDocument();

    // Everyone again.
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('combobox', {name: 'Who'}));
    await user.click(await screen.findByRole('option', {name: 'c.staff'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'team', appointment: undefined, who: undefined});
  });

  it("moves appointments and meetings within a person's column in the team view, on their calendar, but not blocks", async () => {
    withAnnouncements();
    const {fetch} = renderPage({
      user: adminUser,
      search: {view: 'team'},
      handlers: {
        'PUT /api/calendar/meetings/meeting-1': () => json({}),
        'PUT /api/calendar/blocks/block-b': () => json({}),
        'POST /api/appointments/appt-b/book': () => json({}),
      },
    });
    const bStaff = await screen.findByRole('group', {name: 'b.staff, Monday 23 November, today'});
    // Everyone free, b.staff, then test-user: 100px each.
    layOutColumns();

    // Edit blocks isn't offered here, so blocked time stays put (it still opens in its dialog).
    const lunch = within(bStaff).getByRole('button', {name: /^Lunch/}).closest<HTMLElement>('[data-kind]')!;
    expect(lunch.style.cursor).toBe('');
    dragCard(lunch, 1, [150, yOf(24)], [150, yOf(28)]);
    expect(calls(fetch)).not.toContain('PUT /api/calendar/blocks/block-b');

    const appointment = within(bStaff).getByRole('button', {name: /^Test Patient/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(appointment, {button: 0, pointerId: 2, clientX: 150, clientY: yOf(16)});
    fireEvent.pointerMove(appointment, {pointerId: 2, clientX: 150, clientY: yOf(20)});
    fireEvent.pointerUp(appointment, {pointerId: 2});
    fireEvent.click(await dialog().findByRole('button', {name: 'Move'}));
    await expectAnnounced('Appointment moved');
    expect(await bodyOf(fetch, 'POST', '/api/appointments/appt-b/book')).toMatchObject({staffUserId: STAFF_OTHER.userId, startUtc: '2026-11-23T13:00:00.000Z'});

    const mdt = within(screen.getByRole('group', {name: 'test-user, Monday 23 November, today'})).getByRole('button', {name: /^MDT/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(mdt, {button: 0, pointerId: 3, clientX: 250, clientY: yOf(4)});
    fireEvent.pointerMove(mdt, {pointerId: 3, clientX: 250, clientY: yOf(12)});
    fireEvent.pointerUp(mdt, {pointerId: 3});
    await expectAnnounced('Meeting moved');
    expect(await bodyOf(fetch, 'PUT', '/api/calendar/meetings/meeting-1')).toMatchObject({startUtc: '2026-11-23T11:00:00.000Z'});
  });

  it('goes to a linked appointment on its day in the team view, staying in the team view', async () => {
    const id = '00000000-0000-0000-0000-00000000c0de';
    const appointment = testAppointment({id, date: '2026-12-02', staffUserId: STAFF_OTHER.userId, staffUsername: 'b.staff'});
    const {onSearchChange} = renderPage({user: adminUser, search: {view: 'team', appointment: id}, handlers: {[`GET /api/appointments/${id}`]: () => json(appointment)}});

    expect(await screen.findByRole('heading', {name: 'Appointment'})).toBeInTheDocument();
    await waitFor(() => expect(onSearchChange).toHaveBeenCalledWith({view: 'team', appointment: id, date: '2026-12-02', staff: undefined}));
    expect(await screen.findByText('Team · Wednesday 2 December 2026')).toBeInTheDocument();
  });

  it('says when the team view cannot be loaded, and shows nothing for a day it has no details of', async () => {
    const {unmount} = renderPage({user: adminUser, search: {view: 'team'}, handlers: {'GET /api/calendar/team': () => json({}, 500)}});
    expect(await screen.findByText('The calendar could not be loaded. Please try again.')).toBeInTheDocument();
    unmount();

    renderPage({user: adminUser, search: {view: 'team'}, handlers: {'GET /api/calendar/team': () => json({date: MONDAY})}});
    expect(await screen.findByRole('group', {name: 'Team, Monday 23 November 2026'})).toBeInTheDocument();
    expect(document.querySelectorAll('.mc-col')).toHaveLength(0);
  });

  it('never shows the team view to anyone but admins, and keeps it in a narrow window', async () => {
    const staff = renderPage({search: {view: 'team'}});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toBeInTheDocument();
    expect(screen.queryByRole('radio', {name: 'Team'})).not.toBeInTheDocument();
    staff.unmount();

    // In a narrow window or a half view, people start on a day, and can choose the week.
    const narrowStaff = renderPage({isNarrow: true});
    expect(await screen.findByText('My calendar · Monday 23 November 2026')).toBeInTheDocument();
    expect(screen.getByRole('radio', {name: 'Day'})).toBeChecked();
    expect(screen.getByRole('radio', {name: 'Week'})).toBeInTheDocument();
    expect(screen.queryByRole('radio', {name: 'Team'})).not.toBeInTheDocument();
    narrowStaff.unmount();

    // Admins can choose the team too.
    const {user, onSearchChange} = renderPage({user: adminUser, isNarrow: true});
    expect(await screen.findByText('My calendar · Monday 23 November 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', {name: 'Team'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'team'});
    expect(await screen.findByRole('group', {name: 'Team, Monday 23 November 2026'})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Next day'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'team', date: '2026-11-24'});
  });

  it('offers the week in a narrow window, and keeps it, in a compact form: Monday to Friday, headed short', async () => {
    const {user, onSearchChange, fetch} = renderPage({isNarrow: true, cardWidth: 500});
    expect(await screen.findByText('My calendar · Monday 23 November 2026')).toBeInTheDocument();
    expect(await screen.findByRole('group', {name: 'My calendar, Monday 23 November 2026'})).not.toHaveAttribute('data-compact');

    await user.click(screen.getByRole('radio', {name: 'Week'}));
    // Named in the address, as a narrow window would otherwise open on a day.
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'week'});
    expect(await screen.findByText('My calendar · 23 to 29 November 2026')).toBeInTheDocument();
    expect(screen.getByRole('radio', {name: 'Week'})).toBeChecked();
    expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-23&To=2026-11-29');

    const grid = await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'});
    expect(grid).toHaveAttribute('data-compact');
    expect(document.querySelectorAll('.mc-col')).toHaveLength(5);
    expect(document.querySelector('.mc-head')).toHaveTextContent('Mon 23Tue 24Wed 25Thu 26Fri 27');
    expect(screen.queryByRole('group', {name: /^Saturday/})).not.toBeInTheDocument();
    expect(screen.getByRole('group', {name: 'Monday 23 November, today'})).toBeInTheDocument();

    // Weeks at a time, and a day from its heading.
    await user.click(screen.getByRole('button', {name: 'Next week'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'week', date: '2026-11-30'});
    expect(await screen.findByText('My calendar · 30 November to 6 December 2026')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', {name: 'Wed 2: show Wednesday 2 December'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'day', date: '2026-12-02'});
    expect(await screen.findByText('My calendar · Wednesday 2 December 2026')).toBeInTheDocument();
  });

  it('opens the week named in the address in a narrow window, and offers to book or block time selected on it', async () => {
    const {user} = renderPage({isNarrow: true, cardWidth: 500, search: {view: 'week'}});

    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-compact');
    select('Tuesday 24 November, 10:00, free');
    await user.click(await screen.findByRole('button', {name: 'Book appointment'}));
    expect(await dialog().findByText('Tuesday 24 November, 10:00 to 10:15 with test-user')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    select('Thursday 26 November, 11:00, free');
    expect(await screen.findByRole('button', {name: 'Block time'})).toBeInTheDocument();
  });

  it('switches between 15 and 5 minute rows, kept in the address, and books or blocks on 5-minute steps', async () => {
    const {user, onSearchChange} = renderPage();
    const grid = await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'});
    expect(grid).toHaveAttribute('data-row-minutes', '15');

    // Named by what it shows, so it isn't read as an appointment's length.
    await user.click(screen.getByRole('button', {name: 'Rows: 15 min'}));
    await user.click(await screen.findByRole('menuitem', {name: '5 minutes'}));
    // Replacing the address: the row length is a preference, not a step Back goes through.
    expect(onSearchChange).toHaveBeenLastCalledWith({rows: 5}, {replace: true});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-row-minutes', '5');

    select('Tuesday 24 November, 10:05, free');
    await user.click(await screen.findByRole('button', {name: 'Book appointment'}));
    // A click selects 15 minutes, from the 5-minute row clicked.
    expect(await dialog().findByText('Tuesday 24 November, 10:05 to 10:20 with test-user')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    select('Tuesday 24 November, 11:10, free');
    await user.click(await screen.findByRole('button', {name: 'Block time'}));
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('11:10');
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'Rows: 5 min'}));
    await user.click(await screen.findByRole('menuitem', {name: '15 minutes'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({rows: undefined}, {replace: true});
  });

  it("shows the week compact by its card's width, not the window's", async () => {
    // A wide window whose card has only 500px (under the full week's 608px and a scrollbar): compact.
    const narrowCard = renderPage({cardWidth: 500});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-compact');
    narrowCard.unmount();

    // A half view with room for the whole week: the full week, weekends and all.
    renderPage({isNarrow: true, cardWidth: 1000, search: {view: 'week'}});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).not.toHaveAttribute('data-compact');
    expect(document.querySelectorAll('.mc-col')).toHaveLength(7);
  });

  it('remembers the row length last chosen, for an address that names none', async () => {
    const {user, unmount} = renderPage();
    await user.click(await screen.findByRole('button', {name: 'Rows: 15 min'}));
    await user.click(await screen.findByRole('menuitem', {name: '5 minutes'}));
    expect(localStorage.getItem('medapp.calendarRows')).toBe('5');
    unmount();

    // Back from the side nav, Home or a notification (no rows in the address): 5-minute rows.
    const again = renderPage();
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-row-minutes', '5');
    again.unmount();

    // A link that names 15 still shows 15.
    renderPage({search: {rows: 15}});
    expect(await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-row-minutes', '15');
    expect(screen.getByRole('button', {name: 'Rows: 15 min'})).toBeInTheDocument();
  });

  it('opens with the row length the address names', async () => {
    const {user} = renderPage({isNarrow: true, cardWidth: 500, search: {view: 'week', rows: 5}});
    const grid = await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'});
    expect(grid).toHaveAttribute('data-row-minutes', '5');
    expect(grid).toHaveAttribute('data-compact');
    // A narrow toolbar keeps the row length under View options.
    await user.click(screen.getByRole('button', {name: 'View options'}));
    expect(await screen.findByText('Rows: 5 min')).toBeInTheDocument();
  });

  it('puts whose calendar, the rows, the default week and the templates under View options in a narrow toolbar', async () => {
    const {user, onSearchChange} = renderPage({isNarrow: true, cardWidth: 500, search: {view: 'week'}});
    await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'});
    // Getting about stays in view, the date without its weekday so it fits beside the buttons.
    expect(screen.getByRole('button', {name: 'Previous week'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Today'})).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', {name: 'View'})).toBeInTheDocument();
    expect(screen.queryByRole('combobox', {name: 'Whose calendar'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Rows/})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Default week/})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'My calendar templates'})).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'View options'}));
    expect(await screen.findByRole('menuitem', {name: 'Whose calendar: My calendar'})).toBeInTheDocument();
    expect(screen.getByRole('menuitem', {name: /Default week: None/})).toBeInTheDocument();
    expect(screen.getByRole('menuitem', {name: 'My calendar templates'})).toHaveAttribute('href', '/calendar/templates');
    await user.click(screen.getByRole('menuitem', {name: '5 minutes'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'week', rows: 5}, {replace: true});

    await user.click(screen.getByRole('button', {name: 'View options'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Whose calendar: My calendar'}));
    await user.click(await screen.findByRole('menuitem', {name: STAFF_OTHER.username}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'week', rows: 5, staff: STAFF_OTHER.userId, appointment: undefined});
  });

  it("keeps the menu for the selected time to its buttons' width, not the selection's", async () => {
    renderPage({isNarrow: true});
    await screen.findByRole('group', {name: /^My calendar,/});

    select('Monday 23 November, 10:00, free');
    const menu = (await screen.findByRole('button', {name: 'Book appointment'})).closest('[data-layer-root], [data-astryx-layer-root]') as HTMLElement;
    // Astryx sets the surface's width from the prop (`width: var(--x-width)`): left to its default, the surface would
    // stretch to the selection, a whole column wide.
    expect(menu.style.getPropertyValue('--x-width')).toBe('max-content');
  });

  it('keeps the same time at the top when the row length changes through the address, as the real router has it', async () => {
    // Columns as tall as their rows, 22px each (jsdom lays out nothing).
    const offsetHeight = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains('mc-col') ? (this.closest<HTMLElement>('.medapp-calendar')!.dataset.rowMinutes === '5' ? 2640 : 880) : 0;
      },
    });
    try {
      answerCalendarApi({}, {
        'GET /api/notifications': () => json({items: [], unreadCount: 0}),
        'GET /api/auth/me/settings': () => json({settings: null}),
      });
      // The router restores the scroll positions it saved for an address, on every navigation that resets scroll.
      const {user, router} = renderApp({path: '/calendar', user: staffUser, scrollRestoration: true});
      const grid = await screen.findByRole('group', {name: 'My calendar, 23 to 29 November 2026'}, {timeout: 15_000});
      const scroller = grid.querySelector<HTMLElement>('.mc-scroll')!;

      // 10:30 at the top: a quarter of the way through the working day.
      scroller.scrollTop = 220;
      fireEvent.scroll(scroller);
      await user.click(screen.getByRole('button', {name: 'Rows: 15 min'}));
      await user.click(await screen.findByRole('menuitem', {name: '5 minutes'}));
      await waitFor(() => expect(screen.getByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-row-minutes', '5'));
      // Past the router's own work after the navigation, which once put back the old 220px (09:00 at the top).
      await new Promise((resolve) => setTimeout(resolve, 50));
      expect(scroller.scrollTop).toBe(660);
      // The address changed in place: no new history entry for Back to step through.
      expect(router.state.location.search).toEqual({rows: 5});
      expect(router.history.length).toBe(1);

      await user.click(screen.getByRole('button', {name: 'Rows: 5 min'}));
      await user.click(await screen.findByRole('menuitem', {name: '15 minutes'}));
      await waitFor(() => expect(screen.getByRole('group', {name: 'My calendar, 23 to 29 November 2026'})).toHaveAttribute('data-row-minutes', '15'));
      await new Promise((resolve) => setTimeout(resolve, 50));
      expect(scroller.scrollTop).toBe(220);
    } finally {
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', offsetHeight);
    }
  });
});

describe('the Calendar page outside React', () => {
  it('shows the team view only to admins, and a day by default in a narrow window', () => {
    expect(shownView({view: 'team'}, true)).toBe('team');
    expect(shownView({view: 'team'}, false)).toBe('week');
    expect(shownView({view: 'team'}, false, true)).toBe('day');
    expect(shownView({view: 'team'}, true, true)).toBe('team');
    expect(shownView({}, false)).toBe('week');
    // A narrow window opens on a day, but keeps a week named in the address.
    expect(shownView({}, false, true)).toBe('day');
    expect(shownView({view: 'week'}, false, true)).toBe('week');
    expect(shownView({view: 'day'}, false)).toBe('day');
  });

  it('opens the menu for selected time beside it, or below it when the selection is too wide for beside', () => {
    // A week's column: beside it. A day's column in a wide window: below it, near what was selected, rather than
    // flipped to the far side over the side nav.
    expect(selectionMenuPlacement(0)).toBe('end');
    expect(selectionMenuPlacement(120)).toBe('end');
    expect(selectionMenuPlacement(320)).toBe('end');
    expect(selectionMenuPlacement(800)).toBe('below');
  });

  it("preloads the team view's day for admins, or the person's calendar and then their templates", async () => {
    const fetch = mockApi({
      'GET /api/calendar/team': () => json(teamDay(MONDAY)),
      'GET /api/calendar': () => json(testCalendar({from: MONDAY, today: MONDAY})),
      'GET /api/calendar/staff': () => json(STAFF),
      'GET /api/calendar/staff/*': () => json([]),
      'GET /api/appointments/search': () => json(pageOf([])),
    });
    const queryClient = new QueryClient();

    await preloadCalendarPage(queryClient, {view: 'team', who: [STAFF_OTHER.userId]}, true, MONDAY);
    await waitFor(() => expect(calls(fetch)).toContain(`GET /api/calendar/team?Date=${MONDAY}&StaffUserIds=${STAFF_OTHER.userId}`));

    // The calendar isn't here yet in the browser: its templates wait for the page.
    await preloadCalendarPage(queryClient, {view: 'team', date: '2026-11-24'}, false, MONDAY);
    await waitFor(() => expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-23&To=2026-11-29'));
    expect(calls(fetch).some((c) => c.includes('-templates'))).toBe(false);

    // Once it is, whose it is says whose week templates. Their day templates wait for the page (Apply Template).
    queryClient.setQueryData(['calendar', 'days', 'me', '2026-11-24', '2026-11-24'], testCalendar({from: '2026-11-24', dayCount: 1, today: MONDAY}));
    await preloadCalendarPage(queryClient, {view: 'day', date: '2026-11-24'}, false, MONDAY);
    await waitFor(() => expect(calls(fetch)).toContain(`GET /api/calendar/staff/${STAFF_ME.userId}/week-templates?includeArchived=true`));
    expect(calls(fetch).some((c) => c.includes('/day-templates'))).toBe(false);
    queryClient.clear();
  });
});

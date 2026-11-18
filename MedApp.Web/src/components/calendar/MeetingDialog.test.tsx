import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import type {Availability, Meeting} from '../../api/calendar';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {HalfViewContext} from '../pages/halfView';
import {
  adminUser,
  expectAnnounced,
  isDisabled,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  staffUser,
  withAnnouncements,
} from '../../test/render';
import {MeetingDialog, type MeetingDialogProps} from './MeetingDialog';

/** Synthetic staff: the signed-in admin and two others. */
const ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-admin'};
const KAY = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'k.ashford'};
const BEN = {userId: '00000000-0000-0000-0000-0000000000c3', username: 'b.staff'};
const CAL = {userId: '00000000-0000-0000-0000-0000000000c4', username: 'c.staff'};
const DEV = {userId: '00000000-0000-0000-0000-0000000000c5', username: 'd.staff'};
const EVE = {userId: '00000000-0000-0000-0000-0000000000c6', username: 'e.staff'};
const STAFF = [ME, BEN, CAL, DEV, EVE, KAY].map((s) => ({...s, weekTemplateId: null, weekTemplateName: null}));
const ALL = STAFF.map((s) => s.userId).sort();
const GONE = '00000000-0000-0000-0000-0000000000d4';

const MEETING: Meeting = {
  id: '00000000-0000-0000-0000-00000000e001',
  title: 'Team huddle',
  notes: 'Rota for December',
  date: '2026-11-24',
  startUtc: '2026-11-24T10:00:00Z',
  endUtc: '2026-11-24T11:00:00Z',
  organiserUserId: ME.userId,
  organiserUsername: ME.username,
  attendees: [KAY, {userId: GONE, username: null}],
  canEdit: true,
  createdAtUtc: '2026-11-10T09:00:00Z',
  lastUpdated: '2026-11-10T09:00:00Z',
};

/** A meeting time on a November day (UK time is UTC then). */
const at = (date: string, start: string, end: string) => ({startUtc: `${date}T${start}:00Z`, endUtc: `${date}T${end}:00Z`});

/** What the availability endpoint answers, by its query: suggestions and busy time for the range asked about. */
type AvailabilityAnswer = (query: URLSearchParams) => Partial<Availability> | Response;

const SUGGESTIONS = [at('2026-11-17', '10:00', '11:00'), at('2026-11-17', '14:00', '15:00'), at('2026-11-19', '09:00', '10:00')];

const defaultAvailability: AvailabilityAnswer = () => ({suggestions: SUGGESTIONS, days: [], busy: []});

function renderDialog(
  props: Partial<MeetingDialogProps> = {},
  {availability = defaultAvailability, handlers = {}, user = adminUser, isNarrow = false}: {
    availability?: AvailabilityAnswer;
    handlers?: Parameters<typeof mockApi>[0];
    user?: typeof adminUser;
    isNarrow?: boolean;
  } = {},
) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json(STAFF),
    'GET /api/calendar/availability': (request) => {
      const answer = availability(new URL(request.url).searchParams);
      return answer instanceof Response ? answer : json({days: [], suggestions: [], busy: [], ...answer});
    },
    ...handlers,
  });
  const ui = (
    <SaveShortcutProvider>
      <MeetingDialog isOpen onClose={onClose} onSaved={onSaved} {...props} />
    </SaveShortcutProvider>
  );
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{ui}</HalfViewContext.Provider> : ui, {user});
  return {...result, fetch, onClose, onSaved};
}

/** The open dialog, or the whole page until one opens (for `findBy` queries, which wait). */
const dialog = () => within(openDialog() ?? document.body);

const requests = (fetch: ReturnType<typeof mockApi>, method: string, path: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method && new URL(r.url).pathname === path);

/** The availability queries asked, each as its range, length, people and meeting left out. */
const availabilityAsked = (fetch: ReturnType<typeof mockApi>) =>
  requests(fetch, 'GET', '/api/calendar/availability').map((r) => {
    const q = new URL(r.url).searchParams;
    return {
      from: q.get('From'),
      to: q.get('To'),
      minutes: q.get('DurationMinutes'),
      people: q.getAll('StaffUserIds').sort(),
      except: q.get('ExceptMeetingId'),
    };
  });

const bodyOf = (fetch: ReturnType<typeof mockApi>, method: string, path: string) => requests(fetch, method, path).at(-1)!.clone().json();

const suggestion = (name: string) => dialog().getByRole('button', {name});

// Slow under coverage on a loaded machine (CI): give it room.
describe('MeetingDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    // Monday 16 November 2026, 09:00 UK time.
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-16T09:00:00Z')});
  });

  it("shows a meeting's details, with a deleted account as Deleted user, and nothing to change for staff", async () => {
    const {user, fetch, onClose} = renderDialog({meeting: {...MEETING, canEdit: false, organiserUsername: null}}, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Meeting'})).toBeInTheDocument();
    expect(dialog().getByText('Team huddle')).toBeInTheDocument();
    expect(dialog().getByText('Tuesday 24 November, 10:00 to 11:00')).toBeInTheDocument();
    expect(dialog().getByText('Attendees (2)')).toBeInTheDocument();
    expect(dialog().getByText('k.ashford, Deleted user')).toBeInTheDocument();
    expect(dialog().getByText('Deleted user')).toBeInTheDocument();
    expect(dialog().getByText('Rota for December')).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Change'})).not.toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: 'Cancel Meeting'})).not.toBeInTheDocument();

    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onClose).toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('arranges a new meeting at a suggested time, with everyone, and announces it', async () => {
    withAnnouncements();
    const saved = {...MEETING, id: 'new-meeting'};
    const {user, fetch, onSaved, onClose} = renderDialog({}, {handlers: {'POST /api/calendar/meetings': () => json(saved, 201)}});

    expect(await screen.findByRole('heading', {name: 'New Meeting'})).toBeInTheDocument();
    expect(dialog().getByText("About the meeting, for staff. Don't put patient details here.")).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Length/})).toHaveTextContent('1 hour');
    // The organiser goes by default: suggestions are for them alone until others are chosen.
    expect(await dialog().findByRole('checkbox', {name: "I'm going too (test-admin)"})).toBeChecked();
    expect(await dialog().findByRole('button', {name: 'Tue 17 Nov, 10:00 to 11:00'})).toBeInTheDocument();
    expect(dialog().getByText('Monday 16 November to Sunday 29 November')).toBeInTheDocument();
    expect(dialog().getByText('Choose a suggested time, or a day and a start.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Meeting'}))).toBe(true);

    await user.type(dialog().getByRole('textbox', {name: /Title/}), 'Team huddle');
    await user.click(dialog().getByRole('button', {name: 'Everyone'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Everyone'}))).toBe(true);
    expect(dialog().getByRole('combobox', {name: /Who/})).toHaveTextContent('Everyone');
    await waitFor(() => expect(availabilityAsked(fetch).at(-1)).toEqual({from: '2026-11-16', to: '2026-11-29', minutes: '60', people: ALL, except: null}));

    await user.click(await dialog().findByRole('button', {name: 'Tue 17 Nov, 14:00 to 15:00'}));
    expect(suggestion('Tue 17 Nov, 14:00 to 15:00')).toHaveAttribute('aria-pressed', 'true');
    expect(dialog().getByText('Tuesday 17 November, 14:00 to 15:00, 6 people')).toBeInTheDocument();
    // The time chosen shows in the day and start too, and nobody's busy then.
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('14:00');
    expect(dialog().getByText('Everyone is free then.')).toBeInTheDocument();
    await user.type(dialog().getByRole('textbox', {name: /Notes/}), ' Rota ');
    await user.click(dialog().getByRole('button', {name: 'Add Meeting'}));

    await expectAnnounced('Meeting saved');
    expect(onSaved).toHaveBeenCalledWith(saved);
    expect(onClose).toHaveBeenCalled();
    const body = await bodyOf(fetch, 'POST', '/api/calendar/meetings');
    expect({...body, attendeeUserIds: [...body.attendeeUserIds].sort()}).toEqual({
      title: 'Team huddle',
      notes: 'Rota',
      startUtc: '2026-11-17T14:00:00.000Z',
      endUtc: '2026-11-17T15:00:00.000Z',
      attendeeUserIds: ALL,
    });
  });

  it('chooses people, takes the organiser off, and refreshes the suggestions as who and the length change', async () => {
    const {user, fetch} = renderDialog();

    const who = await dialog().findByRole('combobox', {name: /Who/});
    // The organiser isn't offered in Who: the checkbox says whether they go.
    await user.click(who);
    expect(screen.queryByRole('option', {name: /test-admin/})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', {name: /k\.ashford/}));
    await user.keyboard('{Escape}');
    expect(who).toHaveTextContent('k.ashford');
    await waitFor(() => expect(availabilityAsked(fetch).at(-1)?.people).toEqual([ME.userId, KAY.userId].sort()));

    await user.click(dialog().getByRole('checkbox', {name: /I'm going too/}));
    await waitFor(() => expect(availabilityAsked(fetch).at(-1)?.people).toEqual([KAY.userId]));

    await user.click(dialog().getByRole('combobox', {name: /Length/}));
    await user.click(screen.getByRole('option', {name: '30 minutes'}));
    await waitFor(() => expect(availabilityAsked(fetch).at(-1)).toMatchObject({minutes: '30', people: [KAY.userId]}));

    await user.click(dialog().getByRole('button', {name: 'Clear'}));
    expect(dialog().getByText('Choose who’s going to see when they’re all free.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Clear'}))).toBe(true);
    expect(isDisabled(dialog().getByRole('button', {name: 'Later suggestions'}))).toBe(true);
  });

  it('shows several people as a count, leaves out anyone without a role, and looks for later suggestions, then back to the soonest', async () => {
    const {user, fetch} = renderDialog(
      {initial: {attendeeUserIds: [KAY.userId, BEN.userId, CAL.userId, GONE, ME.userId, DEV.userId]}},
      {
        availability: (q) =>
          q.get('From') === '2026-11-16' ? {suggestions: SUGGESTIONS} : q.get('From') === '2026-11-20' ? {suggestions: []} : {suggestions: [at('2026-12-01', '09:00', '10:00'), {startUtc: null as unknown as string}]},
      },
    );

    expect(await dialog().findByRole('button', {name: 'Tue 17 Nov, 10:00 to 11:00'})).toBeInTheDocument();
    // More than three: a count.
    expect(dialog().getByRole('combobox', {name: /Who/})).toHaveTextContent('4 people');
    expect(dialog().queryByRole('button', {name: 'Soonest'})).not.toBeInTheDocument();

    // From the day after the last suggestion (Thursday 19th).
    await user.click(dialog().getByRole('button', {name: 'Later suggestions'}));
    expect(await dialog().findByText(/Nothing fits for everyone in these two weeks/)).toBeInTheDocument();
    expect(dialog().getByText('Friday 20 November to Thursday 3 December')).toBeInTheDocument();

    // Nothing fitted: from the day after the range.
    await user.click(dialog().getByRole('button', {name: 'Later suggestions'}));
    expect(await dialog().findByRole('button', {name: 'Tue 1 Dec, 09:00 to 10:00'})).toBeInTheDocument();
    expect(availabilityAsked(fetch).map((a) => a.from)).toEqual(['2026-11-16', '2026-11-20', '2026-12-04']);
    expect(availabilityAsked(fetch)[0].people).toEqual([ME.userId, KAY.userId, BEN.userId, CAL.userId, DEV.userId].sort());

    await user.click(dialog().getByRole('button', {name: 'Soonest'}));
    expect(await dialog().findByRole('button', {name: 'Tue 17 Nov, 10:00 to 11:00'})).toBeInTheDocument();
  });

  it("starts at the team view's time, says who's busy then, and shows the server's clash and field messages", async () => {
    let answer = json({title: 'k.ashford has Lunch, 10:00 to 11:00.'}, 409);
    const {user, fetch, onClose} = renderDialog(
      {initial: {attendeeUserIds: [KAY.userId], ...at('2026-11-18', '10:30', '11:15')}},
      {
        availability: () => ({
          suggestions: SUGGESTIONS,
          busy: [{staffUserId: KAY.userId, username: 'k.ashford', ...at('2026-11-18', '10:00', '11:00'), what: 'Lunch'}],
        }),
        handlers: {'POST /api/calendar/meetings': () => answer},
      },
    );

    expect(await screen.findByRole('heading', {name: 'New Meeting'})).toBeInTheDocument();
    // Its own length, offered beside the usual ones; the organiser wasn't among the people given, so isn't going.
    expect(dialog().getByRole('combobox', {name: /Length/})).toHaveTextContent('45 minutes');
    await waitFor(() => expect(dialog().getByRole('checkbox', {name: /I'm going too/})).not.toBeChecked());
    expect(dialog().getByRole('combobox', {name: /Day/})).toHaveValue('Wed, 18 Nov 2026');
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('10:30');
    expect(await dialog().findByText('Not everyone is free then')).toBeInTheDocument();
    expect(dialog().getByText('k.ashford has Lunch, 10:00 to 11:00')).toBeInTheDocument();

    // Saving anyway: the server's refusal shows in its words, and the dialog stays open.
    await user.type(dialog().getByRole('textbox', {name: /Title/}), 'Huddle');
    await user.click(dialog().getByRole('button', {name: 'Add Meeting'}));
    expect(await dialog().findByText('k.ashford has Lunch, 10:00 to 11:00.')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
    // It looks again at who's free.
    await waitFor(() => expect(availabilityAsked(fetch).length).toBeGreaterThan(1));

    answer = json(
      {title: 'One or more validation errors occurred.', errors: {Title: ['Title must not exceed 100 characters.'], StartUtc: ['Not in the working day.'], Other: ['Something else.']}},
      400,
    );
    await user.click(dialog().getByRole('button', {name: 'Add Meeting'}));
    expect(await dialog().findByText('Title must not exceed 100 characters.')).toBeInTheDocument();
    expect(dialog().getByText('Not in the working day.')).toBeInTheDocument();
    expect(dialog().getByText('Something else.')).toBeInTheDocument();

    answer = json({title: 'Server error'}, 500);
    await user.click(dialog().getByRole('button', {name: 'Add Meeting'}));
    expect(await dialog().findByText('The meeting could not be saved. Please try again.')).toBeInTheDocument();
  });

  it("looks at a day after the suggestions on its own, starts at the first time everyone's free, and won't end after 18:00", async () => {
    const {user, fetch} = renderDialog(
      {},
      {
        availability: (q) =>
          q.get('From') === '2026-12-07'
            ? {days: [{date: '2026-12-07', free: [at('2026-12-07', '08:00', '09:00'), at('2026-12-07', '16:55', '18:00')]}], busy: []}
            : {suggestions: SUGGESTIONS},
      },
    );

    const day = await dialog().findByRole('combobox', {name: /Day/});
    expect(isDisabled(dialog().getByRole('combobox', {name: /Start/}))).toBe(true);
    await user.type(day, '7 December 2026{Enter}');
    await waitFor(() => expect(availabilityAsked(fetch).some((a) => a.from === '2026-12-07' && a.to === '2026-12-07')).toBe(true));
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('08:00'));
    expect(dialog().getByText('Monday 7 December, 08:00 to 09:00, 1 person')).toBeInTheDocument();
    expect(dialog().getByText('Everyone is free then.')).toBeInTheDocument();

    // Two hours fits only in the afternoon's free time, from 17:00 on the 5-minute step... which ends after 18:00.
    await user.click(dialog().getByRole('combobox', {name: /Length/}));
    await user.click(screen.getByRole('option', {name: '2 hours'}));
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('Choose a time'));

    await user.click(dialog().getByRole('combobox', {name: /Start/}));
    await user.click(screen.getByRole('option', {name: '16:00'}));
    expect(dialog().getByText('Monday 7 December, 16:00 to 18:00, 1 person')).toBeInTheDocument();
    await user.click(dialog().getByRole('combobox', {name: /Length/}));
    await user.click(screen.getByRole('option', {name: '1 hour'}));
    await user.click(dialog().getByRole('combobox', {name: /Start/}));
    await user.click(screen.getByRole('option', {name: '17:00'}));
    await user.click(dialog().getByRole('combobox', {name: /Length/}));
    await user.click(screen.getByRole('option', {name: '1 hour 30 minutes'}));
    expect(dialog().getByText('It would end after 18:00.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Meeting'}))).toBe(true);
  });

  it("says so when the staff can't be loaded", async () => {
    renderDialog({}, {handlers: {'GET /api/calendar/staff': () => json({title: 'Server error'}, 500)}});
    expect(await dialog().findByText('The staff could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("says so when the suggestions or who's free can't be loaded", async () => {
    const {user} = renderDialog(
      {initial: {...at('2026-11-18', '10:00', '11:00')}},
      {availability: () => json({title: 'Server error'}, 500)},
    );

    expect(await dialog().findByText('The suggested times could not be loaded. Please try again.')).toBeInTheDocument();
    expect(dialog().getByText("Who's free then could not be checked. Saving still checks it.")).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Later suggestions'}))).toBe(true);
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
  });

  it('changes a meeting from its details, leaving it out of its people’s time, and goes back', async () => {
    withAnnouncements();
    const saved = {...MEETING, title: 'Team huddle (moved)'};
    const {user, fetch, onSaved} = renderDialog({meeting: MEETING}, {handlers: {[`PUT /api/calendar/meetings/${MEETING.id}`]: () => json(saved)}});

    await user.click(await dialog().findByRole('button', {name: 'Change'}));
    expect(await screen.findByRole('heading', {name: 'Change Meeting'})).toBeInTheDocument();
    expect(dialog().getByRole('textbox', {name: /Title/})).toHaveValue('Team huddle');
    // Everyone sees the title, notifications included, so it warns against patient details, as Notes does.
    expect(dialog().getByRole('textbox', {name: /Title/})).toHaveAccessibleDescription(/Don't put patient details here\./);
    expect(dialog().getByRole('textbox', {name: /Notes/})).toHaveValue('Rota for December');
    expect(dialog().queryByRole('checkbox', {name: /I'm going too/})).not.toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Who/})).toHaveTextContent('k.ashford, Deleted user');
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('10:00');
    await waitFor(() => expect(availabilityAsked(fetch)[0]).toMatchObject({except: MEETING.id, people: [KAY.userId, GONE].sort()}));

    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(await screen.findByRole('heading', {name: 'Meeting'})).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Change'}));
    const title = await dialog().findByRole('textbox', {name: /Title/});
    await user.clear(title);
    await user.type(title, 'Team huddle (moved)');
    await user.click(await dialog().findByRole('button', {name: 'Thu 19 Nov, 09:00 to 10:00'}));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Meeting saved');
    expect(onSaved).toHaveBeenCalledWith(saved);
    expect(await bodyOf(fetch, 'PUT', `/api/calendar/meetings/${MEETING.id}`)).toEqual({
      title: 'Team huddle (moved)',
      notes: 'Rota for December',
      startUtc: '2026-11-19T09:00:00.000Z',
      endUtc: '2026-11-19T10:00:00.000Z',
      attendeeUserIds: [KAY.userId, GONE],
      // The version it was changed from: the API refuses the change if anyone has changed the meeting since.
      expectedLastUpdated: MEETING.lastUpdated,
    });
  });

  it('says a meeting on a day that has since closed must move, and asks only for the closed days still to come', async () => {
    const {user, fetch} = renderDialog(
      {meeting: MEETING},
      {handlers: {'GET /api/calendar/closed-days': () => json([{id: 'c1', date: MEETING.date, name: 'Staff training day', isPast: false}])}},
    );

    await user.click(await dialog().findByRole('button', {name: 'Change'}));

    expect(await dialog().findByText('The clinic is closed then: Staff training day. Choose another day.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Save'}))).toBe(true);
    expect(dialog().queryByText('Everyone is free then.')).not.toBeInTheDocument();
    expect(requests(fetch, 'GET', '/api/calendar/closed-days').map((r) => new URL(r.url).search)).toContain('?From=2026-11-16');
  });

  it('cancels a meeting once confirmed, and shows why when it cannot', async () => {
    withAnnouncements();
    let answer = json({title: 'This meeting has already been cancelled.'}, 404);
    const {user, fetch, onSaved, onClose} = renderDialog(
      {meeting: {...MEETING, notes: null}},
      {handlers: {[`DELETE /api/calendar/meetings/${MEETING.id}`]: () => answer}},
    );

    await user.click(await dialog().findByRole('button', {name: 'Cancel Meeting'}));
    expect(await dialog().findByText("Cancel 'Team huddle', Tuesday 24 November, 10:00 to 11:00? Its attendees are told it's cancelled.")).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Keep It'}));
    expect(await screen.findByRole('heading', {name: 'Meeting'})).toBeInTheDocument();
    expect(requests(fetch, 'DELETE', `/api/calendar/meetings/${MEETING.id}`)).toHaveLength(0);

    await user.click(dialog().getByRole('button', {name: 'Cancel Meeting'}));
    await user.click(await dialog().findByRole('button', {name: 'Cancel Meeting'}));
    expect(await dialog().findByText('This meeting has already been cancelled.')).toBeInTheDocument();
    expect(onSaved).not.toHaveBeenCalled();

    answer = new Response(null, {status: 204});
    await user.click(dialog().getByRole('button', {name: 'Cancel Meeting'}));
    await user.click(await dialog().findByRole('button', {name: 'Cancel Meeting'}));
    await expectAnnounced('Meeting cancelled');
    expect(onSaved).toHaveBeenCalledWith(null);
    expect(onClose).toHaveBeenCalled();
  });

  it('saves with Ctrl+S, and lays out in one column in a half view', async () => {
    const {fetch} = renderDialog(
      {initial: {attendeeUserIds: [KAY.userId], ...at('2026-11-18', '10:00', '11:00')}},
      {isNarrow: true, handlers: {'POST /api/calendar/meetings': () => json(MEETING, 201)}},
    );

    const title = await dialog().findByRole('textbox', {name: /Title/});
    fireEvent.change(title, {target: {value: 'Huddle'}});
    await waitFor(() => expect(title).toHaveValue('Huddle'));
    title.focus();
    fireEvent.keyDown(title, {key: 's', ctrlKey: true});
    await waitFor(() => expect(requests(fetch, 'POST', '/api/calendar/meetings')).toHaveLength(1));
  });

  it('copes with a meeting missing its title and attendees, and Ctrl+S saves nothing until it can', async () => {
    const {user, fetch} = renderDialog({meeting: {...MEETING, title: null, attendees: null, notes: null}});

    expect(await dialog().findByText('Attendees (0)')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel Meeting'}));
    expect(await dialog().findByText(/^Cancel '', Tuesday 24 November/)).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Keep It'}));
    await user.click(await dialog().findByRole('button', {name: 'Change'}));

    const title = await dialog().findByRole('textbox', {name: /Title/});
    expect(title).toHaveValue('');
    expect(dialog().getByText('Choose who’s going to see when they’re all free.')).toBeInTheDocument();
    title.focus();
    fireEvent.keyDown(title, {key: 's', ctrlKey: true});
    expect(requests(fetch, 'PUT', `/api/calendar/meetings/${MEETING.id}`)).toHaveLength(0);
  });

  it('starts afresh each time it opens', async () => {
    mockApi({'GET /api/calendar/staff': () => json(STAFF), 'GET /api/calendar/availability': () => json({days: [], suggestions: [], busy: []})});
    function Harness() {
      const [isOpen, setIsOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsOpen(true)}>
            Open
          </button>
          <MeetingDialog isOpen={isOpen} onClose={() => setIsOpen(false)} />
        </>
      );
    }
    const {user} = renderWithApp(<Harness />, {user: adminUser});

    await user.type(await dialog().findByRole('textbox', {name: /Title/}), 'Draft');
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('button', {name: 'Open'}));
    expect(await dialog().findByRole('textbox', {name: /Title/})).toHaveValue('');
  });
});

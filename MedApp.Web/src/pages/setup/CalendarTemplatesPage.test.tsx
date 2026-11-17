import {screen, waitFor, within} from '@testing-library/react';
import {
  adminUser,
  calls,
  expectAnnounced,
  findAlert,
  isDisabled,
  json,
  mockApi,
  openDialog,
  pickFavourite,
  renderWithApp,
  searchBar,
  staffUser,
  withAnnouncements,
} from '../../test/render';
import {calendarKeys} from '../../api/calendar';
import {calendarTemplatesPageQueries, CalendarTemplatesPage} from './CalendarTemplatesPage';
import type {CalendarTemplatesSearch} from './calendarTemplatesSearch';

const ADMIN = {userId: '00000000-0000-0000-0000-0000000000a9', username: 'test-admin'};
const ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-user'};
const OTHER = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'b.staff'};

const STAFF = '/api/calendar/staff';
const DAYS = '/api/calendar/day-templates';
const WEEKS = '/api/calendar/week-templates';
const daysOf = (userId: string) => `${STAFF}/${userId}/day-templates`;
const weeksOf = (userId: string) => `${STAFF}/${userId}/week-templates`;
const defaultOf = (userId: string) => `${STAFF}/${userId}/week-template`;

const dayTemplates = [
  {
    id: 'dt1',
    name: 'Standard day',
    isArchived: false,
    weekTemplateCount: 2,
    canBeDeleted: false,
    canEdit: true,
    blocks: [{id: 'b1', activityTypeId: 'a1', activityName: 'Lunch', colour: 'green', startTime: '12:00:00', endTime: '13:00:00'}],
  },
  {id: 'dt2', name: 'Empty day', isArchived: false, weekTemplateCount: 0, canBeDeleted: true, canEdit: true, blocks: []},
  {id: 'dt3', name: 'Old day', isArchived: true, weekTemplateCount: 1, canBeDeleted: false, canEdit: true, blocks: []},
];

const weekDays = (id: string | null, name: string | null) =>
  ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({dayOfWeek, dayTemplateId: id, dayTemplateName: name}));

const weekTemplates = [
  {id: 'wt1', name: 'Full time', isArchived: false, staffCount: 1, canBeDeleted: false, canEdit: true, days: weekDays('dt1', 'Standard day')},
  {id: 'wt2', name: 'Part time', isArchived: false, staffCount: 0, canBeDeleted: true, canEdit: true, days: weekDays(null, null)},
  {id: 'wt3', name: 'Old week', isArchived: true, staffCount: 0, canBeDeleted: false, canEdit: true, days: weekDays(null, null)},
];

const readOnly = <T extends object>(items: T[]) => items.map((t) => ({...t, canEdit: false}));

function renderPage(
  search: CalendarTemplatesSearch = {},
  extra: Parameters<typeof mockApi>[0] = {},
  {user = adminUser, owner = ADMIN.userId} = {},
) {
  const fetch = mockApi({
    [`GET ${STAFF}`]: () => json([ADMIN, ME, OTHER]),
    [`GET ${daysOf(owner)}`]: () => json(dayTemplates),
    [`GET ${weeksOf(owner)}`]: () => json(weekTemplates),
    [`GET ${defaultOf(owner)}`]: () => json({staffUserId: owner, weekTemplateId: 'wt1', weekTemplateName: 'Full time', since: '2026-11-02'}),
    ...extra,
  });
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<CalendarTemplatesPage search={search} onSearchChange={onSearchChange} />, {user})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!;

describe('CalendarTemplatesPage', () => {
  it('starts on your own day templates: their blocks, what uses them, and a New button', async () => {
    const {fetch} = renderPage();

    const standard = within(await rowOf('Standard day'));
    expect(screen.getByRole('heading', {name: 'My calendar templates'})).toBeInTheDocument();
    expect(standard.getByText('Lunch, 12:00–13:00')).toBeInTheDocument();
    expect(standard.getByText('2 week templates')).toBeInTheDocument();
    expect(within(await rowOf('Empty day')).getByText('No blocks')).toBeInTheDocument();
    expect(within(await rowOf('Empty day')).getByText('No week templates')).toBeInTheDocument();
    expect(screen.queryByText('Old day')).not.toBeInTheDocument();
    expect(screen.getByText('2 active day templates')).toBeInTheDocument();
    expect(screen.getByRole('tab', {name: 'Day Templates'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel', {name: 'Day Templates'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Day Template'})).toHaveAttribute('href', '/calendar/templates/day/new');
    expect(standard.getByRole('link', {name: 'Edit day template'})).toHaveAttribute('href', '/calendar/templates/day/dt1');
    expect(calls(fetch)).toContain(`GET ${daysOf(ADMIN.userId)}?includeArchived=true`);
  });

  it('keeps the tab and whose templates in the URL, each tab starting on its own search', async () => {
    const {user, onSearchChange} = renderPage({name: 'standard', staff: OTHER.userId}, {}, {owner: OTHER.userId});

    await screen.findByText('Standard day');
    await user.click(screen.getByRole('tab', {name: 'Week Templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({tab: 'week', staff: OTHER.userId});
    await user.click(screen.getByRole('tab', {name: 'Day Templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({staff: OTHER.userId});
  });

  it('lets an admin pick whose templates, from themselves first then everyone else', async () => {
    const {user, onSearchChange} = renderPage({tab: 'week', name: 'full'});

    const picker = await screen.findByRole('combobox', {name: 'Whose calendar templates'});
    await waitFor(() => expect(picker).toHaveTextContent('My templates'));
    // It never shows an error, so it keeps no line for one, and lines up with the New button beside it.
    expect(picker.closest<HTMLElement>('[style*="--medapp-field-status-line"]')?.style.getPropertyValue('--medapp-field-status-line')).toBe('0px');
    await user.click(picker);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['My templates', 'b.staff', 'test-user']);
    await user.click(screen.getByRole('option', {name: 'b.staff'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({tab: 'week', staff: OTHER.userId});
  });

  it("shows an admin someone else's templates to change, keeping whose in every link", async () => {
    const {user, onSearchChange} = renderPage({staff: OTHER.userId}, {}, {owner: OTHER.userId});

    const standard = within(await rowOf('Standard day'));
    expect(screen.getByRole('heading', {name: "b.staff's calendar templates"})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Day Template'})).toHaveAttribute('href', `/calendar/templates/day/new?staff=${OTHER.userId}`);
    expect(standard.getByRole('link', {name: 'Edit day template'})).toHaveAttribute('href', `/calendar/templates/day/dt1?staff=${OTHER.userId}`);

    await user.click(screen.getByRole('combobox', {name: 'Whose calendar templates'}));
    await user.click(screen.getByRole('option', {name: 'My templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({tab: undefined, staff: undefined});
  });

  it("shows staff someone else's templates read-only, without the picker", async () => {
    renderPage({staff: OTHER.userId}, {[`GET ${daysOf(OTHER.userId)}`]: () => json(readOnly(dayTemplates))}, {user: staffUser, owner: OTHER.userId});

    const standard = within(await rowOf('Standard day'));
    expect(screen.getByRole('heading', {name: "b.staff's calendar templates"})).toBeInTheDocument();
    expect(standard.getByRole('link', {name: 'View day template'})).toHaveAttribute('href', `/calendar/templates/day/dt1?staff=${OTHER.userId}`);
    expect(standard.queryByRole('button', {name: /Archive|Delete/})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'New Day Template'})).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', {name: 'Whose calendar templates'})).not.toBeInTheDocument();
  });

  it('gives staff their own templates to change, without the picker', async () => {
    renderPage({}, {}, {user: staffUser, owner: ME.userId});

    expect(await screen.findByRole('link', {name: 'New Day Template'})).toHaveAttribute('href', '/calendar/templates/day/new');
    expect(screen.getByRole('heading', {name: 'My calendar templates'})).toBeInTheDocument();
    expect(screen.queryByRole('combobox', {name: 'Whose calendar templates'})).not.toBeInTheDocument();
  });

  it("says so when the staff list can't be loaded, as it says whose your templates are", async () => {
    renderPage({}, {[`GET ${STAFF}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert("The staff list could not be loaded, so the calendar templates can't be found. Please try again.")).toBeInTheDocument();
  });

  it("names the page of someone who isn't in the staff list plainly", async () => {
    const stranger = '00000000-0000-0000-0000-0000000000c3';
    renderPage({staff: stranger}, {}, {owner: stranger});
    expect(await screen.findByRole('heading', {name: 'Calendar templates'})).toBeInTheDocument();
  });

  it('archives, restores and deletes day templates, saying why one failed', async () => {
    const {user, fetch} = renderPage(
      {status: 'any'},
      {
        [`POST ${DAYS}/dt1/archive`]: () => json({}),
        [`POST ${DAYS}/dt3/restore`]: () => json({title: 'Down'}, 500),
        [`DELETE ${DAYS}/dt2`]: () => new Response(null, {status: 204}),
      },
    );

    await user.click(within(await rowOf('Standard day')).getByRole('button', {name: 'Archive day template'}));
    const archive = within(openDialog()!);
    expect(archive.getByText('1 block')).toBeInTheDocument();
    await user.click(archive.getByRole('button', {name: 'Archive'}));
    await waitFor(() => expect(calls(fetch)).toContain(`POST ${DAYS}/dt1/archive`));

    expect(isDisabled(within(await rowOf('Old day')).getByRole('button', {name: /can be archived but not deleted/}))).toBe(true);
    await user.click(within(await rowOf('Empty day')).getByRole('button', {name: 'Delete day template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Day Template'}));
    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${DAYS}/dt2`));

    await user.click(within(await rowOf('Old day')).getByRole('button', {name: 'Restore day template'}));
    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('says why an archive failed, and closes its dialog', async () => {
    const {user} = renderPage({}, {[`POST ${DAYS}/dt1/archive`]: () => json({title: 'Someone else changed it.'}, 409)});

    await user.click(within(await rowOf('Standard day')).getByRole('button', {name: 'Archive day template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert('Someone else changed it.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('lists the week templates: their days, and which is the default week', async () => {
    const {user, fetch} = renderPage(
      {tab: 'week'},
      {[`POST ${WEEKS}/wt1/archive`]: () => json({}), [`DELETE ${WEEKS}/wt2`]: () => new Response(null, {status: 204})},
    );

    const full = within(await rowOf('Full time'));
    expect(full.getByText('Mon to Fri: Standard day')).toBeInTheDocument();
    expect(full.getByText('Yes')).toBeInTheDocument();
    expect(within(await rowOf('Part time')).getByText('No')).toBeInTheDocument();
    expect(screen.getByText('2 active week templates')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Week Template'})).toHaveAttribute('href', '/calendar/templates/week/new');
    expect(full.getByRole('link', {name: 'Edit week template'})).toHaveAttribute('href', '/calendar/templates/week/wt1');
    expect(isDisabled(full.getByRole('button', {name: /has been the default week/}))).toBe(true);

    await user.click(full.getByRole('button', {name: 'Archive week template'}));
    expect(within(openDialog()!).getByText('Mon to Fri: Standard day', {selector: 'dd *, dd'})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));
    await waitFor(() => expect(calls(fetch)).toContain(`POST ${WEEKS}/wt1/archive`));

    await user.click(within(await rowOf('Part time')).getByRole('button', {name: 'Delete week template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Week Template'}));
    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${WEEKS}/wt2`));
  });

  it('restores an archived week template', async () => {
    const {user, fetch} = renderPage({tab: 'week', status: 'archived'}, {[`POST ${WEEKS}/wt3/restore`]: () => json({})});

    await user.click(within(await rowOf('Old week')).getByRole('button', {name: 'Restore week template'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${WEEKS}/wt3/restore`));
  });

  it('changes the default week from their own week templates, and announces it', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(
      {tab: 'default'},
      {[`PUT ${defaultOf(ADMIN.userId)}`]: () => json({staffUserId: ADMIN.userId, weekTemplateId: 'wt2'})},
    );

    const field = await screen.findByRole('combobox', {name: /^Default week/});
    await waitFor(() => expect(field).toHaveTextContent('Full time'));
    expect(screen.getByText('Default week: Full time')).toBeInTheDocument();
    expect(screen.getByText('The default week since 2 November 2026.')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /New/})).not.toBeInTheDocument();

    await user.click(field);
    expect(screen.queryByRole('option', {name: /Old week/})).not.toBeInTheDocument();
    // The same choice changes nothing.
    await user.click(screen.getByRole('option', {name: 'Full time'}));
    expect(sent(fetch, 'PUT')).toBeUndefined();

    await user.click(field);
    await user.click(screen.getByRole('option', {name: 'Part time'}));
    await waitFor(() => expect(sent(fetch, 'PUT')).toBeDefined());
    expect(await sent(fetch, 'PUT').json()).toEqual({weekTemplateId: 'wt2'});
    await expectAnnounced('Default week saved');
  });

  it('removes the default week, showing an archived one while it is theirs, and says why a change failed', async () => {
    withAnnouncements();
    let fail = false;
    const {user, fetch} = renderPage(
      {tab: 'default'},
      {
        [`GET ${defaultOf(ADMIN.userId)}`]: () => json({staffUserId: ADMIN.userId, weekTemplateId: 'wt3', weekTemplateName: 'Old week', since: null}),
        [`PUT ${defaultOf(ADMIN.userId)}`]: () => (fail ? json({title: 'Down'}, 500) : json({staffUserId: ADMIN.userId, weekTemplateId: null})),
      },
    );

    const field = await screen.findByRole('combobox', {name: /^Default week/});
    await waitFor(() => expect(field).toHaveTextContent('Old week (archived)'));
    expect(screen.queryByText(/The default week since/)).not.toBeInTheDocument();
    await user.click(field);
    await user.click(screen.getByRole('option', {name: 'No default week'}));
    await expectAnnounced('Default week removed');
    expect(await sent(fetch, 'PUT').json()).toEqual({weekTemplateId: null});

    fail = true;
    await waitFor(() => expect(isDisabled(field)).toBe(false));
    await user.click(field);
    await user.click(screen.getByRole('option', {name: 'Part time'}));
    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it("shows staff someone else's default week without letting them change it", async () => {
    renderPage({tab: 'default', staff: OTHER.userId}, {}, {user: staffUser, owner: OTHER.userId});

    const field = await screen.findByRole('combobox', {name: /^Default week/});
    await waitFor(() => expect(field).toHaveTextContent('Full time'));
    expect(isDisabled(field)).toBe(true);
    expect(screen.getByText('Only b.staff or an admin can change it.')).toBeInTheDocument();
  });

  it('says when the default week could not be loaded', async () => {
    renderPage({tab: 'default'}, {[`GET ${defaultOf(ADMIN.userId)}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The default week could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('offers to create the first day template', async () => {
    renderPage({}, {[`GET ${daysOf(ADMIN.userId)}`]: () => json([])});
    expect(await screen.findByRole('link', {name: 'Create Your First Day Template'})).toHaveAttribute('href', '/calendar/templates/day/new');
  });

  it('offers to create the first week template', async () => {
    renderPage({tab: 'week'}, {[`GET ${weeksOf(ADMIN.userId)}`]: () => json([])});
    expect(await screen.findByRole('link', {name: 'Create Your First Week Template'})).toHaveAttribute('href', '/calendar/templates/week/new');
  });

  it("offers an admin to create someone else's first template", async () => {
    renderPage({staff: OTHER.userId}, {[`GET ${daysOf(OTHER.userId)}`]: () => json([])}, {owner: OTHER.userId});
    expect(await screen.findByRole('link', {name: 'Create a Day Template'})).toHaveAttribute('href', `/calendar/templates/day/new?staff=${OTHER.userId}`);
  });

  it("offers staff nothing to create on someone else's empty list", async () => {
    renderPage({tab: 'week', staff: OTHER.userId}, {[`GET ${weeksOf(OTHER.userId)}`]: () => json([])}, {user: staffUser, owner: OTHER.userId});
    expect(await screen.findByText('No week templates yet')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /Create/})).not.toBeInTheDocument();
  });

  it('offers the archived ones when the search matches no active one', async () => {
    const {user, onSearchChange} = renderPage({name: 'old'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));

    expect(onSearchChange).toHaveBeenCalledWith({name: 'old', page: undefined, status: 'any', tab: undefined, staff: undefined});
  });

  it('says when a list could not be loaded', async () => {
    renderPage({}, {[`GET ${daysOf(ADMIN.userId)}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The day templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('says when the week templates could not be loaded', async () => {
    renderPage({tab: 'week'}, {[`GET ${weeksOf(ADMIN.userId)}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The week templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('filters each list from its favourites, and pages it, keeping the tab', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...weekTemplates[1], id: `w${i}`, name: `Week ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({tab: 'week'}, {[`GET ${weeksOf(ADMIN.userId)}`]: () => json(many)});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 2, tab: 'week'});
    await pickFavourite(user, 'Archived');
    expect(onSearchChange).toHaveBeenLastCalledWith({name: undefined, status: 'archived', page: undefined, tab: 'week'});
  });

  it('pages the day templates, and clears their search', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...dayTemplates[1], id: `d${i}`, name: `Day ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({name: 'day'}, {[`GET ${daysOf(ADMIN.userId)}`]: () => json(many)});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({name: 'day', page: 2});
    await user.click(within(searchBar()).getByRole('button', {name: 'Clear all'}));
    // Clearing the bar clears Active too, so archived ones show.
    expect(onSearchChange).toHaveBeenLastCalledWith({name: undefined, page: undefined, status: 'any'});
  });

  it('copes with day and week templates without their optional fields', async () => {
    renderPage({status: 'any'}, {[`GET ${daysOf(ADMIN.userId)}`]: () => json([{id: 'd9', blocks: [{id: 'b9'}]}, {id: 'd8', name: 'Unnamed blocks'}])});
    expect(await screen.findByText(', –')).toBeInTheDocument();
    expect(screen.getByText('No blocks')).toBeInTheDocument();

    renderPage({tab: 'week', status: 'any'}, {[`GET ${weeksOf(ADMIN.userId)}`]: () => json([{id: 'w9'}])});
    expect(await screen.findByText('Mon to Fri: No template')).toBeInTheDocument();
  });

  it('copes with a default week and week templates without their optional fields', async () => {
    const {user} = renderPage(
      {tab: 'default'},
      {
        [`GET ${weeksOf(ADMIN.userId)}`]: () => json([{id: 'w9'}, {id: 'w8', name: 'Archived week', isArchived: true}, {}]),
        [`GET ${defaultOf(ADMIN.userId)}`]: () => json({}),
      },
    );

    expect(await screen.findByText('Default week: None')).toBeInTheDocument();
    const field = screen.getByRole('combobox', {name: /^Default week/});
    await waitFor(() => expect(isDisabled(field)).toBe(false));
    await user.click(field);
    expect(screen.getAllByRole('option')).toHaveLength(3);
  });

  it('preloads what each tab reads', () => {
    const keys = (tab: Parameters<typeof calendarTemplatesPageQueries>[0]) => calendarTemplatesPageQueries(tab, OTHER.userId).map((q) => q.queryKey);
    expect(keys('day')).toEqual([calendarKeys.dayTemplatesOf(OTHER.userId)]);
    expect(keys('week')).toEqual([calendarKeys.weekTemplatesOf(OTHER.userId)]);
    expect(keys('default')).toEqual([calendarKeys.weekTemplateOf(OTHER.userId), calendarKeys.weekTemplatesOf(OTHER.userId)]);
  });
});

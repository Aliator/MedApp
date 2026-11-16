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
  withAnnouncements,
} from '../../test/render';
import type {CalendarTemplatesSearch} from '../listSearch';
import {calendarTemplatesHref, CalendarTemplatesPage} from './CalendarTemplatesPage';

const DAYS = '/api/calendar/day-templates';
const WEEKS = '/api/calendar/week-templates';
const STAFF = '/api/calendar/staff';

const dayTemplates = [
  {
    id: 'dt1',
    name: 'Standard day',
    isArchived: false,
    weekTemplateCount: 2,
    canBeDeleted: false,
    blocks: [{id: 'b1', activityTypeId: 'a1', activityName: 'Lunch', colour: 'green', startTime: '12:00:00', endTime: '13:00:00'}],
  },
  {id: 'dt2', name: 'Empty day', isArchived: false, weekTemplateCount: 0, canBeDeleted: true, blocks: []},
  {id: 'dt3', name: 'Old day', isArchived: true, weekTemplateCount: 1, canBeDeleted: false, blocks: []},
];

const weekDays = (id: string | null, name: string | null) =>
  ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({dayOfWeek, dayTemplateId: id, dayTemplateName: name}));

const weekTemplates = [
  {id: 'wt1', name: 'Full time', isArchived: false, staffCount: 3, canBeDeleted: false, days: weekDays('dt1', 'Standard day')},
  {id: 'wt2', name: 'Part time', isArchived: false, staffCount: 0, canBeDeleted: true, days: weekDays(null, null)},
  {id: 'wt3', name: 'Old week', isArchived: true, staffCount: 1, canBeDeleted: false, days: weekDays(null, null)},
];

const staff = [
  {userId: 'u1', username: 'test.doctor', weekTemplateId: 'wt1', weekTemplateName: 'Full time'},
  {userId: 'u2', username: 'test.nurse', weekTemplateId: null, weekTemplateName: null},
  {userId: 'u3', username: 'test.locum', weekTemplateId: 'wt3', weekTemplateName: 'Old week'},
];

function renderPage(search: CalendarTemplatesSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${DAYS}`]: () => json(dayTemplates),
    [`GET ${WEEKS}`]: () => json(weekTemplates),
    [`GET ${STAFF}`]: () => json(staff),
    ...extra,
  });
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<CalendarTemplatesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!;

describe('CalendarTemplatesPage', () => {
  it('starts on the day templates: their blocks, what uses them, and a New button', async () => {
    renderPage();

    const standard = within(await rowOf('Standard day'));
    expect(standard.getByText('Lunch, 12:00–13:00')).toBeInTheDocument();
    expect(standard.getByText('2 week templates')).toBeInTheDocument();
    expect(within(await rowOf('Empty day')).getByText('No blocks')).toBeInTheDocument();
    expect(within(await rowOf('Empty day')).getByText('No week templates')).toBeInTheDocument();
    expect(screen.queryByText('Old day')).not.toBeInTheDocument();
    expect(screen.getByText('2 active day templates')).toBeInTheDocument();
    expect(screen.getByRole('tab', {name: 'Day Templates'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel', {name: 'Day Templates'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Day Template'})).toHaveAttribute('href', '/admin/calendar-templates/day/new');
    expect(standard.getByRole('link', {name: 'Edit day template'})).toHaveAttribute('href', '/admin/calendar-templates/day/dt1');
  });

  it('keeps the tab in the URL, each starting on its own search', async () => {
    const {user, onSearchChange} = renderPage({name: 'standard'});

    await screen.findByText('Standard day');
    await user.click(screen.getByRole('tab', {name: 'Week Templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({tab: 'week'});
    await user.click(screen.getByRole('tab', {name: 'Day Templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({});
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

  it('lists the week templates: their days, and who has them as their default', async () => {
    const {user, fetch} = renderPage(
      {tab: 'week'},
      {[`POST ${WEEKS}/wt1/archive`]: () => json({}), [`DELETE ${WEEKS}/wt2`]: () => new Response(null, {status: 204})},
    );

    const full = within(await rowOf('Full time'));
    expect(full.getByText('Mon to Fri: Standard day')).toBeInTheDocument();
    expect(full.getByText('Default for 3 people')).toBeInTheDocument();
    expect(within(await rowOf('Part time')).getByText('No one')).toBeInTheDocument();
    expect(screen.getByText('2 active week templates')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Week Template'})).toHaveAttribute('href', '/admin/calendar-templates/week/new');
    expect(full.getByRole('link', {name: 'Edit week template'})).toHaveAttribute('href', '/admin/calendar-templates/week/wt1');

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

  it("changes a member of staff's default week in place, and announces it", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({tab: 'staff'}, {[`PUT ${STAFF}/u2/week-template`]: () => json({staffUserId: 'u2', weekTemplateId: 'wt2'})});

    expect(await screen.findByText('3 people')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /New/})).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', {name: 'Default week for test.doctor'})).toHaveTextContent('Full time');
    // An archived one stays theirs, labelled.
    expect(screen.getByRole('combobox', {name: 'Default week for test.locum'})).toHaveTextContent('Old week (archived)');

    await user.click(screen.getByRole('combobox', {name: 'Default week for test.nurse'}));
    expect(screen.queryByRole('option', {name: /Old week/})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Part time'}));

    await waitFor(() => expect(sent(fetch, 'PUT')).toBeDefined());
    expect(await sent(fetch, 'PUT').json()).toEqual({weekTemplateId: 'wt2'});
    await expectAnnounced('Default week saved for test.nurse');
  });

  it("removes someone's default week, does nothing for the same choice, and says why a change failed", async () => {
    const {user, fetch} = renderPage({tab: 'staff'}, {[`PUT ${STAFF}/u1/week-template`]: () => json({title: 'Down'}, 500)});

    await user.click(await screen.findByRole('combobox', {name: 'Default week for test.doctor'}));
    await user.click(screen.getByRole('option', {name: 'Full time'}));
    expect(sent(fetch, 'PUT')).toBeUndefined();

    await user.click(screen.getByRole('combobox', {name: 'Default week for test.doctor'}));
    await user.click(screen.getByRole('option', {name: 'No default week'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    expect(await sent(fetch, 'PUT').json()).toEqual({weekTemplateId: null});
  });

  it('searches the staff by username', async () => {
    const {user, onSearchChange} = renderPage({tab: 'staff', name: 'nurse'});

    expect(await screen.findByText('1 matching person')).toBeInTheDocument();
    expect(screen.getByText('test.nurse')).toBeInTheDocument();
    expect(screen.queryByText('test.doctor')).not.toBeInTheDocument();
    await user.click(screen.getByRole('tab', {name: 'Staff Defaults'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({tab: 'staff'});
  });

  it('offers to create the first day template', async () => {
    renderPage({}, {[`GET ${DAYS}`]: () => json([])});
    expect(await screen.findByRole('link', {name: 'Create Your First Day Template'})).toHaveAttribute('href', '/admin/calendar-templates/day/new');
  });

  it('offers to create the first week template', async () => {
    renderPage({tab: 'week'}, {[`GET ${WEEKS}`]: () => json([])});
    expect(await screen.findByRole('link', {name: 'Create Your First Week Template'})).toHaveAttribute('href', '/admin/calendar-templates/week/new');
  });

  it('offers the archived ones when the search matches no active one', async () => {
    const {user, onSearchChange} = renderPage({name: 'old'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));

    expect(onSearchChange).toHaveBeenCalledWith({name: 'old', page: undefined, status: 'any', tab: undefined});
  });

  it('says when a list could not be loaded', async () => {
    renderPage({}, {[`GET ${DAYS}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The day templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('says when the week templates could not be loaded', async () => {
    renderPage({tab: 'week'}, {[`GET ${WEEKS}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The week templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('says when the staff could not be loaded', async () => {
    renderPage({tab: 'staff'}, {[`GET ${STAFF}`]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The staff and their default weeks could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('filters each list from its favourites, and pages it, keeping the tab', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...weekTemplates[1], id: `w${i}`, name: `Week ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({tab: 'week'}, {[`GET ${WEEKS}`]: () => json(many)});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 2, tab: 'week'});
    await pickFavourite(user, 'Archived');
    expect(onSearchChange).toHaveBeenLastCalledWith({name: undefined, status: 'archived', page: undefined, tab: 'week'});
  });

  it('pages the day templates', async () => {
    const many = Array.from({length: 12}, (_, i) => ({...dayTemplates[1], id: `d${i}`, name: `Day ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({}, {[`GET ${DAYS}`]: () => json(many)});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 2, tab: undefined});
  });

  it('clears the staff search, and pages the staff', async () => {
    const many = Array.from({length: 12}, (_, i) => ({userId: `u${i}`, username: `test.staff${String(i).padStart(2, '0')}`, weekTemplateId: null}));
    const {user, onSearchChange} = renderPage({tab: 'staff', name: 'staff'}, {[`GET ${STAFF}`]: () => json(many)});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({name: 'staff', page: 2, tab: 'staff'});
    await user.click(within(searchBar()).getByRole('button', {name: 'Clear all'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({name: undefined, tab: 'staff'});
  });

  it('copes with templates and staff the API sends without their optional fields', async () => {
    const {user} = renderPage(
      {tab: 'staff'},
      {
        [`GET ${STAFF}`]: () => json([{userId: 'u9'}, {username: 'test.nobody'}]),
        [`GET ${WEEKS}`]: () => json([{id: 'w9'}, {id: 'w8', name: 'Archived week', isArchived: true}, {}]),
      },
    );

    expect(await screen.findByText('2 people')).toBeInTheDocument();
    await user.click(screen.getAllByRole('combobox', {name: /^Default week for/})[0]);
    expect(screen.getAllByRole('option')).toHaveLength(3);
  });

  it('copes with day and week templates without their optional fields', async () => {
    renderPage({status: 'any'}, {[`GET ${DAYS}`]: () => json([{id: 'd9', blocks: [{id: 'b9'}]}, {id: 'd8', name: 'Unnamed blocks'}])});
    expect(await screen.findByText(', –')).toBeInTheDocument();
    expect(screen.getByText('No blocks')).toBeInTheDocument();

    renderPage({tab: 'week', status: 'any'}, {[`GET ${WEEKS}`]: () => json([{id: 'w9'}])});
    expect(await screen.findByText('Mon to Fri: No template')).toBeInTheDocument();
  });

  it('links each editor back to its own tab', () => {
    expect(calendarTemplatesHref('day')).toBe('/admin/calendar-templates');
    expect(calendarTemplatesHref('week')).toBe('/admin/calendar-templates?tab=week');
  });
});

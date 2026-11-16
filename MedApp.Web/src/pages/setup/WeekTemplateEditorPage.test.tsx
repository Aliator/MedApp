import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, expectAnnounced, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {WeekTemplateEditorPage} from './WeekTemplateEditorPage';

const BASE = '/api/calendar';

const dayTemplates = [
  {
    id: 'dt1',
    name: 'Standard day',
    isArchived: false,
    blocks: [{id: 'b1', activityName: 'Lunch', startTime: '12:00:00', endTime: '13:00:00'}],
  },
  {id: 'dt2', name: 'Admin day', isArchived: false, blocks: []},
  {id: 'dt3', name: 'Old day', isArchived: true, blocks: []},
];

const weekDay = (dayOfWeek: string, dayTemplateId: string | null) => ({dayOfWeek, dayTemplateId});
const template = {
  id: 'wt1',
  name: 'Full time',
  isArchived: false,
  staffCount: 3,
  days: [weekDay('Monday', 'dt1'), weekDay('Tuesday', 'dt3'), weekDay('Wednesday', null), weekDay('Thursday', 'dt1'), weekDay('Friday', 'dt2')],
};

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/day-templates`]: () => json(dayTemplates),
    [`GET ${BASE}/week-templates/wt1`]: () => json(template),
    ...extra,
  });
  const path = id ? `/admin/calendar-templates/week/${id}` : '/admin/calendar-templates/week/new';
  return {fetch, ...renderWithApp(<WeekTemplateEditorPage id={id} />, {user: adminUser, path})};
}

const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method);
const day = (name: string) => screen.getByRole('combobox', {name});

describe('WeekTemplateEditorPage', () => {
  it('builds a new week: a day template for Monday, the same every day, and creates it without asking', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor(undefined, {[`POST ${BASE}/week-templates`]: () => json({id: 'wt9'}, 201)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Clinic week');
    expect(day('Monday')).toHaveTextContent('No template');
    expect(screen.getAllByText('No template blocks: free for appointments all day.')).toHaveLength(5);
    expect(screen.getByText('Saturday')).toBeInTheDocument();
    expect(screen.getAllByText('Not a working day: nothing can be booked or blocked.')).toHaveLength(2);
    expect(isDisabled(screen.getByRole('button', {name: 'Same Every Day'}))).toBe(true);

    await user.click(day('Monday'));
    // Archived ones aren't offered.
    expect(screen.queryByRole('option', {name: /Old day/})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Standard day'}));
    expect(screen.getByText('Lunch 12:00 to 13:00')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Same Every Day'}));
    await expectAnnounced('Standard day on every weekday');
    expect(day('Friday')).toHaveTextContent('Standard day');

    await user.click(day('Wednesday'));
    await user.click(screen.getByRole('option', {name: 'Admin day'}));
    expect(screen.getByText('No blocks: free for appointments all day.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Create Week Template'}));

    await waitFor(() => expect(router.state.location.href).toBe('/admin/calendar-templates?tab=week'));
    expect(openDialog()).toBeNull();
    expect(await sent(fetch, 'POST')!.json()).toEqual({
      name: 'Clinic week',
      monday: 'dt1',
      tuesday: 'dt1',
      wednesday: 'dt2',
      thursday: 'dt1',
      friday: 'dt1',
    });
    await expectAnnounced('Week template created');
  });

  it('starts from a template, keeps an archived day template, and confirms that changes apply from today', async () => {
    withAnnouncements();
    const {user, fetch} = renderEditor('wt1', {[`PUT ${BASE}/week-templates/wt1`]: () => json(template)});

    expect(await screen.findByRole('textbox', {name: /Name/})).toHaveValue('Full time');
    expect(day('Tuesday')).toHaveTextContent('Old day (archived)');
    expect(day('Wednesday')).toHaveTextContent('No template');

    await user.click(day('Wednesday'));
    await user.click(screen.getByRole('option', {name: 'Standard day'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/past days keep what they had\. It's the default week for 3 people/)).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(sent(fetch, 'PUT')).toBeDefined());
    expect(await sent(fetch, 'PUT')!.json()).toEqual({
      name: 'Full time',
      monday: 'dt1',
      tuesday: 'dt3',
      wednesday: 'dt1',
      thursday: 'dt1',
      friday: 'dt2',
    });
    await expectAnnounced('Week template saved');
  });

  it("shows the API's errors on the name, and closes the confirmation", async () => {
    const {user} = renderEditor('wt1', {
      [`PUT ${BASE}/week-templates/wt1`]: () => json({title: 'Validation failed', status: 400, errors: {Name: ['Name is already used.']}}, 400),
    });

    await screen.findByRole('textbox', {name: /Name/});
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save'}));

    expect((await screen.findAllByText('Name is already used.')).length).toBeGreaterThan(0);
    expect(openDialog()).toBeNull();
  });

  it('says why a save failed for another reason', async () => {
    const {user} = renderEditor(undefined, {[`POST ${BASE}/week-templates`]: () => json({title: 'Down'}, 500)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Week');
    await user.click(screen.getByRole('button', {name: 'Create Week Template'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderEditor(undefined, {[`GET ${BASE}/day-templates`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The week template could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('goes back to the list for an unknown one', async () => {
    const {router} = renderEditor('wt404', {[`GET ${BASE}/week-templates/wt404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/calendar-templates'));
  });

  it('copes with day templates without their optional fields', async () => {
    const {user} = renderEditor('wt1', {
      [`GET ${BASE}/day-templates`]: () => json([...dayTemplates, {id: 'dt9'}, {isArchived: false}]),
      [`GET ${BASE}/week-templates/wt1`]: () => json({...template, days: [weekDay('Monday', 'dt9'), weekDay('Tuesday', 'gone')]}),
    });

    expect(await screen.findByText('No blocks: free for appointments all day.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Same Every Day'}));
    expect(screen.getAllByText('No blocks: free for appointments all day.')).toHaveLength(5);
  });

  it('saves nothing when the confirmation is cancelled', async () => {
    const {user, fetch} = renderEditor('wt1');

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(sent(fetch, 'PUT')).toBeUndefined();
  });

  it('cancels back to the list', async () => {
    const {user, router} = renderEditor();

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.href).toBe('/admin/calendar-templates?tab=week'));
  });
});

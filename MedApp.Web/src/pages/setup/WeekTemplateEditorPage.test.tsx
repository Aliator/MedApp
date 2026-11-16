import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {WeekTemplateEditorPage} from './WeekTemplateEditorPage';

const BASE = '/api/calendar';
const ADMIN = {userId: '00000000-0000-0000-0000-0000000000a9', username: 'test-admin'};
const ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-user'};
const OTHER = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'b.staff'};
const daysOf = (userId: string) => `${BASE}/staff/${userId}/day-templates`;

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
  ownerUserId: ADMIN.userId,
  name: 'Full time',
  isArchived: false,
  staffCount: 1,
  canEdit: true,
  days: [weekDay('Monday', 'dt1'), weekDay('Tuesday', 'dt3'), weekDay('Wednesday', null), weekDay('Thursday', 'dt1'), weekDay('Friday', 'dt2')],
};

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}, {user = adminUser, staff}: {user?: typeof adminUser; staff?: string} = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/staff`]: () => json([ADMIN, ME, OTHER]),
    [`GET ${daysOf(ADMIN.userId)}`]: () => json(dayTemplates),
    [`GET ${BASE}/week-templates/wt1`]: () => json(template),
    ...extra,
  });
  const path = id ? `/calendar/templates/week/${id}` : '/calendar/templates/week/new';
  return {fetch, ...renderWithApp(<WeekTemplateEditorPage id={id} staff={staff} />, {user, path})};
}

const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method);
const day = (name: string) => screen.getByRole('combobox', {name});

describe('WeekTemplateEditorPage', () => {
  it('builds a new week: a day template for Monday, the same every day, and creates it without asking', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor(undefined, {[`POST ${BASE}/staff/${ADMIN.userId}/week-templates`]: () => json({id: 'wt9'}, 201)});

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

    await waitFor(() => expect(router.state.location.href).toBe('/calendar/templates?tab=week'));
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
    expect(dialog.getByText(/past days keep what they had\. It's your default week, so your calendar changes too/)).toBeInTheDocument();
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
    const {user} = renderEditor(undefined, {[`POST ${BASE}/staff/${ADMIN.userId}/week-templates`]: () => json({title: 'Down'}, 500)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Week');
    await user.click(screen.getByRole('button', {name: 'Create Week Template'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderEditor(undefined, {[`GET ${daysOf(ADMIN.userId)}`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The week template could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('goes back to the list for an unknown one', async () => {
    const {router} = renderEditor('wt404', {[`GET ${BASE}/week-templates/wt404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
  });

  it('copes with day templates without their optional fields', async () => {
    const {user} = renderEditor('wt1', {
      [`GET ${daysOf(ADMIN.userId)}`]: () => json([...dayTemplates, {id: 'dt9'}, {isArchived: false}]),
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

    await waitFor(() => expect(router.state.location.href).toBe('/calendar/templates?tab=week'));
  });

  it("builds one from someone else's day templates for an admin, and goes back to their list", async () => {
    const {user, fetch, router} = renderEditor(
      undefined,
      {
        [`GET ${daysOf(OTHER.userId)}`]: () => json([{id: 'dt7', name: 'Their day', isArchived: false, blocks: []}]),
        [`POST ${BASE}/staff/${OTHER.userId}/week-templates`]: () => json({id: 'wt9'}, 201),
      },
      {staff: OTHER.userId},
    );

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Their week');
    expect(screen.getByText("b.staff's calendar templates")).toBeInTheDocument();
    expect(screen.getByText("What's chosen as b.staff's default week.")).toBeInTheDocument();
    await user.click(day('Monday'));
    expect(screen.queryByRole('option', {name: 'Standard day'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Their day'}));
    await user.click(screen.getByRole('button', {name: 'Create Week Template'}));

    await waitFor(() => expect(router.state.location.href).toBe(`/calendar/templates?tab=week&staff=${OTHER.userId}`));
    expect(sent(fetch, 'POST')).toBeDefined();
    expect(calls(fetch)).not.toContain(`GET ${daysOf(ADMIN.userId)}?includeArchived=true`);
  });

  it("offers the day templates of the template's owner, and goes back to their list", async () => {
    const {user, fetch, router} = renderEditor('wt1', {
      [`GET ${BASE}/week-templates/wt1`]: () => json({...template, ownerUserId: OTHER.userId}),
      [`GET ${daysOf(OTHER.userId)}`]: () => json(dayTemplates),
    });

    expect(await screen.findByText("Full time · b.staff's calendar templates")).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${daysOf(OTHER.userId)}?includeArchived=true`);
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.href).toBe(`/calendar/templates?tab=week&staff=${OTHER.userId}`));
  });

  it("shows staff someone else's template read-only", async () => {
    renderEditor(
      'wt1',
      {
        [`GET ${BASE}/week-templates/wt1`]: () => json({...template, ownerUserId: OTHER.userId, canEdit: false}),
        [`GET ${daysOf(OTHER.userId)}`]: () => json(dayTemplates),
      },
      {user: staffUser},
    );

    expect(await screen.findByRole('heading', {name: 'Week Template'})).toBeInTheDocument();
    expect(screen.getByText('Only b.staff or an admin can change this week template.')).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('textbox', {name: /Name/}))).toBe(true);
    expect(isDisabled(day('Monday'))).toBe(true);
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Same Every Day'})).not.toBeInTheDocument();
  });

  it("names the owner plainly when they aren't in the staff list", async () => {
    renderEditor(
      'wt1',
      {
        [`GET ${BASE}/week-templates/wt1`]: () => json({...template, ownerUserId: '00000000-0000-0000-0000-0000000000c3', canEdit: false}),
        [`GET ${daysOf('00000000-0000-0000-0000-0000000000c3')}`]: () => json(dayTemplates),
      },
      {user: staffUser},
    );

    expect(await screen.findByText('Only its owner or an admin can change this week template.')).toBeInTheDocument();
    expect(screen.getByText("What's chosen as their default week.")).toBeInTheDocument();
  });

  it("doesn't let staff add to someone else's templates", async () => {
    renderEditor(undefined, {[`GET ${daysOf(OTHER.userId)}`]: () => json(dayTemplates)}, {user: staffUser, staff: OTHER.userId});

    expect(await screen.findByText("Only b.staff or an admin can add to b.staff's calendar templates.")).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: /Name/})).not.toBeInTheDocument();
  });

  it("says so when it can't tell whose a new one is", async () => {
    renderEditor(undefined, {[`GET ${BASE}/staff`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The week template could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, expectAnnounced, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {DayTemplateEditorPage} from './DayTemplateEditorPage';

const BASE = '/api/calendar';
const ADMIN = {userId: '00000000-0000-0000-0000-0000000000a9', username: 'test-admin'};
const ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-user'};
const OTHER = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'b.staff'};

const activities = [
  {id: 'a1', name: 'Lunch', colour: 'green', isArchived: false},
  {id: 'a2', name: 'Admin time', colour: 'blue', isArchived: false},
  {id: 'a3', name: 'Old meeting', colour: 'red', isArchived: true},
];

const template = {
  id: 'dt1',
  ownerUserId: ADMIN.userId,
  name: 'Standard day',
  isArchived: false,
  canEdit: true,
  weekTemplateCount: 2,
  blocks: [
    {id: 'b2', activityTypeId: 'a3', activityName: 'Old meeting', colour: 'red', startTime: '16:00:00', endTime: '17:00:00', note: 'Huddle'},
    {id: 'b1', activityTypeId: 'a1', activityName: 'Lunch', colour: 'green', startTime: '12:00:00', endTime: '13:00:00', note: null},
  ],
};

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}, {user = adminUser, staff}: {user?: typeof adminUser; staff?: string} = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/staff`]: () => json([ADMIN, ME, OTHER]),
    [`GET ${BASE}/activity-types`]: () => json(activities),
    [`GET ${BASE}/day-templates/dt1`]: () => json(template),
    ...extra,
  });
  const path = id ? `/calendar/templates/day/${id}` : '/calendar/templates/day/new';
  return {fetch, ...renderWithApp(<DayTemplateEditorPage id={id} staff={staff} />, {user, path})};
}

const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method);
const block = (n: number) => within(screen.getByRole('group', {name: new RegExp(`^Block ${n}:`)}));
const choose = async (user: ReturnType<typeof renderEditor>['user'], combobox: HTMLElement, option: string) => {
  await user.click(combobox);
  await user.click(screen.getByRole('option', {name: option}));
};

describe('DayTemplateEditorPage', () => {
  it('builds a new day template, previews the day, and creates it without asking', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor(undefined, {[`POST ${BASE}/staff/${ADMIN.userId}/day-templates`]: () => json({id: 'dt9'}, 201)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Clinic day');
    expect(screen.getByText('No blocks yet')).toBeInTheDocument();
    expect(within(screen.getByRole('list', {name: 'The day, from the template'})).getByText('08:00 to 18:00')).toBeInTheDocument();

    // The first block: the first active activity by name, 09:00 to 10:00, its activity focused.
    await user.click(screen.getByRole('button', {name: 'Add Block'}));
    expect(block(1).getByRole('combobox', {name: 'Activity'})).toHaveTextContent('Admin time');
    await waitFor(() => expect(block(1).getByRole('combobox', {name: 'Activity'})).toHaveFocus());
    await choose(user, block(1).getByRole('combobox', {name: 'Activity'}), 'Lunch');
    await choose(user, block(1).getByRole('combobox', {name: 'Start'}), '12:00');
    await choose(user, block(1).getByRole('combobox', {name: 'End'}), '13:00');
    await user.type(block(1).getByRole('textbox', {name: /Note/}), 'Canteen');

    // The next starts where the last ended.
    await user.click(screen.getByRole('button', {name: 'Add Block'}));
    expect(block(2).getByRole('combobox', {name: 'Start'})).toHaveTextContent('13:00');
    expect(block(2).getByRole('combobox', {name: 'End'})).toHaveTextContent('14:00');

    const day = within(screen.getByRole('list', {name: 'The day, from the template'}));
    expect(day.getAllByText(/ to /).map((t) => t.textContent)).toEqual(['08:00 to 12:00', '12:00 to 13:00', '13:00 to 14:00', '14:00 to 18:00']);
    expect(day.getByText('Canteen')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Create Day Template'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
    expect(openDialog()).toBeNull();
    expect(await sent(fetch, 'POST')!.json()).toEqual({
      name: 'Clinic day',
      blocks: [
        {activityTypeId: 'a1', startTime: '12:00:00', endTime: '13:00:00', note: 'Canteen'},
        {activityTypeId: 'a2', startTime: '13:00:00', endTime: '14:00:00', note: null},
      ],
    });
    await expectAnnounced('Day template created');
  });

  it('keeps blocks in start order, flags overlaps and backwards times, and saves nothing until they are fixed', async () => {
    const {user, fetch} = renderEditor();

    await screen.findByRole('textbox', {name: /Name/});
    await user.click(screen.getByRole('button', {name: 'Add Block'}));
    await user.click(screen.getByRole('button', {name: 'Add Block'}));
    // Block 2 (10:00 to 11:00) moved to 08:00: it comes first, its start keeping the focus.
    await choose(user, block(2).getByRole('combobox', {name: 'Start'}), '08:00');
    expect(block(1).getByRole('combobox', {name: 'Start'})).toHaveTextContent('08:00');
    await waitFor(() => expect(block(1).getByRole('combobox', {name: 'Start'})).toHaveFocus());

    // Now 08:00 to 11:00 overlaps the 09:00 to 10:00 block.
    expect(block(2).getAllByText('Overlaps Admin time, 08:00 to 11:00.').length).toBeGreaterThan(0);
    await choose(user, block(1).getByRole('combobox', {name: 'End'}), '08:15');
    expect(block(1).queryByText(/^Overlaps/)).not.toBeInTheDocument();
    expect(block(2).queryByText(/^Overlaps/)).not.toBeInTheDocument();
    // Ending before it starts is flagged on its end.
    await choose(user, block(2).getByRole('combobox', {name: 'End'}), '08:30');
    expect(block(2).getAllByText('Ends before it starts.').length).toBeGreaterThan(0);

    await user.type(screen.getByRole('textbox', {name: /Name/}), 'Broken day');
    await user.click(screen.getByRole('button', {name: 'Create Day Template'}));
    expect(await findAlert('Some blocks need changing first: see the messages beside them.')).toBeInTheDocument();
    expect(sent(fetch, 'POST')).toBeUndefined();
  });

  it('starts from a template, keeps an archived activity, and confirms that changes apply from today', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor('dt1', {[`PUT ${BASE}/day-templates/dt1`]: () => json(template)});

    expect(await screen.findByRole('textbox', {name: /Name/})).toHaveValue('Standard day');
    expect(block(1).getByRole('combobox', {name: 'Activity'})).toHaveTextContent('Lunch');
    expect(block(2).getByRole('combobox', {name: 'Activity'})).toHaveTextContent('Old meeting (archived)');
    expect(block(2).getByRole('textbox', {name: /Note/})).toHaveValue('Huddle');

    await user.click(screen.getByRole('button', {name: 'Remove Lunch, 12:00 to 13:00'}));
    await expectAnnounced('Lunch block removed');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/Changes apply from today; past days keep what they had\. It's used in 2 week templates/)).toBeInTheDocument();
    expect(dialog.getByText('Old meeting 16:00 to 17:00')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
    expect(await sent(fetch, 'PUT')!.json()).toEqual({
      name: 'Standard day',
      blocks: [{activityTypeId: 'a3', startTime: '16:00:00', endTime: '17:00:00', note: 'Huddle'}],
    });
    await expectAnnounced('Day template saved');
  });

  it("shows the API's errors: the name's on its field, the blocks' in the header", async () => {
    const {user} = renderEditor('dt1', {
      [`PUT ${BASE}/day-templates/dt1`]: () =>
        json({title: 'Validation failed', status: 400, errors: {Name: ['Name is already used.'], Blocks: ['Blocks overlap.']}}, 400),
    });

    await screen.findByRole('textbox', {name: /Name/});
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save'}));

    expect((await screen.findAllByText('Name is already used.')).length).toBeGreaterThan(0);
    expect(screen.getByText('Blocks overlap.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('says why a save failed for another reason', async () => {
    const {user} = renderEditor(undefined, {[`POST ${BASE}/staff/${ADMIN.userId}/day-templates`]: () => json({title: 'Down'}, 500)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Day');
    await user.click(screen.getByRole('button', {name: 'Create Day Template'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('adds a block before the end of a full day, and says when there are no activities to block time for', async () => {
    const {user} = renderEditor('dt1', {
      [`GET ${BASE}/day-templates/dt1`]: () =>
        json({...template, blocks: [{id: 'b', activityTypeId: 'a1', activityName: 'Lunch', startTime: '17:00:00', endTime: '18:00:00'}]}),
    });

    await screen.findByRole('textbox', {name: /Name/});
    await user.click(screen.getByRole('button', {name: 'Add Block'}));
    expect(block(2).getByRole('combobox', {name: 'Start'})).toHaveTextContent('17:00');

    renderEditor(undefined, {[`GET ${BASE}/activity-types`]: () => json([activities[2]])});
    expect(await screen.findByText(/There are no active activity types/)).toBeInTheDocument();
  });

  it('puts the details, blocks and day in one card in a half view', async () => {
    mockApi({[`GET ${BASE}/activity-types`]: () => json(activities), [`GET ${BASE}/staff`]: () => json([ADMIN])});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <DayTemplateEditorPage />
      </HalfViewContext.Provider>,
      {user: adminUser, path: '/calendar/templates/day/new'},
    );

    expect(await screen.findByRole('heading', {name: 'The Day'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Blocks (0)'})).toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderEditor(undefined, {[`GET ${BASE}/activity-types`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The day template could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('goes back to the list for an unknown one', async () => {
    const {router} = renderEditor('dt404', {[`GET ${BASE}/day-templates/dt404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
  });

  it('keeps a time off the 15-minute steps, and copes with activity types and blocks without their optional fields', async () => {
    renderEditor('dt1', {
      [`GET ${BASE}/activity-types`]: () => json([...activities, {colour: 'gray'}, {id: 'a9'}]),
      [`GET ${BASE}/day-templates/dt1`]: () =>
        json({...template, blocks: [{id: 'b', activityTypeId: 'gone', startTime: '12:05:00', endTime: '12:50:00'}, {id: 'c', startTime: '14:00:00', endTime: '15:00:00'}]}),
    });

    expect(await screen.findByRole('group', {name: 'Block 1: an activity, 12:05 to 12:50'})).toBeInTheDocument();
    expect(block(1).getByRole('combobox', {name: 'Start'})).toHaveTextContent('12:05');
    expect(block(2).getByRole('combobox', {name: 'Activity'})).toBeInTheDocument();
  });

  it('saves nothing when the confirmation is cancelled', async () => {
    const {user, fetch} = renderEditor('dt1');

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(sent(fetch, 'PUT')).toBeUndefined();
  });

  it('goes back to the list from Cancel', async () => {
    const {user, router} = renderEditor();

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/calendar/templates'));
  });

  it("adds one to someone else's templates for an admin, and goes back to their list", async () => {
    const {user, fetch, router} = renderEditor(
      undefined,
      {[`POST ${BASE}/staff/${OTHER.userId}/day-templates`]: () => json({id: 'dt9'}, 201)},
      {staff: OTHER.userId},
    );

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Clinic day');
    expect(screen.getByText("b.staff's calendar templates")).toBeInTheDocument();
    expect(screen.getByText(/in b.staff's week templates, or for one day on b.staff's calendar/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Create Day Template'}));

    await waitFor(() => expect(router.state.location.href).toBe(`/calendar/templates?staff=${OTHER.userId}`));
    expect(sent(fetch, 'POST')).toBeDefined();
  });

  it("goes back to the list of the template's owner, and says whose calendar a change affects", async () => {
    const {user, router} = renderEditor('dt1', {[`GET ${BASE}/day-templates/dt1`]: () => json({...template, ownerUserId: OTHER.userId})});

    expect(await screen.findByText("Standard day · b.staff's calendar templates")).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(within(openDialog()!).getByText(/so b.staff's calendar changes too./)).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.href).toBe(`/calendar/templates?staff=${OTHER.userId}`));
  });

  it("shows staff someone else's template read-only", async () => {
    renderEditor('dt1', {[`GET ${BASE}/day-templates/dt1`]: () => json({...template, ownerUserId: OTHER.userId, canEdit: false})}, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Day Template'})).toBeInTheDocument();
    expect(screen.getByText('Only b.staff or an admin can change this day template.')).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('textbox', {name: /Name/}))).toBe(true);
    expect(isDisabled(block(1).getByRole('combobox', {name: 'Activity'}))).toBe(true);
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add Block'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Remove/})).not.toBeInTheDocument();
  });

  it("names the owner plainly when they aren't in the staff list", async () => {
    renderEditor(
      'dt1',
      {
        [`GET ${BASE}/day-templates/dt1`]: () => json({...template, ownerUserId: '00000000-0000-0000-0000-0000000000c3', canEdit: false}),
      },
      {user: staffUser},
    );

    expect(await screen.findByText('Only its owner or an admin can change this day template.')).toBeInTheDocument();
    expect(screen.getByText(/in their week templates, or for one day on their calendar/)).toBeInTheDocument();
  });

  it("doesn't let staff add to someone else's templates", async () => {
    renderEditor(undefined, {}, {user: staffUser, staff: OTHER.userId});

    expect(await screen.findByText("Only b.staff or an admin can add to b.staff's calendar templates.")).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: /Name/})).not.toBeInTheDocument();
  });

  it("says so when it can't tell whose a new one is", async () => {
    renderEditor(undefined, {[`GET ${BASE}/staff`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The day template could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

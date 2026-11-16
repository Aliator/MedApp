import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, descriptionOf, expectAnnounced, findAlert, json, mockApi, openDialog, pageOf, renderWithApp, withAnnouncements} from '../../test/render';
import {HalfViewContext} from '../../components/pages/halfView';
import {MonitoringPage} from './MonitoringPage';

const RULES = '/api/monitoring/rules';
const SAM = '0000000a-0000-0000-0000-000000000001';
const KIM = '0000000a-0000-0000-0000-000000000002';
const KNEE = '0000000e-0000-0000-0000-000000000001';
const BLOODS = '0000000d-0000-0000-0000-000000000001';
const SCAN = '0000000d-0000-0000-0000-000000000002';

const people = [
  {userId: SAM, username: 'sam.staff', roles: ['User']},
  {userId: KIM, username: 'kim.staff', roles: ['User']},
];
const templates = [{id: KNEE, name: 'Knee review', isArchived: false, stages: []}];
const definitions = [
  {id: BLOODS, name: 'Bloods', description: '', instructions: '', isArchived: false},
  {id: SCAN, name: 'Scan', description: '', instructions: '', isArchived: true},
];

const rules = [
  {
    id: 'r1',
    event: 'StageFinished',
    subjectUserId: SAM,
    subjectUsername: 'sam.staff',
    minimumPriority: 'High',
    stageDefinitionId: BLOODS,
    stageName: 'Bloods',
    templateId: null,
    templateName: null,
    isEnabled: true,
  },
  {id: 'r2', event: 'ProcedureCreated', subjectUserId: null, minimumPriority: null, templateId: KNEE, templateName: 'Knee review', isEnabled: false},
];

function renderPage(extra: Parameters<typeof mockApi>[0] = {}, list: unknown[] = rules) {
  const fetch = mockApi({
    [`GET ${RULES}`]: () => json(list),
    'GET /api/auth/users/search': () => json(pageOf(people)),
    'GET /api/procedures/patient-procedure-stage/templates': () => json(templates),
    'GET /api/procedures/patient-procedure-stage/definitions': () => json(definitions),
    ...extra,
  });
  return {fetch, ...renderWithApp(<MonitoringPage />, {user: adminUser})};
}

const sent = async (fetch: ReturnType<typeof mockApi>, method: string) =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!).json();
// The row whose sentence has this text (the dialogs' choices can hold the same names).
const rowOf = async (text: string | RegExp) => (await within(await screen.findByRole('table')).findAllByText(text))[0].closest('tr')!;
const pick = async (user: ReturnType<typeof renderPage>['user'], field: RegExp, option: string) => {
  await user.click(within(openDialog()!).getByRole('combobox', {name: field}));
  await user.click(screen.getByRole('option', {name: option}));
};
const preview = () => within(openDialog()!).getByText('Preview').parentElement!;

describe('MonitoringPage', () => {
  it("lists the admin's rules as sentences, each with its switch", async () => {
    renderPage();

    const first = await rowOf('Bloods');
    expect(first).toHaveTextContent('Notify me when sam.staff finishes Bloods on a High or Urgent priority procedure');
    expect(await rowOf('Knee review')).toHaveTextContent('Notify me when anyone creates a procedure from the Knee review template');
    expect(within(first).getByRole('switch')).toBeChecked();
    expect(within(await rowOf('Knee review')).getByRole('switch')).not.toBeChecked();
    expect(screen.getByText('2 rules, 1 on')).toBeInTheDocument();
  });

  it('adds a rule, previewing its sentence as it is built', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({[`POST ${RULES}`]: () => json({id: 'r9'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Rule'}));
    expect(preview()).toHaveTextContent('Notify me when anyone finishes a stage on a procedure');
    await pick(user, /Staff member/, 'sam.staff');
    await pick(user, /Stage/, 'Bloods');
    await pick(user, /Priority/, 'High or higher');
    await pick(user, /Template/, 'Knee review');
    expect(preview()).toHaveTextContent(
      'Notify me when sam.staff finishes Bloods on a High or Urgent priority procedure from the Knee review template',
    );
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Rule'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST')).toEqual({
      subjectUserId: SAM,
      event: 'StageFinished',
      minimumPriority: 'High',
      templateId: KNEE,
      stageDefinitionId: BLOODS,
      isEnabled: true,
    });
    await expectAnnounced('Rule added');
  });

  it('offers a stage only for stage events, and drops a stage chosen before switching away', async () => {
    const {user, fetch} = renderPage({[`POST ${RULES}`]: () => json({id: 'r9'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Rule'}));
    await pick(user, /Stage/, 'Scan (archived)');
    expect(preview()).toHaveTextContent('finishes Scan on');
    await pick(user, /Does this/, 'Is assigned to a procedure');
    expect(within(openDialog()!).queryByRole('combobox', {name: /Stage/})).not.toBeInTheDocument();
    expect(preview()).toHaveTextContent('Notify me when anyone is assigned to a procedure');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Rule'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST')).toMatchObject({event: 'ProcedureAssigned', stageDefinitionId: null});
  });

  it('edits a rule, starting from what it says', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({[`PUT ${RULES}/r1`]: () => json(rules[0])});

    await user.click(within(await rowOf('Bloods')).getByRole('button', {name: 'Edit rule'}));
    expect(preview()).toHaveTextContent('Notify me when sam.staff finishes Bloods on a High or Urgent priority procedure');
    await pick(user, /Priority/, 'Any priority');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'PUT')).toEqual({
      subjectUserId: SAM,
      event: 'StageFinished',
      minimumPriority: null,
      templateId: null,
      stageDefinitionId: BLOODS,
      isEnabled: true,
    });
    await expectAnnounced('Rule saved');
  });

  it('keeps a rule naming something since deleted, and says so', async () => {
    const gone = {id: 'r3', event: 'StageUnlocked', subjectUserId: 'gone-user', subjectUsername: null, stageDefinitionId: 'gone-stage', stageName: null, isEnabled: true};
    const {user} = renderPage({}, [gone]);

    expect(await rowOf(/a deleted user/)).toHaveTextContent('Notify me when a deleted user unlocks a deleted stage on a procedure');
    await user.click(screen.getByRole('button', {name: 'Edit rule'}));
    expect(within(openDialog()!).getByRole('combobox', {name: /Staff member/})).toHaveTextContent('Deleted user');
    expect(within(openDialog()!).getByRole('combobox', {name: /Stage/})).toHaveTextContent('Deleted stage');
  });

  it('switches a rule off with its own request, named by its sentence, held still while it saves', async () => {
    withAnnouncements();
    let finish: (response: Response) => void = () => {};
    const {user, fetch} = renderPage({[`PUT ${RULES}/r1/enabled`]: () => new Promise<Response>((resolve) => (finish = resolve))});

    const toggle = within(await rowOf('Bloods')).getByRole('switch', {name: 'Notify me when sam.staff finishes Bloods on a High or Urgent priority procedure'});
    await user.click(toggle);

    await waitFor(() => expect(toggle).toHaveAttribute('aria-disabled', 'true'));
    expect(toggle).not.toBeChecked();
    expect(await sent(fetch, 'PUT')).toEqual({isEnabled: false});
    finish(json({...rules[0], isEnabled: false}));
    await expectAnnounced('Rule switched off');
    await waitFor(() => expect(toggle).not.toHaveAttribute('aria-disabled'));
  });

  it('switches two rules at once, and reports each', async () => {
    withAnnouncements();
    const {user} = renderPage({
      [`PUT ${RULES}/r1/enabled`]: () => json({title: 'Server error'}, 500),
      [`PUT ${RULES}/r2/enabled`]: () => json({...rules[1], isEnabled: true}),
    });

    await user.click(within(await rowOf('Bloods')).getByRole('switch'));
    await user.click(within(await rowOf('Knee review')).getByRole('switch'));

    await expectAnnounced('Rule switched on');
    await findAlert('A server error occurred');
  });

  it('says when the rule to switch has gone', async () => {
    const {user} = renderPage({[`PUT ${RULES}/r2/enabled`]: () => json({title: 'Monitoring rule not found.'}, 404)});

    await user.click(within(await rowOf('Knee review')).getByRole('switch'));

    await findAlert('That rule no longer exists');
  });

  it('offers only admins for "Unlocks a stage", and says when the list is cut short', async () => {
    const admins = [{userId: KIM, username: 'kim.admin', roles: ['Admin', 'User']}, {userId: SAM, username: 'sam.staff', roles: ['User']}];
    const {user} = renderPage({'GET /api/auth/users/search': () => json(pageOf(admins, 140))});

    await user.click(await screen.findByRole('button', {name: 'Add Rule'}));
    const person = () => within(openDialog()!).getByRole('combobox', {name: /Staff member/});
    expect(descriptionOf(person())).toContain('Only the first 100 accounts by username are listed for now.');
    await pick(user, /Does this/, 'Unlocks a stage (admins only)');
    expect(descriptionOf(person())).toContain('Only admins unlock stages, so only admins are listed.');
    await user.click(person());

    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Anyone', 'kim.admin']);
  });

  it('deletes a rule after a confirmation', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({[`DELETE ${RULES}/r2`]: () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('Knee review')).getByRole('button', {name: 'Delete rule'}));
    expect(openDialog()).toHaveTextContent('Notify me when anyone creates a procedure from the Knee review template');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Rule'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(fetch.mock.calls.map(([r]) => `${(r as Request).method} ${new URL((r as Request).url).pathname}`)).toContain(`DELETE ${RULES}/r2`);
    await expectAnnounced('Rule deleted');
  });

  it('says when a delete fails', async () => {
    const {user} = renderPage({[`DELETE ${RULES}/r2`]: () => json({title: 'Server error'}, 500)});

    await user.click(within(await rowOf('Knee review')).getByRole('button', {name: 'Delete rule'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Rule'}));

    await findAlert('A server error occurred');
  });

  it('says when something newly chosen has gone', async () => {
    const {user} = renderPage({[`POST ${RULES}`]: () => json({title: "User 'x' was not found."}, 404)});

    await user.click(await screen.findByRole('button', {name: 'Add Rule'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Rule'}));

    await findAlert('The rule, or something newly chosen for it, no longer exists');
  });

  it("shows the API's errors in the form", async () => {
    const {user} = renderPage({
      [`POST ${RULES}`]: () => json({title: 'Validation failed', status: 400, errors: {MinimumPriority: ['Priority must be one of: Low.']}}, 400),
    });

    await user.click(await screen.findByRole('button', {name: 'Add Rule'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Rule'}));

    expect((await within(openDialog()!).findAllByText('Priority must be one of: Low.')).length).toBeGreaterThan(0);
  });

  it('shows each rule as a card, with its switch and actions, when too narrow for the table (a half view)', async () => {
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(320);
    mockApi({
      [`GET ${RULES}`]: () => json(rules),
      'GET /api/auth/users/search': () => json(pageOf(people)),
      'GET /api/procedures/patient-procedure-stage/templates': () => json(templates),
      'GET /api/procedures/patient-procedure-stage/definitions': () => json(definitions),
    });
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <MonitoringPage />
      </HalfViewContext.Provider>,
      {user: adminUser},
    );

    const cards = await screen.findAllByRole('listitem');
    expect(cards).toHaveLength(2);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(cards[0]).toHaveTextContent('Notify me when sam.staff finishes Bloods on a High or Urgent priority procedure');
    expect(within(cards[0]).getByRole('switch')).toBeChecked();
    expect(within(cards[1]).getByRole('button', {name: 'Edit rule'})).toBeInTheDocument();
  });

  it('starts empty with a way to add the first rule', async () => {
    const {user} = renderPage({}, []);

    await user.click(await screen.findByRole('button', {name: 'Add Your First Rule'}));

    expect(openDialog()).toHaveTextContent('Add Rule');
    expect(screen.getByText("What staff do that you're told about")).toBeInTheDocument();
  });

  it('says when the rules could not be loaded', async () => {
    renderPage({[`GET ${RULES}`]: () => json({title: 'Server error'}, 500)});

    await findAlert('Your rules could not be loaded');
  });
});

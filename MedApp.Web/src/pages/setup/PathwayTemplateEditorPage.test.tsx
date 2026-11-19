import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, findAlert, json, mockApi, openDialog, renderWithApp, expectAnnounced, withAnnouncements} from '../../test/render';
import {PARALLEL_ROUTES_LOCKED} from '../../api/pathwayRoutes';
import {PathwayTemplateEditorPage, startingStopKey, stopStageOrder} from './PathwayTemplateEditorPage';
import {BRANCH_WITHOUT_POINT_MESSAGE} from './templateBranches';

const BASE = '/api/pathways/patient-pathway-stage';

const definitions = [
  {id: 'd1', name: 'Consultation', description: 'Seen by the clinician.', isArchived: false},
  // A real SNOMED CT concept ID, used only as a code: 71388002 is Procedure.
  {id: 'd2', name: 'Scan', description: 'Imaging done.', snomedCode: '71388002', snomedTerm: 'Procedure', isArchived: false, visitRequirement: 'Suggested'},
  {id: 'd3', name: 'Old triage', description: '', isArchived: true},
];

const template = {
  id: 't1',
  name: 'Knee review',
  pathwayCount: 3,
  teamId: 'sp1',
  teamName: 'Orthopaedics',
  stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'],
  stages: [],
};

/** The active teams (the editor asks for those only). */
const teams = [
  {id: 'sp2', name: 'Urology', isArchived: false},
  {id: 'sp1', name: 'Orthopaedics', isArchived: false},
];

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/definitions`]: () => json(definitions),
    [`GET ${BASE}/templates/t1`]: () => json(template),
    'GET /api/teams': () => json(teams),
    ...extra,
  });
  const path = id ? `/admin/pathway-templates/${id}` : '/admin/pathway-templates/new';
  return {fetch, ...renderWithApp(<PathwayTemplateEditorPage id={id} />, {user: adminUser, path})};
}

const stageNames = () =>
  within(screen.getByRole('list', {name: 'Stages in order'}))
    .getAllByText(/^\d+\. /)
    .map((e) => e.textContent);
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method);

async function chooseTeam(user: ReturnType<typeof renderEditor>['user'], name: string) {
  await user.click(screen.getByRole('combobox', {name: /Team/}));
  await user.click(screen.getByRole('option', {name}));
}

describe('PathwayTemplateEditorPage', () => {
  it('builds a new template from the active definitions, then creates it after a confirmation', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor(undefined, {[`POST ${BASE}/templates`]: () => json({id: 't9'}, 201)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Scan pathway');
    const addable = within(screen.getByRole('list', {name: 'Stages to add'}));
    expect(addable.queryByText('Old triage')).not.toBeInTheDocument();
    // Whether each involves a visit shows on it, here and once added.
    expect(addable.getAllByText('Visit suggested')).toHaveLength(1);
    await user.click(addable.getByRole('button', {name: 'Add Consultation'}));
    await user.click(addable.getByRole('button', {name: 'Add Scan'}));
    await user.click(addable.getByRole('button', {name: 'Add Consultation'}));
    expect(stageNames()).toEqual(['1. Consultation', '2. Scan', '3. Consultation']);
    expect(within(screen.getByRole('list', {name: 'Stages in order'})).getAllByText('Visit suggested')).toHaveLength(1);

    await user.click(screen.getByRole('button', {name: 'Remove Scan (stage 2)'}));
    expect(stageNames()).toEqual(['1. Consultation', '2. Consultation']);

    await chooseTeam(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    expect(within(openDialog()!).getByText('Urology')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
    expect(await sent(fetch, 'POST')!.json()).toEqual({name: 'Scan pathway', stageDefinitionIdsInOrder: ['d1', 'd1'], teamId: 'sp2', clockStopStageOrder: null});
    await expectAnnounced('Template created');
  });

  it("starts from a template's stages, archived ones marked, and says its pathways keep theirs", async () => {
    const {user, fetch} = renderEditor('t1', {[`PATCH ${BASE}/templates/t1`]: () => json(template)});

    expect(await screen.findByRole('textbox', {name: /Name/})).toHaveValue('Knee review');
    expect(stageNames()).toEqual(['1. Consultation', '2. Old triage', '3. Consultation']);
    expect(within(screen.getByRole('list', {name: 'Stages in order'})).getByText('Archived')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(within(openDialog()!).getByText(/The 3 pathways already made from it keep theirs/)).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
    expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'], clockStopStageOrder: null});
  });

  it("shows the API's errors: the name's on its field, the stages' in the header", async () => {
    const {user} = renderEditor(undefined, {
      [`POST ${BASE}/templates`]: () =>
        json(
          {title: 'Validation failed', status: 400, errors: {Name: ['Name is required.'], StageDefinitionIdsInOrder: ['Add at least one stage.']}},
          400,
        ),
    });

    await screen.findByRole('button', {name: 'Create Template'});
    await chooseTeam(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    const banner = (await screen.findByText('Add at least one stage.')).closest('[role="alert"], section')!;
    expect(banner).not.toHaveTextContent('Name is required.');
    expect(screen.getByRole('textbox', {name: /Name/})).toHaveAccessibleDescription(/Name is required\./);
  });

  it('asks for a team before creating a template, on the field, without asking to confirm', async () => {
    const {user, fetch} = renderEditor();

    await user.click(await screen.findByRole('button', {name: 'Create Template'}));

    expect(openDialog()).toBeNull();
    const field = screen.getByRole('combobox', {name: /Team/});
    await waitFor(() => expect(field).toHaveAccessibleDescription(/Choose the team this template belongs to\./));
    await user.click(field);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Orthopaedics', 'Urology']);
    await user.click(screen.getByRole('option', {name: 'Urology'}));
    expect(field).not.toHaveAccessibleDescription(/Choose the team/);
    expect(sent(fetch, 'POST')).toBeUndefined();
  });

  it("shows the API's reason when a team can't be chosen, e.g. one archived meanwhile", async () => {
    const {user} = renderEditor(undefined, {
      [`POST ${BASE}/templates`]: () => json({title: 'Urology is archived, so it cannot be chosen.', status: 409}, 409),
    });

    await screen.findByRole('button', {name: 'Create Template'});
    await chooseTeam(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    expect(await findAlert('Urology is archived, so it cannot be chosen.')).toBeInTheDocument();
  });

  it('sends a changed team when saving, and keeps an archived one it has, marked', async () => {
    const {user, fetch} = renderEditor('t1', {
      [`GET ${BASE}/templates/t1`]: () => json({...template, teamId: 'sp9', teamName: 'Old clinic'}),
      [`PATCH ${BASE}/templates/t1`]: () => json(template),
    });

    const field = await screen.findByRole('combobox', {name: /Team/});
    expect(field).toHaveTextContent('Old clinic (archived)');
    await user.click(field);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Old clinic (archived)', 'Orthopaedics', 'Urology']);
    await user.click(screen.getByRole('option', {name: 'Urology'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
    expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'], teamId: 'sp2', clockStopStageOrder: null});
  });

  it('sends a changed retention class, saying a longer one applies to existing pathways and a shorter one never shortens them', async () => {
    const {user, fetch} = renderEditor('t1', {[`PATCH ${BASE}/templates/t1`]: () => json(template)});

    const field = await screen.findByRole('combobox', {name: /Retention class/});
    expect(field).toHaveTextContent('Standard (adult or child period)');
    expect(screen.getByText(/A longer class applies to its existing pathways too; a shorter one doesn't shorten their records/)).toBeInTheDocument();
    await user.click(field);
    await user.click(screen.getByRole('option', {name: 'Oncology'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
    expect(await sent(fetch, 'PATCH')!.json()).toEqual({
      name: 'Knee review',
      stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'],
      clockStopStageOrder: null,
      retentionClass: 'Oncology',
    });
  });

  it('says when the teams could not be loaded', async () => {
    renderEditor(undefined, {'GET /api/teams': () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The template could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('moves a stage with the keyboard from its handle', async () => {
    const {user} = renderEditor('t1');

    await screen.findByRole('list', {name: 'Stages in order'});
    const handle = screen.getByRole('button', {name: /Reorder.*1\. Consultation/});
    await user.click(handle);
    await user.keyboard('{ArrowDown}');

    await waitFor(() => expect(stageNames()).toEqual(['1. Old triage', '2. Consultation', '3. Consultation']));
  });

  it('asks plainly before saving a template no pathway uses, and stays put if cancelled', async () => {
    const {user, fetch} = renderEditor('t1', {[`GET ${BASE}/templates/t1`]: () => json({...template, pathwayCount: 0})});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    expect(within(openDialog()!).getByText('Are you sure you want to save these changes?')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(sent(fetch, 'PATCH')).toBeUndefined();
  });

  it('names the one pathway made from a template, and says why a save failed', async () => {
    const {user} = renderEditor('t1', {
      [`GET ${BASE}/templates/t1`]: () => json({...template, pathwayCount: 1}),
      [`PATCH ${BASE}/templates/t1`]: () => json({title: 'Server error'}, 500),
    });

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    expect(within(openDialog()!).getByText(/The 1 pathway already made from it keeps? theirs/)).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it("shows each stage's SNOMED CT concept, in the template and in the stages to add", async () => {
    const {user} = renderEditor();

    const addable = within(await screen.findByRole('list', {name: 'Stages to add'}));
    expect(addable.getByText('SNOMED CT: 71388002 | Procedure')).toBeInTheDocument();
    await user.click(addable.getByRole('button', {name: 'Add Scan'}));

    const added = within(screen.getByRole('list', {name: 'Stages in order'}));
    expect(added.getByText('SNOMED CT: 71388002 | Procedure')).toBeInTheDocument();
    expect(added.getByText('Imaging done.')).toBeInTheDocument();
  });

  it('shows a stage whose definition is gone as an unknown stage', async () => {
    renderEditor('t1', {[`GET ${BASE}/templates/t1`]: () => json({...template, stageDefinitionIdsInOrder: ['d404']})});

    await screen.findByRole('list', {name: 'Stages in order'});
    expect(stageNames()).toEqual(['1. Unknown stage']);
    expect(screen.getByRole('button', {name: 'Remove stage (stage 1)'})).toBeInTheDocument();
  });

  it('puts everything in one card in a half view, with the stages to add below', async () => {
    mockApi({[`GET ${BASE}/definitions`]: () => json(definitions), 'GET /api/teams': () => json(teams)});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <PathwayTemplateEditorPage />
      </HalfViewContext.Provider>,
      {user: adminUser, path: '/admin/pathway-templates/new'},
    );

    expect(await screen.findByText('Add stages from the list below.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Add Stages'})).toBeInTheDocument();
    expect(screen.queryByText('All Stages')).not.toBeInTheDocument();
  });

  it('says when the stages could not be loaded', async () => {
    renderEditor(undefined, {[`GET ${BASE}/definitions`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The template could not be loaded. Please try again.')).toBeInTheDocument();
  });

  describe('the clock-stop stage', () => {
    const stopField = () => screen.getByRole('combobox', {name: /Clock-stop stage/});
    async function chooseStop(user: ReturnType<typeof renderEditor>['user'], name: string) {
      await user.click(stopField());
      await user.click(screen.getByRole('option', {name}));
    }
    const stopsTheClock = () =>
      within(screen.getByRole('list', {name: 'Stages in order'}))
        .getAllByRole('listitem')
        .filter((item) => within(item).queryByText('Stops the clock'))
        .map((item) => within(item).getByText(/^\d+\. /).textContent);

    it('starts from the template, marks that stage, and sends its position with the stages', async () => {
      const {user, fetch} = renderEditor('t1', {
        [`GET ${BASE}/templates/t1`]: () => json({...template, clockStopStageOrder: 3}),
        [`PATCH ${BASE}/templates/t1`]: () => json(template),
      });

      await screen.findByRole('list', {name: 'Stages in order'});
      expect(stopField()).toHaveTextContent('3. Consultation');
      expect(stopField()).toHaveAccessibleDescription(/The waiting-time clock stops when this stage is finished\./);
      expect(stopsTheClock()).toEqual(['3. Consultation']);

      await user.click(screen.getByRole('button', {name: 'Save'}));
      expect(within(openDialog()!).getByText('When 3. Consultation is finished')).toBeInTheDocument();
      await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

      await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
      expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'], clockStopStageOrder: 3});
    });

    it('stays on the same stage when the stages are reordered', async () => {
      const {user, fetch} = renderEditor('t1', {[`PATCH ${BASE}/templates/t1`]: () => json(template)});

      await screen.findByRole('list', {name: 'Stages in order'});
      await chooseStop(user, '1. Consultation');
      expect(stopsTheClock()).toEqual(['1. Consultation']);

      await user.click(screen.getByRole('button', {name: /Reorder.*1\. Consultation/}));
      await user.keyboard('{ArrowDown}');
      await waitFor(() => expect(stageNames()).toEqual(['1. Old triage', '2. Consultation', '3. Consultation']));
      expect(stopsTheClock()).toEqual(['2. Consultation']);
      expect(stopField()).toHaveTextContent('2. Consultation');

      await user.click(screen.getByRole('button', {name: 'Save'}));
      await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));
      await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
      expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d3', 'd1', 'd1'], clockStopStageOrder: 2});
    });

    it('goes when its stage is removed, and can be set back to none', async () => {
      const {user} = renderEditor('t1');

      await screen.findByRole('list', {name: 'Stages in order'});
      await chooseStop(user, '2. Old triage');
      expect(stopsTheClock()).toEqual(['2. Old triage']);
      await user.click(screen.getByRole('button', {name: 'Remove Old triage (stage 2)'}));
      expect(stopsTheClock()).toEqual([]);
      expect(stopField()).toHaveTextContent('None');

      await chooseStop(user, '2. Consultation');
      expect(stopsTheClock()).toEqual(['2. Consultation']);
      await chooseStop(user, 'None');
      expect(stopsTheClock()).toEqual([]);

      await user.click(screen.getByRole('button', {name: 'Save'}));
      expect(within(openDialog()!).getByText('At no stage')).toBeInTheDocument();
    });

    it('is disabled until the template has stages', async () => {
      renderEditor();

      await screen.findByRole('list', {name: 'Stages to add'});
      expect(stopField()).toHaveAttribute('aria-disabled', 'true');
    });

    it('starts from the stage marked as stopping the clock, and from none for a new template', () => {
      const rows = [
        {key: 'a', definitionId: 'd1'},
        {key: 'b', definitionId: 'd2'},
      ];
      expect(startingStopKey({...template, stages: [{stageOrder: 2, stopsClock: true}]}, rows)).toBe('b');
      expect(startingStopKey({...template, clockStopStageOrder: 1}, rows)).toBe('a');
      expect(startingStopKey({...template, clockStopStageOrder: 9}, rows)).toBe('none');
      expect(startingStopKey(null, rows)).toBe('none');
      expect(stopStageOrder(rows, 'b')).toBe(2);
      expect(stopStageOrder(rows, 'gone')).toBeNull();
    });
  });

  it('goes back to the list for an unknown template', async () => {
    const {router} = renderEditor('t404', {[`GET ${BASE}/templates/t404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
  });

  describe('optional stages and branches', () => {
    const row = (n: number) => within(within(screen.getByRole('list', {name: 'Stages in order'})).getAllByRole('listitem')[n]);

    it('builds branches after a branch point, indented, saying what the API would refuse until they are right', async () => {
      const {user, fetch} = renderEditor(undefined, {[`POST ${BASE}/templates`]: () => json({id: 't9'}, 201)});

      await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Biopsy pathway');
      const addable = within(screen.getByRole('list', {name: 'Stages to add'}));
      for (const name of ['Consultation', 'Scan', 'Scan', 'Consultation']) await user.click(addable.getByRole('button', {name: `Add ${name}`}));

      // Plain stages: two boxes, unticked, and no branch until there's a branch point before them.
      expect(row(0).getByRole('checkbox', {name: 'Optional'})).not.toBeChecked();
      expect(row(0).getByRole('checkbox', {name: 'Branch point'})).not.toBeChecked();
      expect(row(1).queryByRole('textbox', {name: 'Branch'})).not.toBeInTheDocument();

      await user.click(row(0).getByRole('checkbox', {name: 'Branch point'}));
      expect(row(0).getByText(/needs at least two branches after it/)).toBeInTheDocument();
      expect(row(0).queryByRole('textbox', {name: 'Branch'})).not.toBeInTheDocument();
      await user.type(row(1).getByRole('textbox', {name: 'Branch'}), 'Benign');
      await user.type(row(2).getByRole('textbox', {name: 'Branch'}), 'Treatment');
      await user.click(row(2).getByRole('checkbox', {name: 'Optional'}));
      expect(screen.queryByText(/needs at least two branches/)).not.toBeInTheDocument();

      // Stages in a branch sit indented under the branch point; the one after them always runs.
      const items = within(screen.getByRole('list', {name: 'Stages in order'})).getAllByRole('listitem');
      expect(items[1].getAttribute('style')).toContain('margin-inline-start');
      expect(items[3].getAttribute('style') ?? '').not.toContain('margin-inline-start');

      // The last stage always runs.
      await user.click(row(3).getByRole('checkbox', {name: 'Optional'}));
      expect(row(3).getByText("The last stage always runs: it can't be optional, a branch point or in a branch.")).toBeInTheDocument();
      await user.click(row(3).getByRole('checkbox', {name: 'Optional'}));

      await chooseTeam(user, 'Urology');
      await user.click(screen.getByRole('button', {name: 'Create Template'}));
      expect(within(openDialog()!).getByText(/1\. Consultation \(branch point\), 2\. Scan \(Benign\), 3\. Scan \(optional, Treatment\), 4\. Consultation/)).toBeInTheDocument();
      await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

      await waitFor(() => expect(sent(fetch, 'POST')).toBeDefined());
      expect(await sent(fetch, 'POST')!.json()).toEqual({
        name: 'Biopsy pathway',
        stageDefinitionIdsInOrder: ['d1', 'd2', 'd2', 'd1'],
        stageOptions: [
          {isOptional: false, choosesBranch: true, branch: null},
          {isOptional: false, choosesBranch: false, branch: 'Benign'},
          {isOptional: true, choosesBranch: false, branch: 'Treatment'},
          {isOptional: false, choosesBranch: false, branch: null},
        ],
        teamId: 'sp2',
        clockStopStageOrder: null,
      });
    });

    it("starts from a template's stage options, and sends none once every stage is plain again", async () => {
      const options = (stageOrder: number, stageDefinitionId: string, more: Record<string, unknown> = {}) => ({
        stageOrder,
        stageDefinitionId,
        isOptional: false,
        choosesBranch: false,
        branch: null,
        ...more,
      });
      const {user, fetch} = renderEditor('t1', {
        [`GET ${BASE}/templates/t1`]: () =>
          json({
            ...template,
            stageDefinitionIdsInOrder: ['d1', 'd2', 'd2', 'd1'],
            stages: [options(1, 'd1', {choosesBranch: true}), options(2, 'd2', {branch: 'Benign'}), options(3, 'd2', {branch: 'Treatment'}), options(4, 'd1')],
          }),
        [`PATCH ${BASE}/templates/t1`]: () => json(template),
      });

      await screen.findByRole('list', {name: 'Stages in order'});
      expect(row(0).getByRole('checkbox', {name: 'Branch point'})).toBeChecked();
      expect(row(1).getByRole('textbox', {name: 'Branch'})).toHaveValue('Benign');
      expect(row(2).getByRole('textbox', {name: 'Branch'})).toHaveValue('Treatment');

      await user.clear(row(1).getByRole('textbox', {name: 'Branch'}));
      await user.clear(row(2).getByRole('textbox', {name: 'Branch'}));
      await user.click(row(0).getByRole('checkbox', {name: 'Branch point'}));
      // With no branch point before them, the emptied branch fields go.
      expect(row(1).queryByRole('textbox', {name: 'Branch'})).not.toBeInTheDocument();
      await user.click(screen.getByRole('button', {name: 'Save'}));
      await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

      await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
      expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd2', 'd2', 'd1'], clockStopStageOrder: null});
    });

    it('says a stage in a branch must come after a branch point', async () => {
      const {user} = renderEditor('t1', {
        [`GET ${BASE}/templates/t1`]: () =>
          json({
            ...template,
            stageDefinitionIdsInOrder: ['d1', 'd2', 'd1'],
            stages: [
              {stageOrder: 1, stageDefinitionId: 'd1', isOptional: false, choosesBranch: true, branch: null},
              {stageOrder: 2, stageDefinitionId: 'd2', isOptional: false, choosesBranch: false, branch: null},
              {stageOrder: 3, stageDefinitionId: 'd1', isOptional: false, choosesBranch: false, branch: null},
            ],
          }),
      });

      await screen.findByRole('list', {name: 'Stages in order'});
      await user.type(row(1).getByRole('textbox', {name: 'Branch'}), 'Benign');
      await user.click(row(0).getByRole('checkbox', {name: 'Branch point'}));

      // The name stays, so it can be cleared, with the reason it can't be saved.
      expect(row(1).getByRole('textbox', {name: 'Branch'})).toHaveValue('Benign');
      expect(row(1).getByText(BRANCH_WITHOUT_POINT_MESSAGE)).toBeInTheDocument();
    });
  });

  it('shows a template with routes side by side read-only, each stage with the ones it comes after (MC8)', async () => {
    renderEditor('t1', {
      [`GET ${BASE}/templates/t1`]: () =>
        json({
          ...template,
          stageDefinitionIdsInOrder: ['d1', 'd2', 'd2', 'd1'],
          stages: [1, 2, 3, 4].map((stageOrder) => ({stageOrder, stageDefinitionId: stageOrder % 3 === 1 ? 'd1' : 'd2', name: stageOrder % 3 === 1 ? 'Consultation' : 'Scan'})),
          links: [
            {fromStageOrder: 1, toStageOrder: 2},
            {fromStageOrder: 1, toStageOrder: 3},
            {fromStageOrder: 2, toStageOrder: 4},
            {fromStageOrder: 3, toStageOrder: 4},
          ],
        }),
    });

    const list = within(await screen.findByRole('list', {name: 'Stages in order'}));
    expect(screen.getByText(PARALLEL_ROUTES_LOCKED)).toBeInTheDocument();
    expect(list.getByText('After 2. Scan and 3. Scan')).toBeInTheDocument();
    expect(list.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Consultation'})).toHaveAttribute('aria-disabled', 'true');
  });

  it('goes back to the list for an unknown template', async () => {
    const {router} = renderEditor('t404', {[`GET ${BASE}/templates/t404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, findAlert, json, mockApi, openDialog, renderWithApp, expectAnnounced, withAnnouncements} from '../../test/render';
import {PathwayTemplateEditorPage} from './PathwayTemplateEditorPage';

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
  specialtyId: 'sp1',
  specialtyName: 'Orthopaedics',
  stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'],
  stages: [],
};

/** The active specialties (the editor asks for those only). */
const specialties = [
  {id: 'sp2', name: 'Urology', isArchived: false},
  {id: 'sp1', name: 'Orthopaedics', isArchived: false},
];

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/definitions`]: () => json(definitions),
    [`GET ${BASE}/templates/t1`]: () => json(template),
    'GET /api/specialties': () => json(specialties),
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

async function chooseSpecialty(user: ReturnType<typeof renderEditor>['user'], name: string) {
  await user.click(screen.getByRole('combobox', {name: /Specialty/}));
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

    await chooseSpecialty(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    expect(within(openDialog()!).getByText('Urology')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
    expect(await sent(fetch, 'POST')!.json()).toEqual({name: 'Scan pathway', stageDefinitionIdsInOrder: ['d1', 'd1'], specialtyId: 'sp2'});
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
    expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd3', 'd1']});
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
    await chooseSpecialty(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    const banner = (await screen.findByText('Add at least one stage.')).closest('[role="alert"], section')!;
    expect(banner).not.toHaveTextContent('Name is required.');
    expect(screen.getByRole('textbox', {name: /Name/})).toHaveAccessibleDescription(/Name is required\./);
  });

  it('asks for a specialty before creating a template, on the field, without asking to confirm', async () => {
    const {user, fetch} = renderEditor();

    await user.click(await screen.findByRole('button', {name: 'Create Template'}));

    expect(openDialog()).toBeNull();
    const field = screen.getByRole('combobox', {name: /Specialty/});
    await waitFor(() => expect(field).toHaveAccessibleDescription(/Choose the specialty this template belongs to\./));
    await user.click(field);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Orthopaedics', 'Urology']);
    await user.click(screen.getByRole('option', {name: 'Urology'}));
    expect(field).not.toHaveAccessibleDescription(/Choose the specialty/);
    expect(sent(fetch, 'POST')).toBeUndefined();
  });

  it("shows the API's reason when a specialty can't be chosen, e.g. one archived meanwhile", async () => {
    const {user} = renderEditor(undefined, {
      [`POST ${BASE}/templates`]: () => json({title: 'Urology is archived, so it cannot be chosen.', status: 409}, 409),
    });

    await screen.findByRole('button', {name: 'Create Template'});
    await chooseSpecialty(user, 'Urology');
    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    expect(await findAlert('Urology is archived, so it cannot be chosen.')).toBeInTheDocument();
  });

  it('sends a changed specialty when saving, and keeps an archived one it has, marked', async () => {
    const {user, fetch} = renderEditor('t1', {
      [`GET ${BASE}/templates/t1`]: () => json({...template, specialtyId: 'sp9', specialtyName: 'Old clinic'}),
      [`PATCH ${BASE}/templates/t1`]: () => json(template),
    });

    const field = await screen.findByRole('combobox', {name: /Specialty/});
    expect(field).toHaveTextContent('Old clinic (archived)');
    await user.click(field);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Old clinic (archived)', 'Orthopaedics', 'Urology']);
    await user.click(screen.getByRole('option', {name: 'Urology'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(sent(fetch, 'PATCH')).toBeDefined());
    expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'], specialtyId: 'sp2'});
  });

  it('says when the specialties could not be loaded', async () => {
    renderEditor(undefined, {'GET /api/specialties': () => json({title: 'Server error'}, 500)});

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
    mockApi({[`GET ${BASE}/definitions`]: () => json(definitions), 'GET /api/specialties': () => json(specialties)});
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

      await chooseSpecialty(user, 'Urology');
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
        specialtyId: 'sp2',
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
      expect(await sent(fetch, 'PATCH')!.json()).toEqual({name: 'Knee review', stageDefinitionIdsInOrder: ['d1', 'd2', 'd2', 'd1']});
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
      expect(row(1).getByText('A stage in a branch must come after a branch point.')).toBeInTheDocument();
    });
  });

  it('goes back to the list for an unknown template', async () => {
    const {router} = renderEditor('t404', {[`GET ${BASE}/templates/t404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
  });
});

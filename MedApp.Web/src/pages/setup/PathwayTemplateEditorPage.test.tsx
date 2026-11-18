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

const template = {id: 't1', name: 'Knee review', pathwayCount: 3, stageDefinitionIdsInOrder: ['d1', 'd3', 'd1'], stages: []};

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/definitions`]: () => json(definitions),
    [`GET ${BASE}/templates/t1`]: () => json(template),
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

    await user.click(screen.getByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
    expect(await sent(fetch, 'POST')!.json()).toEqual({name: 'Scan pathway', stageDefinitionIdsInOrder: ['d1', 'd1']});
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

    await user.click(await screen.findByRole('button', {name: 'Create Template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Template'}));

    const banner = (await screen.findByText('Add at least one stage.')).closest('[role="alert"], section')!;
    expect(banner).not.toHaveTextContent('Name is required.');
    expect(screen.getByRole('textbox', {name: /Name/})).toHaveAccessibleDescription(/Name is required\./);
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
    mockApi({[`GET ${BASE}/definitions`]: () => json(definitions)});
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

  it('goes back to the list for an unknown template', async () => {
    const {router} = renderEditor('t404', {[`GET ${BASE}/templates/t404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates'));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {findAlert, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {NewProcedurePage} from './NewProcedurePage';
import {typeRichText} from '../../test/richText';

const TEMPLATES = '/api/procedures/patient-procedure-stage/templates';
const CREATE = '/api/procedures/patient-procedures';

const templates = [
  {
    id: 'tpl-knee',
    name: 'Knee review',
    stages: [
      {stageOrder: 2, stageDefinitionId: 'd2', name: 'Scan', description: 'Imaging of the joint.'},
      {stageOrder: 1, stageDefinitionId: 'd1', name: 'Consultation', description: ''},
    ],
  },
  {id: 'tpl-skin', name: 'Skin check', stages: [{stageOrder: 1, stageDefinitionId: 'd1', name: 'Consultation'}]},
  // A template with no stages can't make a procedure, so it isn't offered.
  {id: 'tpl-empty', name: 'Empty template', stages: []},
];

function renderNew(handlers: Record<string, (request: Request) => Response | Promise<Response>> = {}) {
  const fetch = mockApi({
    'GET /api/patients/patient-1': () => json({id: 'patient-1', firstName: 'Test', lastName: 'Patient'}),
    [`GET ${TEMPLATES}`]: () => json(templates),
    ...handlers,
  });
  return {fetch, ...renderWithApp(<NewProcedurePage patientId="patient-1" />, {user: staffUser})};
}

describe('NewProcedurePage', () => {
  it('offers the templates that have stages, for the patient named in the subtitle', async () => {
    const {user} = renderNew();

    expect(await screen.findByText('For Test Patient')).toBeInTheDocument();
    await user.click(screen.getByRole('combobox', {name: /Template/}));

    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Knee review', 'Skin check']);
  });

  it("shows the chosen template's stages in order, and takes its name as the title until the title is changed", async () => {
    const {user} = renderNew();

    await user.click(await screen.findByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'Knee review'}));

    expect(screen.getByText('Stages (2)')).toBeInTheDocument();
    expect(screen.getAllByText(/^\d\. /).map((t) => t.textContent)).toEqual(['1. Consultation', '2. Scan']);
    expect(screen.getByText('Imaging of the joint.')).toBeInTheDocument();
    const title = screen.getByRole('textbox', {name: /Title/});
    expect(title).toHaveValue('Knee review');

    // Still the template's name: it follows the next template.
    await user.click(screen.getByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'Skin check'}));
    expect(title).toHaveValue('Skin check');

    // Changed by hand: it stays.
    await user.clear(title);
    await user.type(title, 'Left knee');
    await user.click(screen.getByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'Knee review'}));
    expect(title).toHaveValue('Left knee');
  });

  it('creates the procedure after a confirmation, then opens it', async () => {
    const {fetch, user, router} = renderNew({
      [`POST ${CREATE}`]: () => json({id: 'proc-new', patientId: 'patient-1', title: 'Knee review'}, 201),
    });

    await user.click(await screen.findByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'Knee review'}));
    await user.type(screen.getByRole('combobox', {name: /Due Date/}), '01/12/2099');
    await user.tab();
    await typeRichText(screen.getByRole('textbox', {name: 'Notes'}), 'Bring previous scans.');
    await user.click(screen.getByRole('button', {name: 'Create Procedure'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('Test Patient')).toBeInTheDocument();
    expect(dialog.getByText('1 December 2099')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Create Procedure'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures/proc-new'));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({
      patientId: 'patient-1',
      templateId: 'tpl-knee',
      title: 'Knee review',
      notes: 'Bring previous scans.',
      dueDate: '2099-12-01',
      priority: 'Medium',
    });
  });

  it("shows the API's validation errors on their fields", async () => {
    const {user} = renderNew({
      [`POST ${CREATE}`]: () =>
        json({title: 'Validation failed', status: 400, errors: {TemplateId: ['Template is required.'], DueDate: ['Due date is required.']}}, 400),
    });

    await screen.findByText('For Test Patient');
    await user.click(screen.getByRole('button', {name: 'Create Procedure'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Procedure'}));

    expect((await screen.findAllByText('Template is required.')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Due date is required.').length).toBeGreaterThan(0);
  });

  it('says to start from a patient when there is none', async () => {
    renderWithApp(<NewProcedurePage />, {user: staffUser});

    expect(await findAlert(/open the patient's procedures and choose New Procedure/)).toBeInTheDocument();
  });
});

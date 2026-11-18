import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {findAlert, json, mockApi, openDialog, renderWithApp, staffUser, expectAnnounced, withAnnouncements} from '../../test/render';
import {NewProcedurePage} from './NewProcedurePage';
import {typeRichText} from '../../test/richText';

const TEMPLATES = '/api/procedures/patient-procedure-stage/templates';
const CREATE = '/api/procedures/patient-procedures';

const templates = [
  {
    id: 'tpl-knee',
    name: 'Knee review',
    stages: [
      // A real SNOMED CT concept ID, used only as a code: 71388002 is Procedure.
      {stageOrder: 2, stageDefinitionId: 'd2', name: 'Scan', description: 'Imaging of the joint.', snomedCode: '71388002', snomedTerm: 'Procedure', visitRequirement: 'Required'},
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
  it("shows the patient's banner", async () => {
    renderNew({'GET /api/patients/patient-1': () => json({id: 'patient-1', firstName: 'Test', lastName: 'Patient', nhsNumber: '9990000034'})});

    const banner = await screen.findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('NHS No. 999 000 0034')).toBeInTheDocument();
  });

  it("names the patient's restricted or disputed record in the header", async () => {
    renderNew({
      'GET /api/patients/patient-1': () =>
        json({id: 'patient-1', firstName: 'Test', lastName: 'Patient', isRestricted: true, restrictionReason: 'Synthetic accuracy check', isDisputed: true}),
    });

    expect(await screen.findByRole('button', {name: 'Restricted record: changes paused, show details'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Disputed record, show details'})).toBeInTheDocument();
  });

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
    expect(screen.getByText('SNOMED CT: 71388002 | Procedure')).toBeInTheDocument();
    expect(screen.getByText('Imaging of the joint.')).toBeInTheDocument();
    // The scan involves a visit.
    expect(screen.getByText('2. Scan').parentElement).toHaveTextContent('2. ScanVisit required');
    expect(screen.getAllByText(/^Visit/)).toHaveLength(1);
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
    withAnnouncements();
    const {fetch, user, router} = renderNew({
      [`POST ${CREATE}`]: () => json({id: 'proc-new', patientId: 'patient-1', title: 'Knee review'}, 201),
    });

    await user.click(await screen.findByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'Knee review'}));
    await user.type(screen.getByRole('combobox', {name: /Due Date/}), '01/12/2099');
    await user.tab();
    await typeRichText(screen.getByRole('textbox', {name: /First note/}), 'Bring previous scans.');
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
    await expectAnnounced('Procedure created');
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

  it('says why a create failed for a reason other than its fields', async () => {
    const {user} = renderNew({[`POST ${CREATE}`]: () => json({title: 'Server error'}, 500)});

    await screen.findByText('For Test Patient');
    await user.click(screen.getByRole('button', {name: 'Create Procedure'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Procedure'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('creates nothing when the confirmation is cancelled', async () => {
    const {fetch, user} = renderNew();

    await screen.findByText('For Test Patient');
    await user.click(screen.getByRole('button', {name: 'Create Procedure'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(fetch.mock.calls.map(([r]) => (r as Request).method)).not.toContain('POST');
  });

  it("goes back to the patient's procedures", async () => {
    const {user, router} = renderNew();

    await user.click(await screen.findByRole('button', {name: 'Back'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients/patient-1/procedures'));
  });

  it('says when the templates could not be loaded', async () => {
    renderNew({[`GET ${TEMPLATES}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('puts the template and the details in one card in a half view', async () => {
    mockApi({
      'GET /api/patients/patient-1': () => json({id: 'patient-1', firstName: 'Test', lastName: 'Patient'}),
      [`GET ${TEMPLATES}`]: () => json(templates),
    });
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <NewProcedurePage patientId="patient-1" />
      </HalfViewContext.Provider>,
      {user: staffUser},
    );

    expect(await screen.findByRole('combobox', {name: /Template/})).toBeInTheDocument();
    expect(screen.getByRole('textbox', {name: /Title/})).toBeInTheDocument();
    // One form: its Template section has a header bar of its own.
    expect(screen.getByRole('heading', {name: 'Template'})).toBeInTheDocument();
  });

  it('says to start from a patient when there is none, and goes back to the procedures', async () => {
    const {user, router} = renderWithApp(<NewProcedurePage />, {user: staffUser});

    expect(await findAlert(/open the patient's procedures and choose New Procedure/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Back'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures'));
  });
});

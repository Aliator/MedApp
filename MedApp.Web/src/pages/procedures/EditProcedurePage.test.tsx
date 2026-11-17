import {screen, waitFor, within} from '@testing-library/react';
import {findAlert, json, mockApi, openDialog, renderWithApp, staffUser, expectAnnounced, withAnnouncements} from '../../test/render';
import {NOT_ASSIGNED_MESSAGE} from './ProcedurePage';
import {EditProcedurePage} from './EditProcedurePage';

const id = 'proc-1';
const PATH = `/api/procedures/patient-procedures/${id}`;

const procedure = (overrides: Record<string, unknown> = {}) => ({
  id,
  patientId: 'patient-1',
  patientName: 'Test Patient',
  title: 'Colonoscopy',
  notes: [{id: 'n1', authorUsername: 'test-user', text: 'Pack posted.', createdAtUtc: '2026-11-01T10:00:00Z', version: 1, canEdit: true}],
  dueDate: '2099-12-01',
  priority: 'High',
  status: 'InProgress',
  canEditDetails: true,
  assignments: [{userId: 'u1', username: staffUser.username}],
  stages: [],
  ...overrides,
});

function renderEdit(body = procedure(), handlers: Record<string, (request: Request) => Response | Promise<Response>> = {}) {
  const fetch = mockApi({[`GET ${PATH}`]: () => json(body), ...handlers});
  return {fetch, ...renderWithApp(<EditProcedurePage id={id} />, {path: `/procedures/${id}/edit`, user: staffUser})};
}

describe('EditProcedurePage', () => {
  it("shows the patient's banner, while editing and when it can't be edited", async () => {
    const patient = {id: 'patient-1', firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: null};
    renderEdit(procedure({patient}));

    const banner = await screen.findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('No NHS number')).toBeInTheDocument();
  });

  it("shows the patient's banner on a procedure that can't be edited", async () => {
    renderEdit(procedure({canEditDetails: false, status: 'Completed', patient: {firstName: 'Test', lastName: 'Patient'}}));

    expect(await screen.findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
  });

  it("starts from the procedure's details", async () => {
    renderEdit();

    expect(await screen.findByRole('textbox', {name: /Title/})).toHaveValue('Colonoscopy');
    // Notes are added and edited on the procedure's page, not here.
    expect(screen.queryByRole('textbox', {name: 'Notes'})).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', {name: /Priority/})).toHaveTextContent('High');
    expect(screen.getByText('For Test Patient')).toBeInTheDocument();
  });

  it('saves the changes after a confirmation, then goes back to the procedure', async () => {
    withAnnouncements();
    const {fetch, user, router} = renderEdit(procedure(), {[`PATCH ${PATH}`]: () => json(procedure({title: 'Colonoscopy (repeat)'}))});

    const title = await screen.findByRole('textbox', {name: /Title/});
    await user.type(title, ' (repeat)');
    await user.click(screen.getByRole('combobox', {name: /Priority/}));
    await user.click(screen.getByRole('option', {name: 'Urgent'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('Are you sure you want to save these changes?')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/procedures/${id}`));
    const patch = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PATCH')!;
    expect(await patch.json()).toEqual({title: 'Colonoscopy (repeat)', dueDate: '2099-12-01', priority: 'Urgent'});
    await expectAnnounced('Procedure saved');
  });

  it("asks the not-assigned question when saving a procedure that isn't yours", async () => {
    const {user} = renderEdit(procedure({assignments: [{userId: 'u9', username: 'someone.else'}]}));

    await user.click(await screen.findByRole('button', {name: 'Save'}));

    expect(within(openDialog()!).getByText(NOT_ASSIGNED_MESSAGE)).toBeInTheDocument();
  });

  it("explains a failed save, keeping what's typed", async () => {
    const {user} = renderEdit(procedure(), {[`PATCH ${PATH}`]: () => json({title: 'Server error'}, 500)});

    const title = await screen.findByRole('textbox', {name: /Title/});
    await user.type(title, '!');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    expect(title).toHaveValue('Colonoscopy!');
  });

  it("shows the API's errors on the fields", async () => {
    const {user} = renderEdit(procedure(), {
      [`PATCH ${PATH}`]: () => json({title: 'Validation failed', status: 400, errors: {Title: ['Title is required.']}}, 400),
    });

    await user.clear(await screen.findByRole('textbox', {name: /Title/}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(screen.getByRole('textbox', {name: /Title/})).toHaveAccessibleDescription(/Title is required\./));
  });

  it('stays on the form when the confirmation is cancelled', async () => {
    const {user, fetch} = renderEdit();

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(fetch.mock.calls.map(([r]) => (r as Request).method)).not.toContain('PATCH');
  });

  it('says when the procedure could not be loaded', async () => {
    renderEdit(procedure(), {[`GET ${PATH}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The procedure could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("says a cancelled procedure's details can't be edited", async () => {
    renderEdit(procedure({status: 'Cancelled', canEditDetails: false}));

    expect(await findAlert("This procedure is cancelled, so its details can't be edited.")).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
  });

  it('goes back to the list for an unknown procedure', async () => {
    mockApi({[`GET ${PATH}`]: () => json({title: 'Not found'}, 404)});
    const {router} = renderWithApp(<EditProcedurePage id={id} />, {path: `/procedures/${id}/edit`, user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures'));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {findAlert, json, mockApi, openDialog, renderWithApp, staffUser, expectAnnounced, withAnnouncements} from '../../test/render';
import {NOT_ASSIGNED_MESSAGE} from './PathwayPage';
import {EditPathwayPage} from './EditPathwayPage';
import {DUE_DATE_FROM_CLOCK} from './PathwayDetailsFields';

const id = 'proc-1';
const PATH = `/api/pathways/patient-pathways/${id}`;

const pathway = (overrides: Record<string, unknown> = {}) => ({
  id,
  patientId: 'patient-1',
  patientName: 'Test Patient',
  title: 'Colonoscopy',
  noteList: [{id: 'n1', authorUsername: 'test-user', text: 'Pack posted.', createdAtUtc: '2026-11-01T10:00:00Z', version: 1, canEdit: true}],
  dueDate: '2099-12-01',
  priority: 'High',
  status: 'InProgress',
  canEditDetails: true,
  assignments: [{userId: 'u1', username: staffUser.username}],
  stages: [],
  ...overrides,
});

function renderEdit(body = pathway(), handlers: Record<string, (request: Request) => Response | Promise<Response>> = {}) {
  const fetch = mockApi({[`GET ${PATH}`]: () => json(body), ...handlers});
  return {fetch, ...renderWithApp(<EditPathwayPage id={id} />, {path: `/pathways/${id}/edit`, user: staffUser})};
}

describe('EditPathwayPage', () => {
  it("shows the patient's banner, while editing and when it can't be edited", async () => {
    const patient = {id: 'patient-1', firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: null};
    renderEdit(pathway({patient}));

    const banner = await screen.findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('No NHS number')).toBeInTheDocument();
  });

  it("shows the patient's banner on a pathway that can't be edited", async () => {
    renderEdit(pathway({canEditDetails: false, status: 'Completed', patient: {firstName: 'Test', lastName: 'Patient'}}));

    expect(await screen.findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
  });

  it("starts from the pathway's details", async () => {
    renderEdit();

    expect(await screen.findByRole('textbox', {name: /Title/})).toHaveValue('Colonoscopy');
    // Notes are added and edited on the pathway's page, not here.
    expect(screen.queryByRole('textbox', {name: /note/i})).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', {name: /Priority/})).toHaveTextContent('High');
    expect(screen.getByText('For Test Patient')).toBeInTheDocument();
  });

  it('saves the changes after a confirmation, then goes back to the pathway', async () => {
    withAnnouncements();
    const {fetch, user, router} = renderEdit(pathway(), {[`PATCH ${PATH}`]: () => json(pathway({title: 'Colonoscopy (repeat)'}))});

    const title = await screen.findByRole('textbox', {name: /Title/});
    await user.type(title, ' (repeat)');
    await user.click(screen.getByRole('combobox', {name: /Priority/}));
    await user.click(screen.getByRole('option', {name: 'Urgent'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('Are you sure you want to save these changes?')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${id}`));
    const patch = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PATCH')!;
    expect(await patch.json()).toEqual({title: 'Colonoscopy (repeat)', dueDate: '2099-12-01', priority: 'Urgent'});
    await expectAnnounced('Pathway saved');
  });

  it('shows a target date set by the waiting-time clock read-only, and leaves it out of the save', async () => {
    const referral = {id: 'referral-1', clock: {state: 'Paused', targetDays: 28, daysWaited: 5}};
    const {fetch, user, router} = renderEdit(pathway({referral}), {[`PATCH ${PATH}`]: () => json(pathway({referral}))});

    const target = await screen.findByRole('textbox', {name: /Target Date/});
    expect(target).toHaveValue('1 December 2099');
    expect(target).toHaveAttribute('readonly');
    expect(screen.getByText(DUE_DATE_FROM_CLOCK)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save'}));
    const dialog = within(openDialog()!);
    expect(dialog.queryByText('1 December 2099')).not.toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${id}`));
    const patch = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PATCH')!;
    expect(await patch.json()).toEqual({title: 'Colonoscopy', priority: 'High'});
  });

  it('edits the target date by hand again once the clock has stopped', async () => {
    renderEdit(pathway({referral: {id: 'referral-1', clock: {state: 'Stopped', stopReason: 'StopStageFinished'}}}));

    expect(await screen.findByRole('combobox', {name: /Target Date/})).toBeInTheDocument();
    expect(screen.queryByText(DUE_DATE_FROM_CLOCK)).not.toBeInTheDocument();
  });

  it("asks the not-assigned question when saving a pathway that isn't yours", async () => {
    const {user} = renderEdit(pathway({assignments: [{userId: 'u9', username: 'someone.else'}]}));

    await user.click(await screen.findByRole('button', {name: 'Save'}));

    expect(within(openDialog()!).getByText(NOT_ASSIGNED_MESSAGE)).toBeInTheDocument();
  });

  it("explains a failed save, keeping what's typed", async () => {
    const {user} = renderEdit(pathway(), {[`PATCH ${PATH}`]: () => json({title: 'Server error'}, 500)});

    const title = await screen.findByRole('textbox', {name: /Title/});
    await user.type(title, '!');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    expect(title).toHaveValue('Colonoscopy!');
  });

  it("shows the API's errors on the fields", async () => {
    const {user} = renderEdit(pathway(), {
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

  it('says when the pathway could not be loaded', async () => {
    renderEdit(pathway(), {[`GET ${PATH}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The pathway could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("says a cancelled pathway's details can't be edited", async () => {
    renderEdit(pathway({status: 'Cancelled', canEditDetails: false}));

    expect(await findAlert("This pathway is cancelled, so its details can't be edited.")).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
  });

  it("names a restricted record in the header, and gives the restriction as why the details can't be edited", async () => {
    const readOnlyReason = "This patient's record is restricted, so this pathway can't be changed until an admin lifts the restriction.";
    renderEdit(pathway({canEditDetails: false, readOnlyReason, patientFlags: {isRestricted: true, restrictionReason: 'Synthetic accuracy check'}}));

    expect(await screen.findByRole('button', {name: 'Restricted record: changes paused, show details'})).toBeInTheDocument();
    expect(await findAlert(readOnlyReason)).toBeInTheDocument();
    expect(screen.queryByText(/This pathway is in progress/)).not.toBeInTheDocument();
  });

  it('names a disputed record in the header while editing', async () => {
    renderEdit(pathway({patientFlags: {isDisputed: true, disputeReason: 'Synthetic allergy'}}));

    expect(await screen.findByRole('button', {name: 'Disputed record: Synthetic allergy, show details'})).toBeInTheDocument();
    expect(await screen.findByRole('button', {name: 'Save'})).toBeInTheDocument();
  });

  it('goes back to the list for an unknown pathway', async () => {
    mockApi({[`GET ${PATH}`]: () => json({title: 'Not found'}, 404)});
    const {router} = renderWithApp(<EditPathwayPage id={id} />, {path: `/pathways/${id}/edit`, user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {NO_DATE} from '../../api/patients';
import {adminUser, calls, findAlert, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
import {ageOn} from '../../utils/dates';
import {AddPatientPage} from './AddPatientPage';
import {EditPatientPage} from './EditPatientPage';
import {ViewPatientPage} from './ViewPatientPage';

const id = 'f184a7b8-2585-4437-bcd4-e1700be38287';
const freya = {id, firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'};
const bodyOf = async (fetch: ReturnType<typeof mockApi>, index: number) => (fetch.mock.calls[index][0] as Request).json();

describe('ViewPatientPage', () => {
  it("shows the patient's details and a link to edit them", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya)});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await screen.findByText('1 December 1976')).toBeInTheDocument();
    expect(screen.getByText('Freya')).toBeInTheDocument();
    expect(screen.getByText(`${ageOn('1976-12-01')} years`)).toBeInTheDocument();
    expect(screen.getByText(id)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Edit Patient'})).toHaveAttribute('href', `/patients/${id}/edit`);
  });

  it('goes back to the list for an unknown patient', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({title: 'Not found'}, 404)});
    const {router} = renderWithApp(<ViewPatientPage id={id} />, {path: `/patients/${id}`, user: adminUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });
});

describe('AddPatientPage', () => {
  it('adds the patient after confirming, then goes back to the list', async () => {
    const fetch = mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user, router} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.type(await screen.findByLabelText(/^first name/i), 'Freya');
    await user.type(screen.getByLabelText(/^last name/i), 'Ahmed');
    await user.type(screen.getByLabelText(/^email address/i), 'freya.ahmed18@example.com');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(await bodyOf(fetch, 0)).toEqual({
      firstName: 'Freya',
      lastName: 'Ahmed',
      dateOfBirth: NO_DATE, // no date chosen: the API's "not provided" default, as Blazor sent
      email: 'freya.ahmed18@example.com',
    });
  });

  it("shows the API's validation errors on the fields and in a summary", async () => {
    mockApi({
      'POST /api/patients': () =>
        json({title: 'One or more validation errors occurred.', errors: {FirstName: ['First name must not be empty.']}}, 400),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    await findAlert('One or more validation errors occurred.');
    expect(screen.getAllByText('First name must not be empty.').length).toBeGreaterThan(1);
    expect(openDialog()).toBeNull();
  });
});

describe('EditPatientPage', () => {
  it('saves changes after confirming, then shows the patient', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`PATCH /api/patients/${id}`]: () => json({...freya, firstName: 'Fran'}),
    });
    const {user, router} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    const first = await screen.findByLabelText(/^first name/i);
    expect(first).toHaveValue('Freya');
    await user.clear(first);
    await user.type(first, 'Fran');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/patients/${id}`));
    expect(calls(fetch)).toContain(`PATCH /api/patients/${id}`);
    expect(await bodyOf(fetch, 1)).toEqual({...freya, id: undefined, firstName: 'Fran'});
  });

  it('deletes the patient after confirming, then goes to the list', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([], 0)),
      [`DELETE /api/patients/${id}`]: () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));
    const confirm = within(openDialog()!).getByRole('button', {name: 'Delete Patient'});
    await waitFor(() => expect(isDisabled(confirm)).toBe(false));
    await user.click(confirm);

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(calls(fetch)).toContain(
      `GET /api/procedures/patient-procedures/search?PatientId=${id}&IncludeClosed=true&PageSize=1`,
    );
    expect(calls(fetch)).toContain(`DELETE /api/patients/${id}`);
  });

  it('does not offer to delete a patient who has procedures', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([{}], 2)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));

    expect(await within(openDialog()!).findByText("This patient has 2 procedures, so they can't be deleted.")).toBeInTheDocument();
    expect(isDisabled(within(openDialog()!).getByRole('button', {name: 'Delete Patient'}))).toBe(true);
    expect(calls(fetch)).not.toContain(`DELETE /api/patients/${id}`);
  });

  it('shows why the API refused to delete the patient', async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([], 0)),
      [`DELETE /api/patients/${id}`]: () => json({title: "This patient has 1 procedure, so they can't be deleted.", status: 409}, 409),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));
    const confirm = within(openDialog()!).getByRole('button', {name: 'Delete Patient'});
    await waitFor(() => expect(isDisabled(confirm)).toBe(false));
    await user.click(confirm);

    await findAlert("This patient has 1 procedure, so they can't be deleted.");
  });

  it('says when the patient was deleted by someone else', async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`PATCH /api/patients/${id}`]: () => json({title: 'Patient not found.'}, 404),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await findAlert('Patient not found. It may have been deleted.');
  });
});

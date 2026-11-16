import {screen, waitFor, within} from '@testing-library/react';
import {NO_DATE} from '../../api/patients';
import {adminUser, calls, findAlert, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp, expectAnnounced, withAnnouncements} from '../../test/render';
import {ageOn} from '../../utils/dates';
import {AddPatientPage} from './AddPatientPage';
import {EditPatientPage} from './EditPatientPage';
import {PatientView, ViewPatientPage} from './ViewPatientPage';

const id = 'f184a7b8-2585-4437-bcd4-e1700be38287';
const freya = {id, firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'};
/** The patient's timeline: nothing due, and one entry. */
const timeline = {
  [`GET /api/patients/${id}/timeline/overview`]: () => json({upcoming: [], openCount: 0, procedures: []}),
  [`GET /api/patients/${id}/timeline`]: () =>
    json({
      items: [
        {
          id: '00000033-0000-0000-0000-000000000001',
          kind: 'Created',
          action: 'Created',
          atUtc: '2026-06-15T09:00:00Z',
          username: 'test.staff',
          procedureId: '00000033-0000-0000-0000-000000000002',
          procedureTitle: 'Knee assessment',
          isProcedureDeleted: false,
          changes: [],
        },
      ],
      nextCursor: null,
    }),
};
/** The body of the first request made with `method` (the forms also ask whether NHS number look-ups are set up). */
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string) =>
  (fetch.mock.calls.map(([request]) => request as Request).find((request) => request.method === method)!).json();

describe('ViewPatientPage', () => {
  it("shows the patient's details and a link to edit them", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await screen.findByText('1 December 1976')).toBeInTheDocument();
    expect(screen.getByText('Freya')).toBeInTheDocument();
    expect(screen.getByText(`${ageOn('1976-12-01')} years`)).toBeInTheDocument();
    expect(screen.getByText(id)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Edit Patient'})).toHaveAttribute('href', `/patients/${id}/edit`);
    expect(screen.getByText('Not given')).toBeInTheDocument();
  });

  it("shows the patient's banner above the details, and their NHS number", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000042'}), ...timeline});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    const banner = await screen.findByRole('region', {name: 'Patient'});
    expect(within(banner).getByText('AHMED, Freya')).toBeInTheDocument();
    expect(within(banner).getByText('NHS No. 999 000 0042')).toBeInTheDocument();
    // And in the details, under Personal Information.
    expect(screen.getAllByText('999 000 0042')).toHaveLength(1);
  });

  it('goes back to the list for an unknown patient', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({title: 'Not found'}, 404)});
    const {router} = renderWithApp(<ViewPatientPage id={id} />, {path: `/patients/${id}`, user: adminUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('says when the patient could not be loaded', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await findAlert('The patient could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('gives a one-year-old\'s age as "1 year"', async () => {
    vi.useFakeTimers({toFake: ['Date']});
    vi.setSystemTime(new Date('2026-11-15T12:00:00Z'));
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, dateOfBirth: '2025-06-01'})});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await screen.findByText('1 year')).toBeInTheDocument();
  });

  it('shows the timeline beside the details', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline});
    renderWithApp(<PatientView id={id} isNarrow={false} />, {user: adminUser});

    expect(await screen.findByText('Procedure created')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Timeline'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Personal Information'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Hide timeline'})).not.toBeInTheDocument();
  });

  it('narrow, folds the details and the timeline in one card, keeping its filters', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline});
    const {user} = renderWithApp(<PatientView id={id} isNarrow />, {user: adminUser});

    expect(await screen.findByText('Procedure created')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Notes'}));
    await user.click(screen.getByRole('button', {name: 'Hide details'}));

    expect(screen.queryByText('Freya')).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', {name: 'Hide timeline'}));
    expect(screen.queryByRole('button', {name: 'Notes'})).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Show timeline'}));
    expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('link', {name: 'Edit Patient'})).toHaveAttribute('href', `/patients/${id}/edit`);
  });
});

describe('AddPatientPage', () => {
  it('adds the patient after confirming, then goes back to the list', async () => {
    withAnnouncements();
    const fetch = mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user, router} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.type(await screen.findByLabelText(/^first name/i), 'Freya');
    await user.type(screen.getByLabelText(/^last name/i), 'Ahmed');
    await user.type(screen.getByLabelText(/^email address/i), 'freya.ahmed18@example.com');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(await bodyOf(fetch, 'POST')).toEqual({
      firstName: 'Freya',
      lastName: 'Ahmed',
      dateOfBirth: NO_DATE, // no date chosen: the API's "not provided" default, as Blazor sent
      email: 'freya.ahmed18@example.com',
    });
    await expectAnnounced('Patient added');
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
    // On the field, and said by Astryx's live region, which can come a moment later.
    await waitFor(() => expect(screen.getAllByText('First name must not be empty.').length).toBeGreaterThan(1));
    expect(openDialog()).toBeNull();
  });

  it('shows the date of birth typed in the confirmation, and sends none once it is cleared', async () => {
    const fetch = mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    const dateOfBirth = await screen.findByRole('combobox', {name: /Date of Birth/});
    await user.type(dateOfBirth, '01/12/1976');
    await user.tab();
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(within(openDialog()!).getByText('1 December 1976')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    await user.clear(dateOfBirth);
    await user.tab();
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    expect((await bodyOf(fetch, 'POST')).dateOfBirth).toBe(NO_DATE);
  });

  it('says why an add failed for a reason other than its fields, and announces no add', async () => {
    withAnnouncements();
    mockApi({'POST /api/patients': () => json({title: 'Server error'}, 500)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(screen.getByTestId('announcer')).not.toHaveTextContent('Patient added');
  });

  it('goes back to the list when cancelled', async () => {
    const {user, router} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });
});

describe('EditPatientPage', () => {
  it.each([
    ['restricted', {isRestricted: true}, "This patient's record is restricted, so it can't be changed until an admin lifts the restriction."],
    ['anonymised', {isAnonymised: true}, "This patient's record has been anonymised, so it can't be changed."],
  ])('says before anyone tries that a %s record cannot be changed', async (_, flags, message) => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, ...flags})});
    renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it('saves changes after confirming, then shows the patient', async () => {
    withAnnouncements();
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
    // The NHS number is always sent, "" when there's none, so a removed one is removed.
    expect(await bodyOf(fetch, 'PATCH')).toEqual({...freya, id: undefined, firstName: 'Fran', nhsNumber: ''});
    await expectAnnounced('Patient saved');
  });

  it('deletes the patient after confirming, then goes to the list', async () => {
    withAnnouncements();
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
    await expectAnnounced('Patient deleted');
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

  it("shows the API's validation errors on the fields", async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`PATCH /api/patients/${id}`]: () => json({title: 'One or more validation errors occurred.', errors: {Email: ['Email is not valid.']}}, 400),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await findAlert('One or more validation errors occurred.');
    expect(screen.getByLabelText(/^email address/i)).toHaveAccessibleDescription(/Email is not valid\./);
  });

  it('makes no change when either confirmation is cancelled', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([{}], 1)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    await user.click(screen.getByRole('button', {name: 'Delete Patient'}));
    expect(await within(openDialog()!).findByText("This patient has 1 procedure, so they can't be deleted.")).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    expect(calls(fetch).filter((c) => !c.startsWith('GET'))).toEqual([]);
  });

  it('goes back to the list for an unknown patient', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({title: 'Not found'}, 404)});
    const {router} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('says when the patient could not be loaded', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    expect(await findAlert('The patient could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

describe('Patient forms, NHS number', () => {
  // Synthetic test numbers only (the 999 range is never issued to real patients).
  const STATUS = 'GET /api/demographics/status';
  const lookUpAvailable = {[STATUS]: () => json({isAvailable: true, source: 'Offline test data'})};
  const nhsField = () => screen.findByRole('textbox', {name: /NHS Number/});

  it("sends a new patient's NHS number without its spaces, and shows it in the confirmation", async () => {
    const fetch = mockApi({'POST /api/patients': () => json({...freya, nhsNumber: '9990000018'}, 201)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '999 000 0018');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(within(openDialog()!).getByText('999 000 0018')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/patients'));
    expect((await bodyOf(fetch, 'POST')).nhsNumber).toBe('9990000018');
  });

  it('says "Not given" in the confirmation without one', async () => {
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));

    expect(within(openDialog()!).getByText('Not given')).toBeInTheDocument();
  });

  it("shows the API's NHS number error on the field, not in the summary", async () => {
    mockApi({
      'POST /api/patients': () =>
        json({title: 'One or more validation errors occurred.', errors: {NhsNumber: ['The NHS number is not valid.']}}, 400),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    const alert = await findAlert('One or more validation errors occurred.');
    expect(alert).not.toHaveTextContent('The NHS number is not valid.');
    expect(await nhsField()).toHaveAccessibleDescription(/The NHS number is not valid\./);
  });

  it('says when another patient already has the NHS number', async () => {
    const message = 'Another patient already has this NHS number. Check you have the right patient.';
    mockApi({'POST /api/patients': () => json({title: message, status: 409}, 409)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '9990000026');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Add'}));

    expect(await findAlert(message)).toBeInTheDocument();
  });

  it('offers no look-up when it is not set up', async () => {
    const fetch = mockApi({[STATUS]: () => json({isAvailable: false, source: null})});
    renderWithApp(<AddPatientPage />, {user: adminUser});

    await nhsField();
    await waitFor(() => expect(calls(fetch)).toContain(STATUS));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.queryByRole('button', {name: 'Look up'})).not.toBeInTheDocument();
  });

  it('looks the number up, fills in the name and date of birth, keeps the number, and says so', async () => {
    withAnnouncements();
    const fetch = mockApi({
      ...lookUpAvailable,
      'POST /api/demographics/lookup': () =>
        json({nhsNumber: '9990000034', firstName: 'Oriel', lastName: 'Penhallow', dateOfBirth: '1951-03-14', source: 'Offline test data'}),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '999 000 0034');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));

    await waitFor(() => expect(screen.getByLabelText(/^first name/i)).toHaveValue('Oriel'));
    expect(screen.getByLabelText(/^last name/i)).toHaveValue('Penhallow');
    expect(screen.getByRole('combobox', {name: /Date of Birth/})).toHaveValue('14 March 1951');
    expect(await nhsField()).toHaveValue('999 000 0034');
    expect(calls(fetch)).toContain('POST /api/demographics/lookup');
    // The number goes in the body, without its spaces, never in the URL.
    expect(await bodyOf(fetch, 'POST')).toEqual({nhsNumber: '9990000034'});
    const found = /Found: name and date of birth filled in from Offline test data\. Check they're right\./;
    expect(await nhsField()).toHaveAccessibleDescription(found);
    await expectAnnounced(found);
  });

  it('keeps what was typed when the look-up comes back after the number changed', async () => {
    let answer: (response: Response) => void = () => {};
    mockApi({
      ...lookUpAvailable,
      'POST /api/demographics/lookup': () => new Promise<Response>((resolve) => (answer = resolve)),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    const field = await nhsField();
    await user.type(field, '9990000042');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));
    await user.clear(field);
    await user.type(field, '9990000050');
    answer(json({nhsNumber: '9990000042', firstName: 'Oriel', lastName: 'Penhallow', dateOfBirth: '1951-03-14', source: ''}));

    await waitFor(() => expect(isDisabled(screen.getByRole('button', {name: 'Look up'}))).toBe(false));
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('');
    expect(field).not.toHaveAccessibleDescription(/Found/);
  });

  it.each([
    [
      'nobody has it',
      () => json({title: 'Nobody with this NHS number was found. Check the number, or enter their details yourself.'}, 404),
      /Nobody with this NHS number was found/,
    ],
    ['the answer has no reason', () => json({}, 404), /Nobody with this NHS number was found/],
    ['the look-up fails', () => json({title: 'Server error'}, 500), /The NHS number couldn't be looked up/],
  ])('says so when %s, and changes nothing', async (_, answer, message) => {
    withAnnouncements();
    mockApi({...lookUpAvailable, 'POST /api/demographics/lookup': answer});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '9990000069');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));

    await waitFor(async () => expect(await nhsField()).toHaveAccessibleDescription(message));
    await expectAnnounced(message);
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('');
  });

  it("shows the patient's NHS number to edit, and removes it when cleared", async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000077'}),
      [`PATCH /api/patients/${id}`]: () => json(freya),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    const field = await nhsField();
    expect(field).toHaveValue('9990000077');
    await user.clear(field);
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(calls(fetch)).toContain(`PATCH /api/patients/${id}`));
    expect((await bodyOf(fetch, 'PATCH')).nhsNumber).toBe('');
  });

  it("shows the patient's NHS number when asking to delete them", async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000077'}),
      'GET /api/procedures/patient-procedures/search': () => json(pageOf([], 0)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));

    expect(within(openDialog()!).getByText('999 000 0077')).toBeInTheDocument();
  });
});

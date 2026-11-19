import {screen, waitFor, within} from '@testing-library/react';
import {NO_DATE} from '../../api/patients';
import {adminUser, staffUser, userWithRoles, calls, findAlert, isDisabled, openAlertDetails, json, mockApi, openDialog, pageOf, renderWithApp, expectAnnounced, withAnnouncements} from '../../test/render';
import {ageOn} from '../../utils/dates';
import {AddPatientPage} from './AddPatientPage';
import {EditPatientPage} from './EditPatientPage';
import {PatientView, splitAppointments, ViewPatientPage} from './ViewPatientPage';
import {testAppointment, testRequested} from '../../test/calendar';

const id = 'f184a7b8-2585-4437-bcd4-e1700be38287';
const freya = {id, firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'};
/** The patient's timeline: nothing due, and one entry. */
const timeline = {
  [`GET /api/patients/${id}/timeline/overview`]: () => json({upcoming: [], openCount: 0, pathways: []}),
  [`GET /api/patients/${id}/timeline`]: () =>
    json({
      items: [
        {
          id: '00000033-0000-0000-0000-000000000001',
          kind: 'Created',
          action: 'Created',
          atUtc: '2026-06-15T09:00:00Z',
          username: 'test.staff',
          pathwayId: '00000033-0000-0000-0000-000000000002',
          pathwayTitle: 'Knee assessment',
          isPathwayDeleted: false,
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
    expect(screen.getAllByText('Freya').length).toBeGreaterThan(0);
    expect(screen.getByText(`${ageOn('1976-12-01')} years`)).toBeInTheDocument();
    expect(screen.getByText(id)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Edit Patient'})).toHaveAttribute('href', `/patients/${id}/edit`);
    // The NHS number, mobile, address and GP practice.
    expect(screen.getAllByText('Not given')).toHaveLength(4);
    // No reminder choice yet: the API's default.
    expect(screen.getByText('No reminders')).toBeInTheDocument();
  });

  it("shows the patient's reminder choice in words", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, reminderChannel: 'TextAndEmail'}), ...timeline});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await screen.findByText('Text and email')).toBeInTheDocument();
  });

  it("shows the patient's mobile and address, and their GP practice from the directory", async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () =>
        json({
          ...freya,
          addressLines: ['1 Test Street', 'Testbrook'],
          postcode: 'ZZ1 1ZZ',
          mobilePhone: '+447700900123',
          practiceId: 'p-test',
          practice: {id: 'p-test', name: 'Testbrook Surgery', odsCode: 'ZZZ123', addressLines: ['2 Surgery Lane'], postcode: 'ZZ2 2ZZ', phone: '01632 960001', isArchived: true},
        }),
      ...timeline,
    });
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    expect(await screen.findByText('07700 900123')).toBeInTheDocument();
    expect(screen.getByText('1 Test Street')).toBeInTheDocument();
    expect(screen.getByText('Testbrook')).toBeInTheDocument();
    expect(screen.getByText('ZZ1 1ZZ')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'GP Practice'})).toBeInTheDocument();
    expect(screen.getByText('Testbrook Surgery (archived)')).toBeInTheDocument();
    expect(screen.getByText('ZZZ123')).toBeInTheDocument();
    expect(screen.getByText('2 Surgery Lane')).toBeInTheDocument();
    expect(screen.getByText('01632 960001')).toBeInTheDocument();
  });

  it('shows who the patient is first, under Personal Information, with their NHS number, and no banner', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000042'}), ...timeline});
    renderWithApp(<ViewPatientPage id={id} />, {user: adminUser});

    // Personal Information is the details' first part, before the contact details and the GP practice.
    const personal = await screen.findByRole('heading', {name: 'Personal Information'});
    const sections = screen.getAllByRole('heading').filter((h) => /Information|GP Practice/.test(h.textContent ?? ''));
    expect(sections[0]).toBe(personal);
    expect(screen.getAllByText('Freya').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Ahmed').length).toBeGreaterThan(0);
    expect(screen.getAllByText('999 000 0042')).toHaveLength(1);
    expect(screen.queryByRole('region', {name: 'Patient: Ahmed, Freya'})).not.toBeInTheDocument();
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

    expect(await screen.findByText('Pathway created')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Timeline'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Personal Information'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Hide timeline'})).not.toBeInTheDocument();
  });

  it('narrow, puts the details and the timeline in one card, the details never folding, the timeline keeping its filters', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline});
    const {user} = renderWithApp(<PatientView id={id} isNarrow />, {user: adminUser});

    expect(await screen.findByText('Pathway created')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Notes'}));
    // The details never fold: who the patient is stays in view.
    expect(screen.queryByRole('button', {name: 'Hide details'})).not.toBeInTheDocument();
    expect(screen.getAllByText('Freya').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', {name: 'Hide timeline'}));
    expect(screen.queryByRole('button', {name: 'Notes'})).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Show timeline'}));
    expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('link', {name: 'Edit Patient'})).toHaveAttribute('href', `/patients/${id}/edit`);
  });
});

describe('ViewPatientPage: appointments', () => {
  const APPOINTMENTS = '/api/appointments/search';
  // In winter, UK time is UTC.
  const soon = testAppointment({id: 'soon', date: '2026-11-23', start: '10:00', end: '10:30', patientId: id});
  const later = testAppointment({id: 'later', date: '2026-11-30', start: '09:00', end: '09:30', patientId: id, pathwayId: null, pathwayTitle: null, stageName: null});
  const waiting = testRequested({id: 'waiting', patientId: id});
  const past = (n: number, status = 'Attended') => testAppointment({id: `past-${n}`, status, date: `2026-10-0${n}`, patientId: id});

  function renderPatient(appointments: unknown[] | Response | undefined) {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`GET ${APPOINTMENTS}`]: () =>
        appointments === undefined
          ? new Promise<Response>(() => {})
          : appointments instanceof Response
            ? appointments
            : json(pageOf(appointments)),
      ...timeline,
    });
    return {fetch, ...renderWithApp(<PatientView id={id} isNarrow={false} />, {user: adminUser})};
  }

  it('lists what is still to happen, soonest first, then the last few that are over', async () => {
    const {fetch} = renderPatient([past(1), waiting, later, past(2, 'DidNotAttend'), soon, past(3, 'Cancelled'), past(4)]);

    const upcoming = within(await screen.findByRole('list', {name: 'Upcoming appointments'}));
    expect(upcoming.getAllByRole('link').map((l) => l.textContent)).toEqual([
      'Mon 23 Nov 2026, 10:00 to 10:30 with test-user',
      'Mon 30 Nov 2026, 09:00 to 09:30 with test-user',
      'Not booked yet',
    ]);
    expect(upcoming.getAllByText('Knee review, Assessment')).toHaveLength(2);
    const earlier = within(screen.getByRole('list', {name: 'Past and cancelled appointments'}));
    expect(earlier.getAllByRole('link').map((l) => l.getAttribute('href'))).toEqual([
      '/calendar?appointment=past-4',
      '/calendar?appointment=past-3',
      '/calendar?appointment=past-2',
    ]);
    expect(screen.getByText('And 1 more: the timeline has them all.')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${APPOINTMENTS}?PatientId=${id}&PageSize=100`);
  });

  it('says when there are none', async () => {
    renderPatient([]);
    expect(await screen.findByText('No appointments yet.')).toBeInTheDocument();
  });

  it('says when nothing is still to happen', async () => {
    renderPatient([past(1)]);
    expect(await screen.findByText('Nothing booked or waiting to be booked.')).toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    renderPatient(json({title: 'Down'}, 500));
    expect(await findAlert('The appointments could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('books an appointment for the patient from the footer', async () => {
    const {user} = renderPatient([]);

    await user.click(await screen.findByRole('button', {name: 'Book Appointment'}));

    expect(within(openDialog()!).getByText('Book Appointment')).toBeInTheDocument();
    // The patient's banner heads it, from the page's copy of the patient (no picker).
    expect(within(openDialog()!).getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(within(openDialog()!).queryByRole('combobox', {name: /Patient/})).not.toBeInTheDocument();
  });

  it("doesn't offer booking while the record is restricted or anonymised", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, isRestricted: true}), [`GET ${APPOINTMENTS}`]: () => json(pageOf([])), ...timeline});
    renderWithApp(<PatientView id={id} isNarrow={false} />, {user: adminUser});

    expect(await screen.findByRole('button', {name: 'Restricted record: changes paused, show details'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Pathways'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book Appointment'})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Edit Patient'})).not.toBeInTheDocument();
  });

  it('books from the footer in a narrow window too, and closes the booking', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), [`GET ${APPOINTMENTS}`]: () => json(pageOf([])), ...timeline});
    const {user} = renderWithApp(<PatientView id={id} isNarrow />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Book Appointment'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('says while the appointments load', async () => {
    renderPatient(undefined);
    expect(await screen.findByText('Loading appointments...')).toBeInTheDocument();
  });

  it('orders past ones without a time by when they last changed', () => {
    const cancelledRequest = {...waiting, id: 'c', status: 'Cancelled', lastUpdated: '2026-11-01T10:00:00Z'};
    const noDate = {...waiting, id: 'n', status: 'Cancelled', lastUpdated: undefined};
    expect(splitAppointments([past(1), noDate, cancelledRequest]).past.map((a) => a.id)).toEqual(['c', 'past-1', 'n']);
  });
});

describe('ViewPatientPage: files', () => {
  const files = {
    'GET /api/attachments': () =>
      json({
        items: [
          {
            id: '00000033-0000-0000-0000-000000000009',
            patientId: id,
            attachedTo: 'Pathway',
            pathwayId: '00000033-0000-0000-0000-000000000002',
            pathwayTitle: 'Knee assessment',
            fileName: 'referral.pdf',
            contentType: 'application/pdf',
            typeName: 'PDF',
            sizeBytes: 2048,
            scanStatus: 'Clean',
            hasThumbnail: false,
            uploadedByUsername: 'test.staff',
            uploadedAtUtc: '2026-11-03T14:05:00Z',
            canDelete: true,
            isDeleted: false,
          },
        ],
        totalCount: 1,
        maxFileBytes: 20 * 1024 * 1024,
        allowedExtensions: ['.pdf'],
      }),
  };

  it("lists all the patient's files under their details, where files can be added to the patient", async () => {
    const fetch = mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline, ...files});
    renderWithApp(<PatientView id={id} isNarrow={false} />, {user: staffUser});

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Files'})).toBeInTheDocument();
    expect(screen.getByText(/^Attached to/)).toHaveTextContent('Attached to Knee assessment');
    expect(screen.getByRole('button', {name: 'Add files'})).toBeInTheDocument();
    // A pathway's file is deleted from the pathway, where its rules apply.
    expect(screen.queryByRole('button', {name: 'Delete referral.pdf'})).not.toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET /api/attachments?PatientId=${id}`);
  });

  it('narrow, folds the files as a section of their own', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya), ...timeline, ...files});
    const {user} = renderWithApp(<PatientView id={id} isNarrow />, {user: staffUser});

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Hide files'}));

    expect(screen.queryByText('referral.pdf')).not.toBeInTheDocument();
    expect(screen.getAllByText('Freya').length).toBeGreaterThan(0);
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
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(await bodyOf(fetch, 'POST')).toEqual({
      firstName: 'Freya',
      lastName: 'Ahmed',
      dateOfBirth: NO_DATE, // no date chosen: the API's "not provided" default, as Blazor sent
      email: 'freya.ahmed18@example.com',
    });
    await expectAnnounced('Patient added');
  });

  it('adds the contact details and GP practice chosen, the mobile and postcode checked once each field is left', async () => {
    const fetch = mockApi({
      'POST /api/patients': () => json(freya, 201),
      'GET /api/practices': () =>
        json([
          {id: 'p-test', name: 'Testbrook Surgery', odsCode: 'ZZZ123', addressLines: ['2 Surgery Lane'], postcode: 'ZZ2 2ZZ', isArchived: false},
          {id: 'p-old', name: 'Oldfield Practice', isArchived: true},
        ]),
    });
    const {user} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.type(await screen.findByLabelText(/^first name/i), 'Freya');
    const mobile = screen.getByRole('textbox', {name: /^Mobile/});
    await user.type(mobile, '01632 960123');
    expect(mobile).not.toHaveAccessibleDescription(/Enter a UK mobile/);
    await user.tab();
    expect(mobile).toHaveAccessibleDescription(/Enter a UK mobile number/);
    const postcode = screen.getByRole('textbox', {name: /^Postcode/});
    await user.type(postcode, 'zz1 1z');
    await user.tab();
    expect(postcode).toHaveAccessibleDescription(/Enter a UK postcode/);

    // Saving with either wrong sends nothing: both show on their fields.
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(openDialog()).toBeNull();

    await user.clear(mobile);
    await user.type(mobile, '+44 7700 900123');
    await user.type(postcode, 'z');
    await user.type(screen.getByRole('textbox', {name: /^Address line 1/}), '1 Test Street');
    await user.type(screen.getByRole('textbox', {name: /^Address line 3/}), 'Testbrook');
    const practice = screen.getByRole('combobox', {name: /^Practice/});
    await user.click(practice);
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['None', 'Testbrook Surgery (ZZZ123)', 'Not listed here']);
    await user.click(screen.getByRole('option', {name: 'Testbrook Surgery (ZZZ123)'}));
    expect(practice).toHaveAccessibleDescription(/2 Surgery Lane, ZZ2 2ZZ/);
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/patients'));
    expect(await bodyOf(fetch, 'POST')).toMatchObject({
      addressLines: ['1 Test Street', 'Testbrook'],
      postcode: 'zz1 1zz',
      mobilePhone: '+44 7700 900123',
      practiceId: 'p-test',
    });
  });

  it('sends the reminder choice when one is chosen, warning without blocking when there is no mobile for texts', async () => {
    const fetch = mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.type(await screen.findByLabelText(/^first name/i), 'Freya');
    await user.type(screen.getByLabelText(/^last name/i), 'Ahmed');
    await user.type(screen.getByLabelText(/^email address/i), 'freya.ahmed18@example.com');
    const reminders = screen.getByRole('combobox', {name: /^Reminders/});
    expect(reminders).toHaveAccessibleDescription(/Texts can't be replied to/);
    await user.click(reminders);
    await user.click(await screen.findByRole('option', {name: 'Text and email'}));
    expect(reminders).toHaveAccessibleDescription(/Texts can’t be sent without a mobile number/);
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/patients'));
    expect(await bodyOf(fetch, 'POST')).toMatchObject({reminderChannel: 'TextAndEmail'});
  });

  it("shows the API's validation errors on the fields and in a summary", async () => {
    mockApi({
      'POST /api/patients': () =>
        json({title: 'One or more validation errors occurred.', errors: {FirstName: ['First name must not be empty.']}}, 400),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await findAlert('One or more validation errors occurred.');
    // On the field, and said by Astryx's live region, which can come a moment later.
    await waitFor(() => expect(screen.getAllByText('First name must not be empty.').length).toBeGreaterThan(1));
    expect(openDialog()).toBeNull();
  });

  it('shows the date of birth typed in the confirmation, and sends none once it is cleared', async () => {
    const fetch = mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    const dateOfBirth = await screen.findByRole('combobox', {name: /Date of birth/});
    await user.type(dateOfBirth, '01/12/1976');
    await user.tab();
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(within(openDialog()!).getByText('1 December 1976')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    await user.clear(dateOfBirth);
    await user.tab();
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    expect((await bodyOf(fetch, 'POST')).dateOfBirth).toBe(NO_DATE);
  });

  it('says why an add failed for a reason other than its fields, and announces no add', async () => {
    withAnnouncements();
    mockApi({'POST /api/patients': () => json({title: 'Server error'}, 500)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(screen.getByTestId('announcer')).not.toHaveTextContent('Patient added');
  });

  it('goes back to the list when cancelled', async () => {
    const {user, router} = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('goes back to Register Referral with the new patient chosen, when adding them for a referral', async () => {
    mockApi({'POST /api/patients': () => json(freya, 201)});
    const {user, router} = renderWithApp(<AddPatientPage then="referral" />, {path: '/patients/add?then=referral', user: staffUser});

    expect(await screen.findByRole('link', {name: /Back/})).toHaveAttribute('href', '/referrals/new');
    await user.type(await screen.findByLabelText(/^first name/i), 'Freya');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals/new'));
    expect(router.state.location.search).toEqual({patientId: id});
  });

  it('cancels back to Register Referral when adding a patient for a referral', async () => {
    const {user, router} = renderWithApp(<AddPatientPage then="referral" />, {path: '/patients/add?then=referral', user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals/new'));
  });
});

describe('EditPatientPage', () => {
  it.each([
    ['restricted', {isRestricted: true, restrictionReason: 'Synthetic accuracy check'}, 'Restricted record', /^Restricted record: nothing about this patient/],
    ['anonymised', {isAnonymised: true}, 'Anonymised record', /^This record has been anonymised/],
  ])('says before anyone tries that a %s record cannot be changed, with its banner', async (_, flags, label, message) => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, ...flags})});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    expect(within(await openAlertDetails(user, label)).getByText(message)).toBeInTheDocument();
  });

  it('offers Delete Patient only to someone whose roles allow it (DeletePatients)', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya)});
    renderWithApp(<EditPatientPage id={id} />, {user: userWithRoles('test-nurse', 'Nurse')});

    expect(await screen.findByRole('button', {name: 'Save'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete Patient'})).not.toBeInTheDocument();
  });

  it("shows a dispute's banner, saying what the patient disputes", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, isDisputed: true, disputeReason: 'Synthetic date of birth'})});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    expect(within(await openAlertDetails(user, 'Disputed record')).getByText("What's disputed: Synthetic date of birth")).toBeInTheDocument();
  });

  it('says when someone else changed the record while it was being edited', async () => {
    const changed = "Someone else changed this patient's record while you were working on it. Reload it to see their change, then try again.";
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`PATCH /api/patients/${id}`]: () => json({title: changed, status: 409}, 409),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    await screen.findByLabelText(/^first name/i);
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await findAlert(changed);
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

  it('starts from the contact details and practice, and sends only those changed', async () => {
    const withContact = {
      ...freya,
      addressLines: ['1 Test Street'],
      postcode: 'ZZ1 1ZZ',
      mobilePhone: '+447700900123',
      practiceId: 'p-old',
      practice: {id: 'p-old', name: 'Oldfield Practice', isArchived: true},
    };
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(withContact),
      'GET /api/practices': () => json([{id: 'p-test', name: 'Testbrook Surgery', isArchived: false}]),
      [`PATCH /api/patients/${id}`]: () => json(withContact),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    expect(await screen.findByRole('textbox', {name: /^Mobile/})).toHaveValue('07700 900123');
    expect(screen.getByRole('textbox', {name: /^Address line 1/})).toHaveValue('1 Test Street');
    expect(screen.getByRole('textbox', {name: /^Postcode/})).toHaveValue('ZZ1 1ZZ');
    // Their practice, archived since, is still theirs; nobody can newly choose it.
    const practice = screen.getByRole('combobox', {name: /^Practice/});
    await waitFor(() => expect(practice).toHaveTextContent('Oldfield Practice (archived)'));

    // Changing nothing in the contact details sends none of them, so the archived practice isn't sent as newly chosen.
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));
    await waitFor(() => expect(calls(fetch)).toContain(`PATCH /api/patients/${id}`));
    const unchanged = await bodyOf(fetch, 'PATCH');
    expect(unchanged).not.toHaveProperty('addressLines');
    expect(unchanged).not.toHaveProperty('postcode');
    expect(unchanged).not.toHaveProperty('mobilePhone');
    expect(unchanged).not.toHaveProperty('practiceId');
    expect(unchanged).not.toHaveProperty('reminderChannel');
  });

  it("starts from the patient's reminder choice, and sends it only once it's changed", async () => {
    const withChoice = {...freya, mobilePhone: '+447700900123', reminderChannel: 'Text'};
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(withChoice),
      [`PATCH /api/patients/${id}`]: () => json(withChoice),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    const reminders = await screen.findByRole('combobox', {name: /^Reminders/});
    expect(reminders).toHaveTextContent('Text');
    // The patient asked for no more texts.
    await user.click(reminders);
    await user.click(await screen.findByRole('option', {name: 'No reminders'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(calls(fetch)).toContain(`PATCH /api/patients/${id}`));
    expect(await bodyOf(fetch, 'PATCH')).toMatchObject({reminderChannel: 'None'});
  });

  it('removes the practice with the empty ID, and the address and postcode as empty', async () => {
    const withContact = {...freya, addressLines: ['1 Test Street'], postcode: 'ZZ1 1ZZ', practiceId: 'p-test', practice: {id: 'p-test', name: 'Testbrook Surgery', isArchived: false}};
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(withContact),
      'GET /api/practices': () => json([{id: 'p-test', name: 'Testbrook Surgery', isArchived: false}]),
      [`PATCH /api/patients/${id}`]: () => json(freya),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {path: `/patients/${id}/edit`, user: adminUser});

    await user.clear(await screen.findByRole('textbox', {name: /^Address line 1/}));
    await user.clear(screen.getByRole('textbox', {name: /^Postcode/}));
    await user.click(screen.getByRole('combobox', {name: /^Practice/}));
    await user.click(await screen.findByRole('option', {name: 'None'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(calls(fetch)).toContain(`PATCH /api/patients/${id}`));
    expect(await bodyOf(fetch, 'PATCH')).toMatchObject({addressLines: [], postcode: '', practiceId: '00000000-0000-0000-0000-000000000000'});
  });

  it('deletes the patient after confirming, then goes to the list', async () => {
    withAnnouncements();
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/pathways/patient-pathways/search': () => json(pageOf([], 0)),
      [`DELETE /api/patients/${id}`]: () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));
    const confirm = within(openDialog()!).getByRole('button', {name: 'Delete Patient'});
    await waitFor(() => expect(isDisabled(confirm)).toBe(false));
    await user.click(confirm);

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
    expect(calls(fetch)).toContain(
      `GET /api/pathways/patient-pathways/search?PatientId=${id}&IncludeClosed=true&PageSize=1`,
    );
    expect(calls(fetch)).toContain(`DELETE /api/patients/${id}`);
    await expectAnnounced('Patient deleted');
  });

  it('does not offer to delete a patient who has pathways', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/pathways/patient-pathways/search': () => json(pageOf([{}], 2)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));

    expect(await within(openDialog()!).findByText("This patient has 2 pathways, so they can't be deleted.")).toBeInTheDocument();
    expect(isDisabled(within(openDialog()!).getByRole('button', {name: 'Delete Patient'}))).toBe(true);
    expect(calls(fetch)).not.toContain(`DELETE /api/patients/${id}`);
  });

  it('shows why the API refused to delete the patient', async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      'GET /api/pathways/patient-pathways/search': () => json(pageOf([], 0)),
      [`DELETE /api/patients/${id}`]: () => json({title: "This patient has 1 pathway, so they can't be deleted.", status: 409}, 409),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));
    const confirm = within(openDialog()!).getByRole('button', {name: 'Delete Patient'});
    await waitFor(() => expect(isDisabled(confirm)).toBe(false));
    await user.click(confirm);

    await findAlert("This patient has 1 pathway, so they can't be deleted.");
  });

  it('says when the patient was deleted by someone else', async () => {
    mockApi({
      [`GET /api/patients/${id}`]: () => json(freya),
      [`PATCH /api/patients/${id}`]: () => json({title: 'Patient not found.'}, 404),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    await findAlert("It can't be found: someone else may have deleted the patient.");
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
      'GET /api/pathways/patient-pathways/search': () => json(pageOf([{}], 1)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    await user.click(screen.getByRole('button', {name: 'Delete Patient'}));
    expect(await within(openDialog()!).findByText("This patient has 1 pathway, so they can't be deleted.")).toBeInTheDocument();
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
  const nhsField = () => screen.findByRole('textbox', {name: /NHS number/});

  it("sends a new patient's NHS number without its spaces, and shows it in the confirmation", async () => {
    const fetch = mockApi({'POST /api/patients': () => json({...freya, nhsNumber: '9990000018'}, 201)});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '999 000 0018');
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(within(openDialog()!).getByText('999 000 0018')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

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
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

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
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));

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
    expect(screen.getByRole('combobox', {name: /Date of birth/})).toHaveValue('14 March 1951');
    expect(await nhsField()).toHaveValue('999 000 0034');
    expect(calls(fetch)).toContain('POST /api/demographics/lookup');
    // The number goes in the body, without its spaces, never in the URL.
    expect(await bodyOf(fetch, 'POST')).toEqual({nhsNumber: '9990000034'});
    const found = /Found: name and date of birth filled in from Offline test data\. Check they're right\./;
    expect(await nhsField()).toHaveAccessibleDescription(found);
    await expectAnnounced(found);
  });

  it('fills in the address, postcode and mobile too when the service has them', async () => {
    withAnnouncements();
    mockApi({
      ...lookUpAvailable,
      'POST /api/demographics/lookup': () =>
        json({
          nhsNumber: '9990000034',
          firstName: 'Oriel',
          lastName: 'Penhallow',
          dateOfBirth: '1951-03-14',
          addressLines: ['3 Synthetic Row', 'Testbrook'],
          postcode: 'ZZ3 3ZZ',
          mobilePhone: '+447700900999',
          source: 'Offline test data',
        }),
    });
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    await user.type(await nhsField(), '999 000 0034');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));

    await waitFor(() => expect(screen.getByRole('textbox', {name: /^Address line 1/})).toHaveValue('3 Synthetic Row'));
    expect(screen.getByRole('textbox', {name: /^Address line 2/})).toHaveValue('Testbrook');
    expect(screen.getByRole('textbox', {name: /^Postcode/})).toHaveValue('ZZ3 3ZZ');
    expect(screen.getByRole('textbox', {name: /^Mobile/})).toHaveValue('07700 900999');
    await expectAnnounced(/name, date of birth and contact details filled in from Offline test data/);
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
      'GET /api/pathways/patient-pathways/search': () => json(pageOf([], 0)),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Patient'}));

    expect(within(openDialog()!).getByText('999 000 0077')).toBeInTheDocument();
  });
});

describe('Patient forms, NHS number look-up and identity', () => {
  // Synthetic people and test NHS numbers only (the 999 range is never issued to real patients).
  const lookUpAvailable = {'GET /api/demographics/status': () => json({isAvailable: true, source: 'Offline test data'})};
  const ada = {nhsNumber: '9990000018', firstName: 'Ada', lastName: 'Synthetic', dateOfBirth: '1980-05-17', source: 'Offline test data'};
  const nhsField = () => screen.findByRole('textbox', {name: /NHS number/});
  const held = /The name and date of birth were filled in from 999 000 0018\. Look the new number up again, or check the details\./;

  /** Add Patient with Ada looked up and filled in, and an email so the form can be saved. */
  async function addWithAdaFilledIn(extra: Parameters<typeof mockApi>[0] = {}) {
    const fetch = mockApi({...lookUpAvailable, 'POST /api/demographics/lookup': () => json(ada), ...extra});
    const result = renderWithApp(<AddPatientPage />, {path: '/patients/add', user: adminUser});
    await result.user.type(await nhsField(), '9990000018');
    await result.user.click(await screen.findByRole('button', {name: 'Look up'}));
    await waitFor(() => expect(screen.getByLabelText(/^first name/i)).toHaveValue('Ada'));
    await result.user.type(screen.getByLabelText(/^email address/i), 'ada.synthetic@example.com');
    return {fetch, ...result};
  }

  it("holds the save when the number is changed after a look-up filled someone else's details", async () => {
    const {user, fetch} = await addWithAdaFilledIn();

    const field = await nhsField();
    await user.clear(field);
    await user.type(field, '9990000026');
    expect(field).toHaveAccessibleDescription(held);

    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(await findAlert(/Not saved: the name and date of birth were filled in from NHS number 999 000 0018/)).toBeInTheDocument();
    expect(openDialog()).toBeNull();
    expect(calls(fetch)).not.toContain('POST /api/patients');
  });

  it('lets the save go ahead once the details are changed by hand', async () => {
    const {user, fetch} = await addWithAdaFilledIn({'POST /api/patients': () => json(freya, 201)});

    const field = await nhsField();
    await user.clear(field);
    await user.type(field, '9990000026');
    await user.type(screen.getByLabelText(/^first name/i), 'line');
    expect(field).not.toHaveAccessibleDescription(held);

    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Patient'}));
    await waitFor(() => expect(calls(fetch)).toContain('POST /api/patients'));
  });

  it('lets the save go ahead once the new number is looked up, and needs no hold for no number', async () => {
    let answer = ada;
    const {user} = await addWithAdaFilledIn({'POST /api/demographics/lookup': () => json(answer)});

    const field = await nhsField();
    await user.clear(field);
    // With no number, the details claim no number, so nothing is held.
    expect(field).not.toHaveAccessibleDescription(held);
    await user.type(field, '9990000026');
    expect(field).toHaveAccessibleDescription(held);

    answer = {...ada, nhsNumber: '9990000026', firstName: 'Ben', lastName: 'Synthetic', dateOfBirth: '1990-01-02'};
    await user.click(screen.getByRole('button', {name: 'Look up'}));
    await waitFor(() => expect(screen.getByLabelText(/^first name/i)).toHaveValue('Ben'));
    expect(field).not.toHaveAccessibleDescription(held);
    await user.click(screen.getByRole('button', {name: 'Add Patient'}));
    expect(openDialog()).not.toBeNull();
  });

  it('announces nothing when the number changed while it was being looked up', async () => {
    withAnnouncements();
    let answer: (response: Response) => void = () => {};
    mockApi({...lookUpAvailable, 'POST /api/demographics/lookup': () => new Promise<Response>((resolve) => (answer = resolve))});
    const {user} = renderWithApp(<AddPatientPage />, {user: adminUser});

    const field = await nhsField();
    await user.type(field, '9990000018');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));
    await user.type(field, '{Backspace}');
    answer(json(ada));

    await waitFor(() => expect(isDisabled(screen.getByRole('button', {name: 'Look up'}))).toBe(true));
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(screen.getByTestId('announcer')).not.toHaveTextContent(/Found|filled in/);
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('');
  });

  it("on Edit Patient, shows who a look-up found beside the record's details, and fills them only when asked", async () => {
    withAnnouncements();
    mockApi({...lookUpAvailable, [`GET /api/patients/${id}`]: () => json(freya), 'POST /api/demographics/lookup': () => json(ada)});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.type(await nhsField(), '9990000018');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));

    expect(await screen.findByText('Found from Offline test data: Ada Synthetic, born 17 May 1980')).toBeInTheDocument();
    expect(screen.getByText('This record now: Freya Ahmed, born 1 December 1976')).toBeInTheDocument();
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('Freya');
    await expectAnnounced(/Found Ada Synthetic, born 17 May 1980/);
    expect(screen.getByTestId('announcer')).not.toHaveTextContent(/filled in/);

    await user.click(screen.getByRole('button', {name: 'Use These Details'}));
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('Ada');
    expect(screen.getByLabelText(/^last name/i)).toHaveValue('Synthetic');
    expect(screen.queryByRole('button', {name: 'Use These Details'})).not.toBeInTheDocument();
    await expectAnnounced(/name and date of birth filled in from Offline test data/);
  });

  it('on Edit Patient, keeps the current details when asked', async () => {
    mockApi({...lookUpAvailable, [`GET /api/patients/${id}`]: () => json(freya), 'POST /api/demographics/lookup': () => json(ada)});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    await user.type(await nhsField(), '9990000018');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));
    await user.click(await screen.findByRole('button', {name: 'Keep Current Details'}));

    expect(screen.queryByText(/Found from Offline test data/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/^first name/i)).toHaveValue('Freya');
  });

  it('shows the saved record in the banner on Edit Patient while the fields change', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000042'})});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    const banner = await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'});
    await user.clear(screen.getByLabelText(/^first name/i));
    await user.type(screen.getByLabelText(/^first name/i), 'Fran');

    expect(banner).toHaveAccessibleName('Patient: Ahmed, Freya');
    expect(within(banner).getByText('NHS No. 999 000 0042')).toBeInTheDocument();
  });

  it("shows the old and new value of each detail that says who the patient is, in the save's confirmation", async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json({...freya, nhsNumber: '9990000042'})});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    const first = await screen.findByLabelText(/^first name/i);
    await user.clear(first);
    await user.type(first, 'Fran');
    await user.clear(await nhsField());
    await user.click(screen.getByRole('button', {name: 'Save'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText("This changes who the record identifies. Check it's the right patient before saving.")).toBeInTheDocument();
    const first_ = dialog.getByText('First name').closest('div')!.parentElement!;
    expect(first_).toHaveTextContent(/Freya.*changes to.*Fran/);
    expect(dialog.getByText('999 000 0042')).toBeInTheDocument();
    expect(dialog.getAllByText('Not given').length).toBeGreaterThan(0);
    expect(dialog.queryByText('Last name')).not.toBeInTheDocument();
  });

  it('asks plainly when nothing that says who the patient is changes', async () => {
    mockApi({[`GET /api/patients/${id}`]: () => json(freya)});
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    const email = await screen.findByLabelText(/^email address/i);
    await user.type(email, 'x');
    await user.click(screen.getByRole('button', {name: 'Save'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('Are you sure you want to save these changes?')).toBeInTheDocument();
    expect(dialog.queryByText('Changes to who this record identifies')).not.toBeInTheDocument();
  });

  it('holds the save on Edit Patient too, after using found details and changing the number', async () => {
    const fetch = mockApi({
      ...lookUpAvailable,
      [`GET /api/patients/${id}`]: () => json(freya),
      'POST /api/demographics/lookup': () => json(ada),
    });
    const {user} = renderWithApp(<EditPatientPage id={id} />, {user: adminUser});

    const field = await nhsField();
    await user.type(field, '9990000018');
    await user.click(await screen.findByRole('button', {name: 'Look up'}));
    await user.click(await screen.findByRole('button', {name: 'Use These Details'}));
    await user.clear(field);
    await user.type(field, '9990000026');
    await user.click(screen.getByRole('button', {name: 'Save'}));

    expect(await findAlert(/Not saved/)).toBeInTheDocument();
    expect(openDialog()).toBeNull();
    expect(calls(fetch)).not.toContain(`PATCH /api/patients/${id}`);
  });
});

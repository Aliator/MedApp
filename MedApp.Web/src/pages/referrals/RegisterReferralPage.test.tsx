import {screen, waitFor, within} from '@testing-library/react';
import {expectAnnounced, findAlert, json, mockApi, openDialog, pageOf, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {DERMATOLOGY_ID, REFERRAL_PATIENT, REFERRAL_PATIENT_ID, TEAMS, testReferral, testReferralSummary, UROLOGY_ID} from '../../test/referrals';
import {RegisterReferralPage} from './RegisterReferralPage';

const PATIENT = `GET /api/patients/${REFERRAL_PATIENT_ID}`;
const DUPLICATE = 'This patient already has an open referral to Dermatology, received 2 Nov 2026.';
const RESTRICTED = "This patient's record is restricted, so a referral can't be registered. Give the letter to an admin.";

function renderPage(
  patientId: string | null = REFERRAL_PATIENT_ID,
  handlers: Record<string, (request: Request) => Response | Promise<Response>> = {},
  user = staffUser,
) {
  const fetch = mockApi({
    [PATIENT]: () => json(REFERRAL_PATIENT),
    'GET /api/teams': () => json(TEAMS),
    'GET /api/referrals/search': () => json(pageOf([])),
    'POST /api/referrals': () => json(testReferral({id: 'new-referral'}), 201),
    ...handlers,
  });
  const onPatientChange = vi.fn();
  const result = renderWithApp(<RegisterReferralPage patientId={patientId ?? undefined} onPatientChange={onPatientChange} />, {user});
  return {...result, fetch, onPatientChange};
}

/** Fills the required details from the letter. */
async function fillDetails(user: ReturnType<typeof renderPage>['user']) {
  await user.click(await screen.findByRole('combobox', {name: /Clinic/}));
  await user.click(await screen.findByRole('option', {name: 'Dermatology'}));
  await user.type(screen.getByRole('textbox', {name: /Practice/}), 'Synthetic Street Surgery');
  await user.type(screen.getByRole('textbox', {name: /Reason/}), 'A rash');
}

const posts = async (fetch: ReturnType<typeof renderPage>['fetch']) =>
  Promise.all(
    fetch.mock.calls
      .map(([request]) => request as Request)
      .filter((request) => request.method === 'POST')
      .map((request) => request.clone().json()),
  );

describe('RegisterReferralPage', () => {
  it("goes back to the list after registering one for another team, as the registrar doesn't keep it (the team walls)", async () => {
    withAnnouncements();
    const urology = testReferral({id: 'sent-elsewhere', teamId: UROLOGY_ID, teamName: 'Urology'});
    const inDermatology = {...staffUser, teams: [{id: DERMATOLOGY_ID, name: 'Dermatology', kind: 'Clinic' as const, role: 'Member' as const}]};
    const {user, router} = renderPage(REFERRAL_PATIENT_ID, {'POST /api/referrals': () => json(urology, 201)}, inDermatology);

    await fillDetails(user);
    await user.click(screen.getByRole('button', {name: 'Register Referral'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals'));
    await expectAnnounced('Referral registered and sent to Urology');
  });

  it('finds the patient first, offering to add them', async () => {
    const {user, onPatientChange} = renderPage(null, {'GET /api/patients/search': () => json(pageOf([REFERRAL_PATIENT]))});

    expect(await screen.findByRole('heading', {name: 'Register Referral', level: 1})).toBeInTheDocument();
    expect(screen.queryByRole('region', {name: /^Patient: /})).not.toBeInTheDocument();
    expect(await screen.findByRole('link', {name: 'Add Patient'})).toHaveAttribute('href', '/patients/add?then=referral');
    await user.type(screen.getByRole('combobox', {name: /Patient/}), 'Ahmed');
    await user.click(await screen.findByRole('option', {name: /Freya Ahmed/}));
    expect(onPatientChange).toHaveBeenCalledWith(REFERRAL_PATIENT_ID);
  });

  it('registers it for the chosen patient, shown in the banner, then opens it', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderPage();

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    await fillDetails(user);
    await user.type(screen.getByRole('textbox', {name: /ODS code/}), 'a12345');
    await user.click(screen.getByRole('button', {name: 'Register Referral'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals/new-referral'));
    expect(await posts(fetch)).toEqual([
      {
        patientId: REFERRAL_PATIENT_ID,
        confirmPossibleDuplicate: false,
        teamId: DERMATOLOGY_ID,
        receivedOn: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
        practiceId: null,
        referrerPractice: 'Synthetic Street Surgery',
        referrerClinician: null,
        referrerOdsCode: 'A12345',
        requestedPriority: 'Medium',
        reason: 'A rash',
      },
    ]);
    await expectAnnounced('Referral registered');
  });

  it("shows the patient's open referrals, flagging the same clinic; a possible duplicate is confirmed as a different referral", async () => {
    let attempt = 0;
    const {user, fetch, router} = renderPage(REFERRAL_PATIENT_ID, {
      'GET /api/referrals/search': () => json(pageOf([testReferralSummary({id: 'open-1'})])),
      'POST /api/referrals': () =>
        ++attempt === 1
          ? json({title: 'One or more validation errors occurred.', errors: {ConfirmPossibleDuplicate: [DUPLICATE]}}, 400)
          : json(testReferral({id: 'second'}), 201),
    });

    expect(await screen.findByText('This patient already has referrals awaiting triage.')).toBeInTheDocument();
    await fillDetails(user);
    expect(screen.getByText(/already has a referral to this team awaiting triage/)).toBeInTheDocument();
    expect(screen.getByText('Same clinic')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Register Referral'}));

    const dialog = within((await screen.findByRole('heading', {name: 'Possible Duplicate'})).closest('dialog')!);
    expect(dialog.getByText(DUPLICATE)).toBeInTheDocument();
    expect(dialog.getByRole('list', {name: 'Open referrals to the same clinic'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'This Is a Different Referral'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals/second'));
    expect((await posts(fetch)).map((body) => body.confirmPossibleDuplicate)).toEqual([false, true]);
  });

  it('can go back from the duplicate question without registering', async () => {
    const {user} = renderPage(REFERRAL_PATIENT_ID, {
      'POST /api/referrals': () => json({errors: {ConfirmPossibleDuplicate: [DUPLICATE]}}, 400),
    });
    await fillDetails(user);
    await user.click(screen.getByRole('button', {name: 'Register Referral'}));
    await user.click(within(await waitFor(() => openDialog()!)).getByRole('button', {name: 'Go Back'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("shows the API's errors on the fields, and its refusal of a restricted record prominently", async () => {
    let attempt = 0;
    const {user} = renderPage(REFERRAL_PATIENT_ID, {
      'POST /api/referrals': () =>
        ++attempt === 1
          ? json({title: 'One or more validation errors occurred.', errors: {ReferrerPractice: ['Practice is required.'], ReceivedOn: ['Too old.']}}, 400)
          : json({title: RESTRICTED, status: 409}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'Register Referral'}));
    expect((await screen.findAllByText('Practice is required.')).length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', {name: 'Register Referral'}));
    expect(await findAlert(RESTRICTED)).toBeInTheDocument();
  });

  it('changes the patient, and finds them again when the one chosen is gone', async () => {
    const {user, onPatientChange, unmount} = renderPage();
    await user.click(await screen.findByRole('button', {name: 'Change Patient'}));
    expect(onPatientChange).toHaveBeenCalledWith(undefined);
    unmount();

    renderPage(REFERRAL_PATIENT_ID, {[PATIENT]: () => json({title: 'Not found'}, 404)});
    expect(await findAlert('Patient not found. They may have been deleted. Find them again.')).toBeInTheDocument();
    expect(screen.getByRole('combobox', {name: /Patient/})).toBeInTheDocument();
  });

  it('says when the patient could not be loaded', async () => {
    renderPage(REFERRAL_PATIENT_ID, {[PATIENT]: () => json({title: 'Down'}, 500)});
    expect(await findAlert('The patient could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('cancels back to the list', async () => {
    const {user, router} = renderPage();
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/referrals'));
  });
});

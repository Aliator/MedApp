import {screen, waitFor, within} from '@testing-library/react';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, seniorClinicianUser, withAnnouncements} from '../../test/render';
import {CONSULTANT_USER_ID, RESULT_ID, SENIOR_USER_ID, SIGN_OFF_HOLDERS, testResult} from '../../test/results';
import {ReassignResultDialog, ResultEnteredInErrorDialog, SignOffResultDialog, TakeOverResultDialog} from './ResultDialogs';

const dialog = () => within(openDialog()!);
const sentBody = (fetch: ReturnType<typeof mockApi>) =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST') as Request).clone().json();

describe('SignOffResultDialog', () => {
  it('lists the six actions in their fixed order with none chosen, and needs a detail for Other', async () => {
    withAnnouncements();
    const onClose = vi.fn();
    const fetch = mockApi({[`POST /api/results/${RESULT_ID}/sign-off`]: () => json(testResult({status: 'SignedOff'}))});
    const {user} = renderWithApp(<SignOffResultDialog result={testResult()} onClose={onClose} />, {user: seniorClinicianUser});

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('Skin biopsy (punch) report')).toBeInTheDocument();
    const radios = dialog().getAllByRole('radio');
    const labels = ['No further action', 'Patient informed', 'Appointment arranged', 'To be discussed at MDT', 'Referred on', 'Other'];
    expect(radios).toHaveLength(6);
    labels.forEach((label, index) => expect(dialog().getByRole('radio', {name: label})).toBe(radios[index]));
    expect(radios.some((r) => (r as HTMLInputElement).checked)).toBe(false);
    expect(isDisabled(dialog().getByRole('button', {name: 'Sign Off'}))).toBe(true);

    await user.click(dialog().getByRole('radio', {name: 'Other'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Sign Off'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /^Detail/}), 'Synthetic: phoned the GP');
    await user.click(dialog().getByRole('button', {name: 'Sign Off'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await sentBody(fetch)).toEqual({action: 'Other', detail: 'Synthetic: phoned the GP', expectedVersion: 2});
    await expectAnnounced('Result signed off');
  });

  it("shows the API's 400 on its field and a 409 in its words", async () => {
    let status = 400;
    mockApi({
      [`POST /api/results/${RESULT_ID}/sign-off`]: () =>
        status === 400 ? json({title: 'Invalid', errors: {Detail: ['The detail is too long.']}}, 400) : json({title: 'Someone else signed it off.'}, 409),
    });
    const {user} = renderWithApp(<SignOffResultDialog result={testResult()} onClose={vi.fn()} />, {user: seniorClinicianUser});

    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.click(dialog().getByRole('radio', {name: 'No further action'}));
    await user.click(dialog().getByRole('button', {name: 'Sign Off'}));
    expect((await dialog().findAllByText('The detail is too long.')).length).toBeGreaterThan(0);

    status = 409;
    await user.click(dialog().getByRole('button', {name: 'Sign Off'}));
    expect(await dialog().findByText('Someone else signed it off.')).toBeInTheDocument();
  });
});

describe('ReassignResultDialog', () => {
  it('offers the holders of SignOffResults but the one it has, none chosen, and sends who was chosen', async () => {
    withAnnouncements();
    const onClose = vi.fn();
    const fetch = mockApi({
      'GET /api/calendar/staff': () => json(SIGN_OFF_HOLDERS),
      [`POST /api/results/${RESULT_ID}/reassign`]: () => json(testResult()),
    });
    const {user} = renderWithApp(<ReassignResultDialog result={testResult()} onClose={onClose} />, {user: seniorClinicianUser});

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(isDisabled(dialog().getByRole('button', {name: 'Reassign'}))).toBe(true);
    await user.click(dialog().getByRole('combobox', {name: /^Responsible clinician/}));
    expect(screen.queryByRole('option', {name: 'test-senior'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', {name: 'dr.synthetic'}));
    await user.click(dialog().getByRole('button', {name: 'Reassign'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await sentBody(fetch)).toEqual({responsibleUserId: CONSULTANT_USER_ID, expectedVersion: 2});
    await expectAnnounced('Result reassigned to dr.synthetic');
  });
});

describe('TakeOverResultDialog', () => {
  it("asks first, then sends the caller's own ID", async () => {
    const onClose = vi.fn();
    const fetch = mockApi({[`POST /api/results/${RESULT_ID}/reassign`]: () => json(testResult())});
    const result = testResult({responsibleUserId: CONSULTANT_USER_ID, responsibleUsername: 'dr.synthetic', needsClinician: true});
    const {user} = renderWithApp(<TakeOverResultDialog result={result} userId={SENIOR_USER_ID} onClose={onClose} />, {user: seniorClinicianUser});

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByText(/dr.synthetic can no longer sign results off/)).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Take Over'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await sentBody(fetch)).toEqual({responsibleUserId: SENIOR_USER_ID, expectedVersion: 2});
  });
});

describe('ResultEnteredInErrorDialog', () => {
  it('says the file is deleted and the specimen goes back to the lab, and needs what was wrong', async () => {
    const onClose = vi.fn();
    const fetch = mockApi({[`POST /api/results/${RESULT_ID}/entered-in-error`]: () => json(testResult({status: 'EnteredInError'}))});
    const {user} = renderWithApp(<ResultEnteredInErrorDialog result={testResult()} onClose={onClose} />, {user: seniorClinicianUser});

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByText(/Its file will be deleted, and specimen MA26-000123 goes back to the lab to be reported again/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Mark Entered in Error'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /^What was wrong/}), 'Synthetic: wrong patient');
    await user.click(dialog().getByRole('button', {name: 'Mark Entered in Error'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await sentBody(fetch)).toEqual({detail: 'Synthetic: wrong patient', expectedVersion: 2});
  });

  it("says only the file is deleted for an external report", async () => {
    mockApi({});
    renderWithApp(<ResultEnteredInErrorDialog result={testResult({specimenId: null, specimenNumber: null, source: 'ExternalReport'})} onClose={vi.fn()} />, {
      user: seniorClinicianUser,
    });

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByText(/isn't a result. Its file will be deleted\./)).toBeInTheDocument();
  });
});

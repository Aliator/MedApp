import {screen} from '@testing-library/react';
import {isDisabled, json, mockApi, nurseUser, renderWithApp} from '../../test/render';
import {CONSENT_PATIENT, CONSENT_RECORD_ID, testConsent} from '../../test/consent';
import {ConsentActionDialog} from './ConsentActionDialog';

describe('ConsentActionDialog', () => {
  it('marks a record entered in error, saying its scan goes with it', async () => {
    const fetch = mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/entered-in-error`]: () => json(testConsent({status: 'EnteredInError'}))});
    const onClose = vi.fn();
    const {user} = renderWithApp(<ConsentActionDialog action="enteredInError" record={testConsent()} patient={CONSENT_PATIENT} onClose={onClose} />, {
      user: nurseUser,
    });

    expect(await screen.findByText(/its scan is deleted/)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Mark Entered in Error'}))).toBe(true);
    await user.type(screen.getByRole('textbox', {name: /What was wrong/}), 'Synthetic: wrong patient.');
    await user.click(screen.getByRole('button', {name: 'Mark Entered in Error'}));

    await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
    const body = await (fetch.mock.calls.find(([r]) => (r as Request).url.endsWith('/entered-in-error'))![0] as Request).json();
    expect(body).toEqual({detail: 'Synthetic: wrong patient.', expectedVersion: 1});
  });

  it("shows the API's refusal in its words", async () => {
    mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/withdraw`]: () => json({title: 'Someone else changed this consent record.'}, 409)});
    const {user} = renderWithApp(<ConsentActionDialog action="withdraw" record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});

    await user.type(await screen.findByRole('textbox', {name: /What was said/}), 'Synthetic.');
    await user.click(screen.getByRole('button', {name: 'Withdraw Consent'}));

    expect(await screen.findByText('Someone else changed this consent record.')).toBeInTheDocument();
  });

  it('asks for a file to add a scan', async () => {
    renderWithApp(<ConsentActionDialog action="attachScan" record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});
    expect(isDisabled(await screen.findByRole('button', {name: 'Add Scan'}))).toBe(true);
  });
});

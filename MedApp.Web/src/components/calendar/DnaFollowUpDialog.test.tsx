import {screen, within} from '@testing-library/react';
import {TEST_PATIENT, testAppointment} from '../../test/calendar';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {DNA_FOLLOW_UP_CHANGES_NOTHING, DnaFollowUpDialog, dnaFollowUpWords} from './DnaFollowUpDialog';

const missed = testAppointment({
  id: 'appt-1',
  status: 'DidNotAttend',
  canRecordDnaFollowUp: true,
  dnaFollowUp: 'Rebook',
  dnaFollowUpByUsername: 'b.staff',
  dnaFollowUpAtUtc: '2026-11-23T10:00:00Z',
});

function renderDialog(handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const fetch = mockApi({'GET /api/patients/patient-1': () => json(TEST_PATIENT), ...handlers});
  const result = renderWithApp(<DnaFollowUpDialog isOpen appointment={missed} onClose={onClose} onSaved={onSaved} />, {user: staffUser});
  return {...result, fetch, onClose, onSaved};
}

describe('DnaFollowUpDialog', () => {
  it('words the follow-up recorded so far, or none', () => {
    expect(dnaFollowUpWords(missed)).toBe('To be rebooked, by b.staff, 23 Nov 2026, 10:00');
    expect(dnaFollowUpWords({dnaFollowUp: 'ClinicianToReview'})).toBe('Clinician to review');
    expect(dnaFollowUpWords({dnaFollowUp: null})).toBeNull();
  });

  it('shows who and what was missed, what was recorded so far, and that it changes nothing else', async () => {
    renderDialog();

    expect(await screen.findByRole('heading', {name: 'Record Follow-up'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByText(DNA_FOLLOW_UP_CHANGES_NOTHING)).toBeInTheDocument();
    expect(await dialog.findByRole('region', {name: 'Patient: Patient, Test'})).toHaveTextContent('NHS No. 999 000 0018');
    expect(dialog.getByText('Mon 23 Nov 2026, 09:00 to 09:30 with test-user')).toBeInTheDocument();
    expect(dialog.getByText('Knee review: Assessment')).toBeInTheDocument();
    expect(dialog.getByText('To be rebooked, by b.staff, 23 Nov 2026, 10:00')).toBeInTheDocument();
    expect(dialog.getByRole('textbox', {name: /Detail/})).toHaveAccessibleDescription(/Who decided, and why/);
  });

  it('records a decision, needing a detail only for No further appointment, then announces and closes', async () => {
    withAnnouncements();
    const saved = {...missed, dnaFollowUp: 'ClinicianToReview'};
    const {user, fetch, onClose, onSaved} = renderDialog({'POST /api/appointments/appt-1/dna-follow-up': () => json(saved)});

    await screen.findByRole('heading', {name: 'Record Follow-up'});
    const dialog = within(openDialog()!);
    const confirm = dialog.getByRole('button', {name: 'Record Follow-up'});
    await user.click(dialog.getByRole('radio', {name: 'No further appointment'}));
    expect(isDisabled(confirm)).toBe(true);
    await user.click(dialog.getByRole('radio', {name: 'Clinician to review'}));
    expect(isDisabled(confirm)).toBe(false);
    await user.click(confirm);

    await expectAnnounced('Follow-up recorded: Clinician to review');
    expect(onSaved).toHaveBeenCalledWith(saved);
    expect(onClose).toHaveBeenCalled();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.clone().json()).toEqual({decision: 'ClinicianToReview', detail: null});
  });

  it('says so when it fails for no reason the API gives', async () => {
    const {user} = renderDialog({'POST /api/appointments/appt-1/dna-follow-up': () => json({}, 500)});

    await screen.findByRole('heading', {name: 'Record Follow-up'});
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('radio', {name: 'To be rebooked'}));
    await user.click(dialog.getByRole('button', {name: 'Record Follow-up'}));

    expect(await dialog.findByText('The follow-up could not be recorded. Please try again.')).toBeInTheDocument();
  });
});

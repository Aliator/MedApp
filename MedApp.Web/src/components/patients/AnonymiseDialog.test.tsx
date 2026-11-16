import {screen, waitFor, within} from '@testing-library/react';
import {calls, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {AnonymiseDialog, fullName} from './AnonymiseDialog';

const id = 'a1b2c3d4-0000-4000-8000-000000000001';
const patient = {id, firstName: 'Oriel', lastName: 'Penhallow', dateOfBirth: '1951-03-14'};
const ANONYMISE = `/api/patients/${id}/anonymise`;
const CHANGED = "Someone else changed this patient's record while you were working on it. Reload it to see their change, then try again.";

function renderDialog(handler: () => Response = () => json({...patient, isAnonymised: true}), defaultReason?: 'RetentionPeriodEnded') {
  const fetch = mockApi({[`POST ${ANONYMISE}`]: handler});
  const onDone = vi.fn();
  const onCancel = vi.fn();
  return {
    fetch,
    onDone,
    onCancel,
    ...renderWithApp(<AnonymiseDialog patient={patient} defaultReason={defaultReason} onDone={onDone} onCancel={onCancel} />),
  };
}

async function fill(user: ReturnType<typeof renderDialog>['user'], name: string, reason: RegExp | null = /past its retention period/i) {
  const dialog = await screen.findByRole('dialog');
  if (reason) await user.click(within(dialog).getByRole('radio', {name: reason}));
  await user.type(within(dialog).getByRole('textbox', {name: /type oriel penhallow to confirm/i}), name);
  return within(dialog).getByRole('button', {name: 'Anonymise Record'});
}

describe('AnonymiseDialog', () => {
  it('names the patient as staff see them', () => {
    expect(fullName({firstName: 'Oriel', lastName: null})).toBe('Oriel');
  });

  it('is closed with no patient', () => {
    renderWithApp(<AnonymiseDialog patient={null} onDone={() => {}} onCancel={() => {}} />);

    expect(openDialog()).toBeNull();
  });

  it('says the result is pseudonymised, and warns that specialty records are kept longer', async () => {
    renderDialog();

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/pseudonymised rather than anonymous/)).toHaveTextContent(/still personal data/);
    expect(within(dialog).getByText(/Mental health, maternity and oncology records must be kept longer/)).toHaveTextContent(
      /Retention:AdultYears/,
    );
    expect(within(dialog).getByRole('textbox', {name: /note/i})).toHaveAccessibleDescription(/must hold no patient details/);
  });

  it("waits for the patient's full name and a reason, ignoring case and extra spaces, then anonymises", async () => {
    const {user, fetch, onDone} = renderDialog();

    const confirm = await fill(user, 'oriel  pen', null);
    expect(isDisabled(confirm)).toBe(true);
    await user.type(within(openDialog()!).getByRole('textbox', {name: /to confirm/i}), 'hallow ');
    // The name's right, but there's no reason yet.
    expect(isDisabled(confirm)).toBe(true);
    await user.click(within(openDialog()!).getByRole('radio', {name: /asked for erasure/i}));
    await user.type(within(openDialog()!).getByRole('textbox', {name: /note/i}), '  Ticket 42  ');
    expect(isDisabled(confirm)).toBe(false);
    await user.click(confirm);

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(calls(fetch)).toEqual([`POST ${ANONYMISE}`]);
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({
      reason: 'ErasureRequest',
      note: 'Ticket 42',
      confirmation: 'oriel  penhallow ',
    });
  });

  it('starts on the reason it is given, and sends no note when there is none', async () => {
    const {user, fetch} = renderDialog(undefined, 'RetentionPeriodEnded');

    expect(await screen.findByRole('radio', {name: /past its retention period/i})).toBeChecked();
    await user.click(await fill(user, 'Oriel Penhallow', null));

    await waitFor(() => expect(calls(fetch)).toHaveLength(1));
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({reason: 'RetentionPeriodEnded', note: null, confirmation: 'Oriel Penhallow'});
  });

  it('says why the API refused, and stays open', async () => {
    const {user, onDone} = renderDialog(() =>
      json({title: 'This patient has 1 procedure under way, so their record can’t be anonymised.'}, 409),
    );

    await user.click(await fill(user, 'Oriel Penhallow'));

    expect(await screen.findByText(/1 procedure under way/)).toBeInTheDocument();
    expect(onDone).not.toHaveBeenCalled();
  });

  it('says so when someone else changed the record meanwhile', async () => {
    const {user} = renderDialog(() => json({title: CHANGED}, 409));

    await user.click(await fill(user, 'Oriel Penhallow'));

    expect(await screen.findByText(CHANGED)).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
  });

  it("shows the API's field errors", async () => {
    const {user} = renderDialog(() =>
      json({title: 'One or more validation errors occurred.', errors: {Confirmation: ['Type the patient’s full name exactly as shown to confirm.']}}, 400),
    );

    await user.click(await fill(user, 'Oriel Penhallow'));

    expect(await screen.findByText('Type the patient’s full name exactly as shown to confirm.')).toBeInTheDocument();
  });

  it('can be cancelled', async () => {
    const {user, onCancel} = renderDialog();

    await screen.findByRole('dialog');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalled();
  });
});

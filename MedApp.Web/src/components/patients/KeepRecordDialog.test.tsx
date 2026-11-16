import {screen, waitFor, within} from '@testing-library/react';
import {calls, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {KeepRecordDialog} from './KeepRecordDialog';

const patientId = 'a1b2c3d4-0000-4000-8000-000000000002';
const patient = {patientId, firstName: 'Tamsin', lastName: 'Rowcroft', dateOfBirth: '1940-02-02'};
const KEEP = `/api/patients/${patientId}/retention/keep`;

function renderDialog(handler: () => Response = () => new Response(null, {status: 204})) {
  const fetch = mockApi({[`POST ${KEEP}`]: handler});
  const onDone = vi.fn();
  const onCancel = vi.fn();
  return {fetch, onDone, onCancel, ...renderWithApp(<KeepRecordDialog patient={patient} onDone={onDone} onCancel={onCancel} />)};
}

describe('KeepRecordDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date']});
    vi.setSystemTime(new Date('2026-11-16T12:00:00Z'));
  });

  it('is closed with no patient', () => {
    renderWithApp(<KeepRecordDialog patient={null} onDone={() => {}} onCancel={() => {}} />);

    expect(openDialog()).toBeNull();
  });

  it('keeps the record for a year by default, with the reason given', async () => {
    const {user, fetch, onDone} = renderDialog();

    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), ' Synthetic legal hold ');
    await user.click(within(dialog).getByRole('button', {name: 'Keep Record'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(calls(fetch)).toEqual([`POST ${KEEP}`]);
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({keepUntil: '2027-11-16', reason: 'Synthetic legal hold'});
  });

  it('keeps it until the date chosen', async () => {
    const {user, fetch, onDone} = renderDialog();

    const dialog = await screen.findByRole('dialog');
    const date = within(dialog).getByRole('combobox', {name: /review again on/i});
    await user.clear(date);
    await user.type(date, '01/06/2027');
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic legal hold');
    await user.click(within(dialog).getByRole('button', {name: 'Keep Record'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({keepUntil: '2027-06-01', reason: 'Synthetic legal hold'});
  });

  it("shows the API's reasons, and stays open", async () => {
    const {user, onDone} = renderDialog(() =>
      json({title: 'One or more validation errors occurred.', errors: {KeepUntil: ['The review date must be no more than 2 years ahead.']}}, 400),
    );

    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic legal hold');
    await user.click(within(dialog).getByRole('button', {name: 'Keep Record'}));

    expect(await screen.findByText('The review date must be no more than 2 years ahead.')).toBeInTheDocument();
    expect(onDone).not.toHaveBeenCalled();
  });

  it('says when the record was anonymised meanwhile', async () => {
    const {user} = renderDialog(() => json({title: "This patient's record has been anonymised, so it's no longer due for review."}, 409));

    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic legal hold');
    await user.click(within(dialog).getByRole('button', {name: 'Keep Record'}));

    expect(await screen.findByText(/no longer due for review/)).toBeInTheDocument();
  });

  it('can be cancelled', async () => {
    const {user, onCancel} = renderDialog();

    await screen.findByRole('dialog');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalled();
  });
});

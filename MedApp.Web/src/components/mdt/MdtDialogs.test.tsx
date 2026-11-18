import {screen, waitFor, within} from '@testing-library/react';
import {ApiError} from '../../api/errors';
import {mdtItem, mdtMeeting, mdtPatient} from '../../test/mdt';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {DeferMdtItemDialog, ListForMdtDialog, meetingLabel, RecordMdtOutcomeDialog, WithdrawMdtItemDialog} from './MdtDialogs';

async function shownDialog() {
  await waitFor(() => expect(openDialog()).not.toBeNull());
  return within(openDialog()!);
}

describe('ListForMdtDialog', () => {
  it('shows the patient, starts empty, and sends the MDT and the question', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(
      <ListForMdtDialog patient={mdtPatient} pathwayTitle="Skin lesion" meetings={[mdtMeeting()]} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    const list = dialog.getByRole('button', {name: 'List for MDT'});
    expect(isDisabled(list)).toBe(true);

    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(mdtMeeting())}));
    await user.type(dialog.getByRole('textbox', {name: /Question for the team/}), ' Excise or watch? ');
    await user.click(list);

    expect(onConfirm).toHaveBeenCalledWith({meetingId: mdtMeeting().id, question: 'Excise or watch?'}, expect.any(Function));
  });

  it("says so when no MDT is arranged, and shows the API's refusal", async () => {
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(new ApiError(409, undefined, 'Synthetic refusal.')));
    renderWithApp(
      <ListForMdtDialog patient={mdtPatient} pathwayTitle="Skin lesion" meetings={[]} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.getByText(/No MDTs are arranged yet/)).toBeInTheDocument();
  });
});

describe('RecordMdtOutcomeDialog', () => {
  it('suggests nothing: an empty outcome and nobody ticked, then sends what was written and who was present', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(
      <RecordMdtOutcomeDialog item={mdtItem({canRecordOutcome: true})} attendees={mdtMeeting().attendees ?? []} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(dialog.getByRole('textbox', {name: /Outcome/})).toHaveValue('');
    expect(dialog.getAllByRole('checkbox').every((c) => !(c as HTMLInputElement).checked)).toBe(true);
    const record = dialog.getByRole('button', {name: 'Record Outcome'});
    expect(isDisabled(record)).toBe(true);

    await user.type(dialog.getByRole('textbox', {name: /Outcome/}), 'Synthetic outcome.');
    await user.click(dialog.getByRole('checkbox', {name: 'k.ashford'}));
    await user.click(record);

    expect(onConfirm).toHaveBeenCalledWith({outcome: 'Synthetic outcome.', presentUserIds: ['u-1']}, expect.any(Function));
  });
});

describe('DeferMdtItemDialog', () => {
  it('offers only later MDTs than the one it is on', async () => {
    const onConfirm = vi.fn();
    const later = mdtMeeting({id: '00000000-0000-4000-8000-0000000000a2', startUtc: '2099-12-10T13:00:00Z', endUtc: '2099-12-10T14:00:00Z'});
    const {user} = renderWithApp(
      <DeferMdtItemDialog item={mdtItem()} meetings={[mdtMeeting(), later]} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    expect(screen.queryByRole('option', {name: meetingLabel(mdtMeeting())})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', {name: meetingLabel(later)}));
    await user.click(dialog.getByRole('button', {name: 'Move'}));

    expect(onConfirm).toHaveBeenCalledWith(later.id, expect.any(Function));
  });

  it('lists again an item whose meeting was cancelled', async () => {
    renderWithApp(
      <DeferMdtItemDialog item={mdtItem({meetingId: null, meetingStartUtc: null})} meetings={[mdtMeeting()]} isProcessing={false} onConfirm={vi.fn()} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.getByRole('button', {name: 'List Again'})).toBeInTheDocument();
  });
});

describe('WithdrawMdtItemDialog', () => {
  it("asks, then withdraws, showing the API's refusal", async () => {
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(new ApiError(409, undefined, 'Synthetic refusal.')));
    const {user} = renderWithApp(<WithdrawMdtItemDialog item={mdtItem()} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('button', {name: 'Withdraw'}));

    expect(await dialog.findByText('Synthetic refusal.')).toBeInTheDocument();
  });
});

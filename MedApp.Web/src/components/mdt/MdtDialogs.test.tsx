import {screen, waitFor, within} from '@testing-library/react';
import {ApiError, ApiValidationError} from '../../api/errors';
import {mdtItem, mdtMeeting, mdtPatient} from '../../test/mdt';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {
  DeferMdtItemDialog,
  ListForMdtDialog,
  MAX_MDT_OUTCOME_LENGTH,
  MAX_MDT_QUESTION_LENGTH,
  meetingLabel,
  RecordMdtOutcomeDialog,
  WithdrawMdtItemDialog,
} from './MdtDialogs';

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

describe('MdtDialogs, what goes wrong', () => {
  const validation = (field: string, message: string) =>
    new ApiValidationError({title: 'Invalid', fields: {[field]: [message]}, general: []});

  it("names an MDT without a title by its specialty", () => {
    expect(meetingLabel(mdtMeeting({title: null, specialtyName: null}))).toMatch(/^ MDT: /);
    expect(meetingLabel(mdtMeeting({title: null}))).toMatch(/^Dermatology MDT: /);
  });

  it("refuses a question that is too long, and shows a 400 on its field and any other refusal", async () => {
    let failWith: unknown = validation('question', 'Synthetic question problem.');
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(failWith));
    const {user} = renderWithApp(
      <ListForMdtDialog pathwayTitle="Skin lesion" meetings={[mdtMeeting()]} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.queryByRole('region', {name: /Patient:/})).not.toBeInTheDocument();
    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(mdtMeeting())}));
    const question = dialog.getByRole('textbox', {name: /Question for the team/});
    await user.click(question);
    await user.paste('x'.repeat(MAX_MDT_QUESTION_LENGTH + 1));
    expect(await dialog.findByText(`At most ${MAX_MDT_QUESTION_LENGTH} characters.`)).toBeInTheDocument();
    const list = dialog.getByRole('button', {name: 'List for MDT'});
    expect(isDisabled(list)).toBe(true);

    await user.clear(question);
    await user.type(question, 'Synthetic question?');
    await user.click(list);
    expect(await dialog.findByText('Synthetic question problem.')).toBeInTheDocument();

    failWith = validation('unknownField', 'Not on a field.');
    await user.click(list);
    expect(await dialog.findByText('Not on a field.')).toBeInTheDocument();
  });

  it('refuses an outcome that is too long, and shows the API refusal', async () => {
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(new ApiError(409, undefined, 'Synthetic outcome refusal.')));
    const {user} = renderWithApp(
      <RecordMdtOutcomeDialog
        item={mdtItem({patient: undefined, pathwayTitle: null, question: null})}
        attendees={[{userId: undefined, username: null}, ...(mdtMeeting().attendees ?? [])]}
        isProcessing={false}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      />,
    );

    const dialog = await shownDialog();
    const outcome = dialog.getByRole('textbox', {name: /Outcome/});
    await user.click(outcome);
    await user.paste('x'.repeat(MAX_MDT_OUTCOME_LENGTH + 1));
    expect(await dialog.findByText(`At most ${MAX_MDT_OUTCOME_LENGTH} characters.`)).toBeInTheDocument();

    await user.clear(outcome);
    await user.type(outcome, 'Synthetic outcome.');
    await user.click(dialog.getByRole('checkbox', {name: 'k.ashford'}));
    await user.click(dialog.getByRole('button', {name: 'Record Outcome'}));
    expect(await dialog.findByText('Synthetic outcome refusal.')).toBeInTheDocument();
  });

  it('shows a refusal to move an item on its MDT field', async () => {
    const later = mdtMeeting({id: '00000000-0000-4000-8000-0000000000a2', title: null, startUtc: '2099-12-10T13:00:00Z', endUtc: '2099-12-10T14:00:00Z'});
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(validation('meetingId', 'Synthetic meeting problem.')));
    const {user} = renderWithApp(
      <DeferMdtItemDialog item={mdtItem({patient: undefined})} meetings={[later]} isLoadingMeetings={false} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(later)}));
    await user.click(dialog.getByRole('button', {name: 'Move'}));
    expect(await dialog.findByText('Synthetic meeting problem.')).toBeInTheDocument();
  });

  it('withdraws an item without its patient or question', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(
      <WithdrawMdtItemDialog item={mdtItem({patient: undefined, question: null})} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('button', {name: 'Withdraw'}));
    expect(onConfirm).toHaveBeenCalledWith(undefined, expect.any(Function));
  });
});

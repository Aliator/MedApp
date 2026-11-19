import {screen, waitFor, within} from '@testing-library/react';
import {MAX_CONSULT_REPLY_LENGTH} from '../../api/consults';
import {ApiError} from '../../api/errors';
import {testConsult} from '../../test/consults';
import {mdtPatient} from '../../test/mdt';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {AskConsultDialog, EndConsultDialog, ReplyConsultDialog} from './ConsultDialogs';

async function shownDialog() {
  await waitFor(() => expect(openDialog()).not.toBeNull());
  return within(openDialog()!);
}

const TEAMS = [
  {id: 'team-lab', name: 'Lab'},
  {id: 'team-other', name: 'Other clinic (test)'},
];

describe('AskConsultDialog', () => {
  it('shows the patient, chooses no team for you, and sends the team and the question', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(
      <AskConsultDialog patient={mdtPatient} pathwayTitle="Skin lesion" teams={TEAMS} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    const ask = dialog.getByRole('button', {name: 'Ask'});
    expect(isDisabled(ask)).toBe(true);
    expect(dialog.getByRole('textbox', {name: /Question/})).toHaveValue('');

    await user.click(dialog.getByRole('combobox', {name: /Team to ask/}));
    await user.click(await screen.findByRole('option', {name: 'Lab'}));
    await user.type(dialog.getByRole('textbox', {name: /Question/}), ' Punch or excision? ');
    await user.click(ask);

    expect(onConfirm).toHaveBeenCalledWith({askedTeamId: 'team-lab', question: 'Punch or excision?'}, expect.any(Function));
  });
});

describe('ReplyConsultDialog', () => {
  it('shows the question, starts empty, and sends the reply as written', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(<ReplyConsultDialog consult={testConsult({canReply: true})} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);

    const dialog = await shownDialog();
    expect(dialog.getByText('Question: Synthetic question for the lab.')).toBeInTheDocument();
    expect(dialog.getByRole('textbox', {name: /Reply/})).toHaveValue('');
    await user.type(dialog.getByRole('textbox', {name: /Reply/}), 'Synthetic reply.');
    await user.click(dialog.getByRole('button', {name: 'Reply'}));

    expect(onConfirm).toHaveBeenCalledWith('Synthetic reply.', expect.any(Function));
  });

  it('refuses a reply that is too long', async () => {
    const {user} = renderWithApp(<ReplyConsultDialog consult={testConsult()} isProcessing={false} onConfirm={vi.fn()} onCancel={vi.fn()} />);
    const dialog = await shownDialog();
    await user.click(dialog.getByRole('textbox', {name: /Reply/}));
    await user.paste('x'.repeat(MAX_CONSULT_REPLY_LENGTH + 1));
    expect(isDisabled(dialog.getByRole('button', {name: 'Reply'}))).toBe(true);
    expect(dialog.getByText(`At most ${MAX_CONSULT_REPLY_LENGTH} characters.`)).toBeInTheDocument();
  });
});

describe('EndConsultDialog', () => {
  it('closes, saying the asked team stops reading the pathway', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(
      <EndConsultDialog consult={testConsult({status: 'Replied'})} kind="close" isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />,
    );
    const dialog = await shownDialog();
    expect(dialog.getByText(/Lab can no longer read the pathway/)).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Close Consult'}));
    expect(onConfirm).toHaveBeenCalled();
  });

  it("withdraws, and shows the API's refusal", async () => {
    const onConfirm = vi.fn((_input: unknown, fail: (e: unknown) => void) => fail(new ApiError(409, undefined, 'Synthetic refusal.')));
    const {user} = renderWithApp(<EndConsultDialog consult={testConsult()} kind="withdraw" isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);
    const dialog = await shownDialog();
    await user.click(dialog.getByRole('button', {name: 'Withdraw'}));
    expect(await dialog.findByText('Synthetic refusal.')).toBeInTheDocument();
  });
});

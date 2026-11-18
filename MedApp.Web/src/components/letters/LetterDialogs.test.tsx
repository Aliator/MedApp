import {screen, waitFor, within} from '@testing-library/react';
import {isDisabled, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {approvedLetter, LETTER_APPROVER_ID, LETTER_AUTHOR_ID, LETTER_ID, patientRecipient, practiceRecipient, testLetter} from '../../test/letters';
import {
  ApproveLetterDialog,
  DiscardLetterDialog,
  recordableDays,
  RecordSentDialog,
  ReturnLetterDialog,
  SendSecureEmailDialog,
  SubmitLetterDialog,
  WithdrawLetterDialog,
} from './LetterDialogs';

const BASE = `/api/letters/${LETTER_ID}`;
const body = (fetch: ReturnType<typeof mockApi>, method = 'POST') =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method) as Request).json();

/** The open dialog, once it's open. */
const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function setUp(dialog: React.ReactElement, handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi(handlers);
  const result = renderWithApp(dialog, {user: staffUser});
  return {...result, fetch};
}

describe('letter dialogs', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('submits to an approver chosen from those who can approve, never the author', async () => {
    const onDone = vi.fn();
    const {user, fetch} = setUp(<SubmitLetterDialog letter={testLetter()} onCancel={vi.fn()} onDone={onDone} />, {
      'GET /api/calendar/staff': () =>
        json([
          {userId: LETTER_AUTHOR_ID, username: 'k.ashford'},
          {userId: LETTER_APPROVER_ID, username: 'd.clinician'},
        ]),
      [`POST ${BASE}/submit`]: () => json(testLetter({status: 'AwaitingApproval'})),
    });

    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Discharge summary')).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Submit'}))).toBe(true);
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Approver/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Approver/}));
    expect(screen.queryByRole('option', {name: 'k.ashford'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('option', {name: 'd.clinician'}));
    await user.click(dialog.getByRole('button', {name: 'Submit'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({approverUserId: LETTER_APPROVER_ID, expectedVersion: 3});
    expect(new URL((fetch.mock.calls[0][0] as Request).url).search).toBe('?permission=ApproveLetters');
  });

  it('says when nobody else can approve, and shows a 400 on the approver', async () => {
    const {user} = setUp(<SubmitLetterDialog letter={testLetter()} onCancel={vi.fn()} onDone={vi.fn()} />, {
      'GET /api/calendar/staff': () => json([{userId: LETTER_AUTHOR_ID, username: 'k.ashford'}]),
    });
    expect(await screen.findByText(/Nobody else can approve letters/)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Submit'}))).toBe(true);
    await user.keyboard('{Escape}');
  });

  it('shows the approver field error and any other message from a 400', async () => {
    const {user} = setUp(<SubmitLetterDialog letter={testLetter()} onCancel={vi.fn()} onDone={vi.fn()} />, {
      'GET /api/calendar/staff': () => json([{userId: LETTER_APPROVER_ID, username: 'd.clinician'}]),
      [`POST ${BASE}/submit`]: () =>
        json({title: 'Invalid', status: 400, errors: {ApproverUserId: ['They can no longer approve letters.'], '': ['Add a recipient first.']}}, 400),
    });
    const dialog = within(await dialogOpen());
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Approver/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Approver/}));
    await user.click(await screen.findByRole('option', {name: 'd.clinician'}));
    await user.click(dialog.getByRole('button', {name: 'Submit'}));
    expect((await dialog.findAllByText('They can no longer approve letters.')).length).toBeGreaterThan(0);
    expect(dialog.getByText('Add a recipient first.')).toBeInTheDocument();
  });

  it('approves after showing the patient and every recipient with their address (hazard H-48)', async () => {
    const onDone = vi.fn();
    const {user, fetch} = setUp(<ApproveLetterDialog letter={testLetter({canApprove: true})} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/approve`]: () => json(approvedLetter()),
    });

    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    const recipients = within(dialog.getByRole('list', {name: 'Recipients'}));
    expect(recipients.getByText('Addressed to: Testbrook Surgery')).toBeInTheDocument();
    expect(recipients.getByText('Copy: Freya Ahmed')).toBeInTheDocument();
    expect(recipients.getByText('2 Example Road')).toBeInTheDocument();
    expect(recipients.getByText('testbrook.surgery@nhs.net')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Approve'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(onDone.mock.calls[0][0]).toMatchObject({status: 'Approved'});
    expect(await body(fetch)).toEqual({expectedVersion: 3});
  });

  it("shows the API's words when approving is refused, and stays open", async () => {
    const onDone = vi.fn();
    const {user} = setUp(<ApproveLetterDialog letter={testLetter({canApprove: true, recipients: null})} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/approve`]: () => json({title: "It can't be approved: patient.address has no value."}, 409),
    });
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('button', {name: 'Approve'}));
    expect(await dialog.findByText("It can't be approved: patient.address has no value.")).toBeInTheDocument();
    expect(onDone).not.toHaveBeenCalled();
  });

  it('returns it to its author with a comment', async () => {
    const onDone = vi.fn();
    const {user, fetch} = setUp(<ReturnLetterDialog letter={testLetter({status: 'AwaitingApproval'})} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/return`]: () => json(testLetter({isReturned: true})),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByText(/goes back to k.ashford/)).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Return'}))).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /What needs changing/}), '  Add the plan  ');
    await user.click(dialog.getByRole('button', {name: 'Return'}));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({comment: 'Add the plan', expectedVersion: 3});
  });

  it('discards a draft, or cancels', async () => {
    const onDone = vi.fn();
    const onCancel = vi.fn();
    const {user} = setUp(<DiscardLetterDialog letter={testLetter()} onCancel={onCancel} onDone={onDone} />, {
      [`POST ${BASE}/discard`]: () => json(testLetter()),
    });
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
    await user.click(dialog.getByRole('button', {name: 'Discard Draft'}));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
  });

  it('withdraws with a reason, listing who it was already sent to', async () => {
    const onDone = vi.fn();
    const letter = approvedLetter({recipients: [practiceRecipient({dispatchStatus: 'Sent', method: 'SecureEmail'}), patientRecipient()]});
    const {user, fetch} = setUp(<WithdrawLetterDialog letter={letter} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/withdraw`]: () => json(approvedLetter({status: 'Withdrawn'})),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByText("It has already been sent to these recipients. Tell them it's withdrawn, and why.")).toBeInTheDocument();
    const sent = within(dialog.getByRole('list', {name: 'Already sent to'}));
    expect(sent.getByText('Addressed to: Testbrook Surgery')).toBeInTheDocument();
    expect(sent.queryByText(/Freya Ahmed/)).not.toBeInTheDocument();
    await user.type(dialog.getByRole('textbox', {name: /Why it's withdrawn/}), 'Wrong plan');
    await user.click(dialog.getByRole('button', {name: 'Withdraw Letter'}));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({reason: 'Wrong plan', expectedVersion: 3});
  });

  it("says when it hasn't been sent to anyone yet", async () => {
    setUp(<WithdrawLetterDialog letter={approvedLetter()} onCancel={vi.fn()} onDone={vi.fn()} />);
    expect(await screen.findByText("It hasn't been sent to anyone yet.")).toBeInTheDocument();
  });

  it('sends by secure email, naming the address', async () => {
    const onDone = vi.fn();
    const recipient = practiceRecipient({canSendSecureEmail: true});
    const {user, fetch} = setUp(<SendSecureEmailDialog letter={approvedLetter()} recipient={recipient} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/recipients/${recipient.id}/send`]: () => json(approvedLetter()),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByText(/goes to Testbrook Surgery at testbrook.surgery@nhs.net/)).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Send'}));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({expectedVersion: 3});
  });

  it("shows the mail server's refusal in the API's words", async () => {
    const recipient = practiceRecipient({canSendSecureEmail: true, name: ''});
    const {user} = setUp(<SendSecureEmailDialog letter={approvedLetter()} recipient={recipient} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/recipients/${recipient.id}/send`]: () => json({title: 'The mail server refused it: try again later.'}, 409),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByText(/goes to GP practice at/)).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Send'}));
    expect(await dialog.findByText('The mail server refused it: try again later.')).toBeInTheDocument();
  });

  it('records it as sent: how, and on which day (today to start with)', async () => {
    const onDone = vi.fn();
    const recipient = patientRecipient({canRecordSent: true});
    const days = recordableDays();
    const {user, fetch} = setUp(<RecordSentDialog letter={approvedLetter()} recipient={recipient} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/recipients/${recipient.id}/record-sent`]: () => json(approvedLetter()),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('radio', {name: 'Post'})).not.toBeChecked();
    expect(dialog.queryByRole('radio', {name: 'Secure email from MedApp'})).not.toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Record as Sent'}))).toBe(true);
    await user.click(dialog.getByRole('radio', {name: 'Handed to the patient'}));
    await user.click(dialog.getByRole('combobox', {name: /Sent on/}));
    const options = await screen.findAllByRole('option');
    expect(options).toHaveLength(8);
    expect(options[0]).toHaveTextContent(/^Today, /);
    await user.click(options[2]);
    await user.click(dialog.getByRole('button', {name: 'Record as Sent'}));
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({method: 'HandedToPatient', sentOn: days[2], expectedVersion: 3});
  });

  it('shows a 400 on the date', async () => {
    const recipient = patientRecipient({canRecordSent: true});
    const {user} = setUp(<RecordSentDialog letter={approvedLetter()} recipient={recipient} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/recipients/${recipient.id}/record-sent`]: () =>
        json({title: 'Invalid', status: 400, errors: {SentOn: ["It can't be before the letter was approved."]}}, 400),
    });
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('radio', {name: 'Post'}));
    await user.click(dialog.getByRole('button', {name: 'Record as Sent'}));
    expect((await dialog.findAllByText("It can't be before the letter was approved.")).length).toBeGreaterThan(0);
  });

  it('counts the days back from today in the UK', () => {
    expect(recordableDays(new Date('2026-11-18T23:30:00Z'))).toEqual([
      '2026-11-18',
      '2026-11-17',
      '2026-11-16',
      '2026-11-15',
      '2026-11-14',
      '2026-11-13',
      '2026-11-12',
      '2026-11-11',
    ]);
    // Just after midnight in British Summer Time, it's already the next day in the UK.
    expect(recordableDays(new Date('2026-07-01T23:30:00Z'))[0]).toBe('2026-07-02');
  });
});

describe('letter dialogs, for a letter missing its details', {timeout: 30_000}, () => {
  afterEach(() => vi.unstubAllGlobals());

  // No ID or version (the API always sends them): each dialog still sends what it has, from version 0.
  const bare = (fields: Parameters<typeof testLetter>[0] = {}) => testLetter({id: undefined, version: undefined, ...fields});
  const sends = (path: string) => ({[`POST /api/letters//${path}`]: () => json(approvedLetter())});
  const sent = async (fetch: ReturnType<typeof mockApi>) => body(fetch);

  it('submits, approves, returns and discards from version 0', async () => {
    const onDone = vi.fn();
    const submit = setUp(<SubmitLetterDialog letter={bare()} onCancel={vi.fn()} onDone={onDone} />, {
      'GET /api/calendar/staff': () => json([{userId: LETTER_APPROVER_ID, username: 'd.clinician'}, {userId: undefined, username: null}]),
      ...sends('submit'),
    });
    let dialog = within(await dialogOpen());
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Approver/}))).toBe(false));
    await submit.user.click(dialog.getByRole('combobox', {name: /Approver/}));
    await submit.user.click(await screen.findByRole('option', {name: 'd.clinician'}));
    await submit.user.click(dialog.getByRole('button', {name: 'Submit'}));
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1));
    expect(await sent(submit.fetch)).toEqual({approverUserId: LETTER_APPROVER_ID, expectedVersion: 0});
    submit.unmount();

    const approve = setUp(<ApproveLetterDialog letter={bare({canApprove: true})} onCancel={vi.fn()} onDone={onDone} />, sends('approve'));
    dialog = within(await dialogOpen());
    await approve.user.click(dialog.getByRole('button', {name: 'Approve'}));
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(2));
    expect(await sent(approve.fetch)).toEqual({expectedVersion: 0});
    approve.unmount();

    const back = setUp(<ReturnLetterDialog letter={bare({status: 'AwaitingApproval'})} onCancel={vi.fn()} onDone={onDone} />, sends('return'));
    dialog = within(await dialogOpen());
    await back.user.type(dialog.getByRole('textbox', {name: /What needs changing/}), 'Synthetic.');
    await back.user.click(dialog.getByRole('button', {name: 'Return'}));
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(3));
    expect(await sent(back.fetch)).toEqual({comment: 'Synthetic.', expectedVersion: 0});
    back.unmount();

    const discard = setUp(<DiscardLetterDialog letter={bare()} onCancel={vi.fn()} onDone={onDone} />, sends('discard'));
    dialog = within(await dialogOpen());
    await discard.user.click(dialog.getByRole('button', {name: 'Discard Draft'}));
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(4));
    expect(await sent(discard.fetch)).toEqual({expectedVersion: 0});
  });

  it('withdraws, sends and records one sent to a recipient missing its details, from version 0', async () => {
    const onDone = vi.fn();
    const recipient = practiceRecipient({id: undefined, name: '', canSendSecureEmail: true, canRecordSent: true});
    const withdraw = setUp(<WithdrawLetterDialog letter={approvedLetter({id: undefined, version: undefined, recipients: undefined})} onCancel={vi.fn()} onDone={onDone} />, sends('withdraw'));
    let dialog = within(await dialogOpen());
    await withdraw.user.type(dialog.getByRole('textbox', {name: /Why it's withdrawn/}), 'Synthetic.');
    await withdraw.user.click(dialog.getByRole('button', {name: 'Withdraw Letter'}));
    await waitFor(() => expect(onDone).toHaveBeenCalledTimes(1));
    expect(await sent(withdraw.fetch)).toEqual({reason: 'Synthetic.', expectedVersion: 0});
    withdraw.unmount();

    const email = setUp(<SendSecureEmailDialog letter={approvedLetter({id: undefined, version: undefined})} recipient={recipient} onCancel={vi.fn()} onDone={onDone} />, sends('recipients//send'));
    dialog = within(await dialogOpen());
    await email.user.click(dialog.getByRole('button', {name: 'Send'}));
    await waitFor(() => expect(email.fetch).toHaveBeenCalled());
    email.unmount();

    const record = setUp(<RecordSentDialog letter={approvedLetter({id: undefined, version: undefined})} recipient={recipient} onCancel={vi.fn()} onDone={onDone} />, sends('recipients//record-sent'));
    dialog = within(await dialogOpen());
    expect(dialog.getByText(/^How it reached GP practice, from the PDF\.$/)).toBeInTheDocument();
    await record.user.click(dialog.getByRole('radio', {name: 'Post'}));
    await record.user.click(dialog.getByRole('button', {name: 'Record as Sent'}));
    await waitFor(() => expect(record.fetch).toHaveBeenCalled());
    expect(await sent(record.fetch)).toEqual(expect.objectContaining({method: 'Post', expectedVersion: 0}));
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import {
  alertLabelName,
  calls,
  expectAnnounced,
  findAlert,
  isDisabled,
  json,
  mockApi,
  openDialog,
  renderApp,
  renderWithApp,
  staffUser,
  userWithRoles,
  withAnnouncements,
} from '../../test/render';
import {approvedLetter, LETTER_ID, LETTER_PDF_ID, patientRecipient, practiceRecipient, testLetter} from '../../test/letters';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import type {Letter} from '../../api/letters';
import {HalfViewContext} from '../../components/pages/halfView';
import {bodyHeadings, LetterPage} from './LetterPage';

const clinician = userWithRoles('test-clinician', 'Clinician');
const BASE = `/api/letters/${LETTER_ID}`;

function render(letter: Letter | (() => Response), handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}`]: typeof letter === 'function' ? letter : () => json(letter),
    'GET /api/practices': () => json([]),
    'GET /api/letter-templates/merge-fields': () => json([{name: 'patient.address', label: "Patient's address"}]),
    ...handlers,
  });
  return {fetch, ...renderWithApp(<LetterPage id={LETTER_ID} />, {user: clinician})};
}

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

const sentBody = async (fetch: ReturnType<typeof mockApi>, method: string) =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method) as Request).json();

describe('LetterPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("finds a body's headings", () => {
    expect(bodyHeadings('# Diagnosis\ntext\n## Plan ##\n#not a heading\n   ### Follow-up  ')).toEqual(['Diagnosis', 'Plan', 'Follow-up']);
    expect(bodyHeadings('No headings')).toEqual([]);
  });

  it("edits its author's draft and saves it from the version it started from", async () => {
    withAnnouncements();
    const {user, fetch} = render(testLetter(), {[`PUT ${BASE}`]: () => json(testLetter({version: 4, body: 'Changed'}))});

    expect(await screen.findByRole('heading', {name: 'Discharge summary', level: 1})).toBeInTheDocument();
    expect(screen.getByText('Dermatology discharge · Draft')).toBeInTheDocument();
    expectPatientFirstInDetails();
    expect(screen.getByRole('link', {name: 'Download Preview'})).toHaveAttribute('href', `/api/letters/${LETTER_ID}/preview`);
    expect(screen.getByRole('link', {name: 'Skin check'})).toHaveAttribute('href', '/pathways/00000061-0000-0000-0000-000000000003');

    const body = screen.getByRole('textbox', {name: /Body/});
    expect(body).toHaveValue(testLetter().body);
    expect(screen.getByText(/Write under each heading: Diagnosis, Plan\./)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Save'}))).toBe(true);

    await user.type(screen.getByRole('textbox', {name: /Closing/}), '!');
    await user.click(screen.getByRole('checkbox', {name: /Large print/}));
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    // Submitting waits until the changes are saved.
    expect(isDisabled(screen.getByRole('button', {name: 'Submit for Approval'}))).toBe(true);
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await expectAnnounced('Letter saved');
    expect(await sentBody(fetch, 'PUT')).toMatchObject({closing: `${testLetter().closing}!`, largePrint: true, expectedVersion: 3});
    await waitFor(() => expect(screen.getByRole('textbox', {name: /Body/})).toHaveValue('Changed'));
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument();
  });

  it('edits the recipients with the letter, and shows their errors with them', async () => {
    const {user, fetch} = render(testLetter({recipients: [patientRecipient()], body: 'Plain words'}), {
      [`PUT ${BASE}`]: () => json({title: 'Invalid', status: 400, errors: {'Recipients[1].Name': ["Give the recipient's name."], Body: ['Too long.']}}, 400),
    });

    expect(await screen.findByText(/Merge fields such as/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Add Recipient'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Someone else'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));

    expect(await screen.findByText("Give the recipient's name.")).toBeInTheDocument();
    expect(screen.getAllByText('Too long.').length).toBeGreaterThan(0);
    expect((await sentBody(fetch, 'PUT')).recipients).toEqual([
      {kind: 'Patient'},
      {kind: 'Other', name: null, addressLines: null, postcode: null, email: null},
    ]);
  });

  it('shows a 400 with no field in the editor, and a 409 with Reload, which reads it again and drops the changes', async () => {
    let attempt = 0;
    const {user, fetch} = render(testLetter(), {
      [`PUT ${BASE}`]: () =>
        ++attempt === 1
          ? json({title: 'Invalid', status: 400, errors: {'': ['Something about the letter.']}}, 400)
          : json({title: 'You saved this letter in another tab. Reload to see it.'}, 409),
    });

    await user.type(await screen.findByRole('textbox', {name: /Letterhead/}), '!');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await screen.findByText('Something about the letter.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await findAlert('You saved this letter in another tab. Reload to see it.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Reload'}));
    await waitFor(() => expect(screen.getByRole('textbox', {name: /Letterhead/})).toHaveValue(testLetter().letterhead));
    expect(calls(fetch).filter((c) => c === `GET ${BASE}`).length).toBeGreaterThanOrEqual(2);
  });

  it('lists the merge fields with no value, and the comment a returned draft came back with', async () => {
    render(
      testLetter({
        isReturned: true,
        returnComment: 'Add the follow-up plan.',
        returnedByUsername: 'd.clinician',
        returnedAtUtc: '2026-11-17T10:00:00Z',
        missingMergeFields: ['patient.address', 'practice.name'],
      }),
    );

    expect(await screen.findByText(/^Returned by d.clinician on 17 Nov 2026, 10:00: Add the follow-up plan\./)).toBeInTheDocument();
    expect(await screen.findByText("Patient's address ({{patient.address}})")).toBeInTheDocument();
    expect(screen.getByText('{{practice.name}}')).toBeInTheDocument();
  });

  it('submits for approval, then announces it', async () => {
    withAnnouncements();
    const {user} = render(testLetter(), {
      'GET /api/calendar/staff': () => json([{userId: 'a1', username: 'd.clinician'}]),
      [`POST ${BASE}/submit`]: () => json(testLetter({status: 'AwaitingApproval', canEdit: false, canSubmit: false})),
    });

    await user.click(await screen.findByRole('button', {name: 'Submit for Approval'}));
    const dialog = within(await dialogOpen());
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Approver/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Approver/}));
    await user.click(await screen.findByRole('option', {name: 'd.clinician'}));
    await user.click(dialog.getByRole('button', {name: 'Submit'}));
    await expectAnnounced('Letter submitted for approval');
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('shows an approver the letter as written, with Approve and Return', async () => {
    withAnnouncements();
    const awaiting = testLetter({
      status: 'AwaitingApproval',
      approverUsername: 'test-clinician',
      submittedAtUtc: '2026-11-17T09:00:00Z',
      largePrint: true,
      canEdit: false,
      canSubmit: false,
      canDiscard: false,
      canApprove: true,
      canReturn: true,
    });
    const {user} = render(awaiting, {
      [`POST ${BASE}/approve`]: () => json(approvedLetter()),
    });

    expect(await screen.findByRole('heading', {name: 'Diagnosis'})).toBeInTheDocument();
    expect(screen.getByText('Large print')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: /Body/})).not.toBeInTheDocument();
    expect(screen.getByText('Submitted')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Return'}));
    expect(within(await dialogOpen()).getByRole('heading', {name: 'Return to Author?'})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'Approve'}));
    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('list', {name: 'Recipients'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Approve'}));
    await expectAnnounced('Letter approved');
  });

  it("once approved: the PDF, each recipient's dispatch, sending, recording and withdrawing", async () => {
    const {user} = render(approvedLetter());

    expect(await screen.findByRole('link', {name: 'Download PDF'})).toHaveAttribute('href', `/api/attachments/${LETTER_PDF_ID}/content`);
    expect(screen.queryByRole('link', {name: 'Download Preview'})).not.toBeInTheDocument();
    expect(screen.getByText('Dermatology discharge · To send')).toBeInTheDocument();
    const recipients = within(screen.getByRole('list', {name: 'Recipients'}));
    expect(recipients.getAllByText('Not sent')).toHaveLength(2);
    expect(recipients.getByText('Letters go to patients by post or by hand, never by email.')).toBeInTheDocument();

    await user.click(recipients.getByRole('button', {name: 'Send by Secure Email'}));
    expect(within(await dialogOpen()).getByRole('heading', {name: 'Send by Secure Email?'})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(recipients.getAllByRole('button', {name: 'Record as Sent'})[1]);
    expect(within(await dialogOpen()).getByText(/How it reached Freya Ahmed/)).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: /Withdraw/}));
    expect(within(await dialogOpen()).getByRole('heading', {name: 'Withdraw Letter?'})).toBeInTheDocument();
  });

  it("says secure email isn't set up here", async () => {
    render(approvedLetter({secureEmailAvailable: false, recipients: [practiceRecipient({canRecordSent: true})]}));
    expect(await screen.findByText(/Secure email from MedApp isn't set up here/)).toBeInTheDocument();
  });

  it('after withdrawal: why, and Write a Correction, which opens the new draft', async () => {
    withAnnouncements();
    const withdrawn = approvedLetter({
      status: 'Withdrawn',
      withdrawnAtUtc: '2026-11-18T09:00:00Z',
      withdrawnByUsername: 'd.clinician',
      withdrawnReason: 'The plan was wrong.',
      canWithdraw: false,
      canWriteCorrection: true,
      recipients: [practiceRecipient({dispatchStatus: 'Sent', method: 'SecureEmail', sentOn: '2026-11-17'})],
    });
    const {user, router} = render(withdrawn, {[`POST ${BASE}/correction`]: () => json(testLetter({id: 'c1', replacesLetterId: LETTER_ID}), 201)});

    expect(await screen.findByText('The plan was wrong.')).toBeInTheDocument();
    expect(screen.getByText(/by d.clinician$/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Write a Correction'}));
    await expectAnnounced('Correction started');
    await waitFor(() => expect(router.state.location.pathname).toBe('/letters/c1'));
  });

  it("links a correction and the letter it replaces, and says when a correction can't be started", async () => {
    const {user} = render(
      approvedLetter({status: 'Withdrawn', canWithdraw: false, canWriteCorrection: true, replacesLetterId: 'old', replacedByLetterId: 'new'}),
      {[`POST ${BASE}/correction`]: () => json({title: 'A correction has already been written.'}, 409)},
    );
    expect(await screen.findByRole('link', {name: 'It corrects a withdrawn letter'})).toHaveAttribute('href', '/letters/old');
    expect(screen.getByRole('link', {name: 'Its correction'})).toHaveAttribute('href', '/letters/new');
    await user.click(screen.getByRole('button', {name: 'Write a Correction'}));
    expect(await findAlert('A correction has already been written.')).toBeInTheDocument();
  });

  it('discards a draft, then goes back to the list', async () => {
    withAnnouncements();
    const {user, router} = render(testLetter(), {[`POST ${BASE}/discard`]: () => json(testLetter())});

    await user.click(await screen.findByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: 'Discard Draft'}));
    await user.click(within(await dialogOpen()).getByRole('button', {name: 'Discard Draft'}));
    await expectAnnounced('Draft discarded');
    await waitFor(() => expect(router.state.location.pathname).toBe('/letters'));
  });

  it("a letter about a referral links to it; a restricted record shows its banner; one that can't change says why", async () => {
    const {unmount} = render(
      testLetter({
        pathwayId: null,
        referralId: 'r1',
        canEdit: false,
        canSubmit: false,
        canDiscard: false,
        canPreview: false,
        patientFlags: {isRestricted: true, isDisputed: false, isAnonymised: false, restrictionReason: 'Patient asked.'},
      }),
    );
    expect(await screen.findByRole('link', {name: 'The referral'})).toHaveAttribute('href', '/referrals/r1');
    expect(screen.getByRole('button', {name: alertLabelName('Restricted record')})).toBeInTheDocument();
    unmount();

    render(testLetter({canEdit: false, canSubmit: false, canDiscard: false, readOnlyReason: 'The record is anonymised.'}));
    expect(await screen.findByRole('button', {name: /^Read-only letter/})).toBeInTheDocument();
  });

  it("says when the letter isn't found, or can't be loaded", async () => {
    const {unmount} = render(() => json({title: 'Not found'}, 404));
    expect(await screen.findByText('Letter not found. It may have been discarded.')).toBeInTheDocument();
    unmount();
    render(() => json({title: 'Boom'}, 500));
    expect(await screen.findByText('The letter could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('folds each section into one card in a narrow window or half view', async () => {
    mockApi({[`GET ${BASE}`]: () => json(approvedLetter()), 'GET /api/letter-templates/merge-fields': () => json([])});
    const {user} = renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <LetterPage id={LETTER_ID} />
      </HalfViewContext.Provider>,
      {user: clinician},
    );

    expect(await screen.findByRole('heading', {name: 'Diagnosis'})).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: 'Letter'}));
    await waitFor(() => expect(screen.queryByRole('heading', {name: 'Diagnosis'})).not.toBeInTheDocument());
    // Details, first with the patient, never folds.
    expectPatientFirstInDetails();
    await user.click(screen.getByRole('heading', {name: 'Details'}));
    expect(screen.getByText('Dermatology discharge')).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: 'Recipients'}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Recipients'})).not.toBeInTheDocument());
  });

  it('shows a letter with little filled in, and types into its body', async () => {
    const sparse = testLetter({
      pathwayId: null,
      pathwayTitle: null,
      referralId: null,
      templateName: '',
      authorUsername: '',
      createdAt: undefined,
      body: '',
      returnComment: 'Check it.',
      isReturned: true,
      returnedByUsername: null,
      returnedAtUtc: null,
      missingMergeFields: null,
      recipients: null,
      canApprove: true,
    });
    const {user} = render(sparse);

    expect(await screen.findByText('Returned by the approver: Check it.')).toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(4);
    expect(screen.getByText(/No recipients yet/)).toBeInTheDocument();
    // The author may approve their own: Submit for Approval is then the second choice.
    expect(screen.getByRole('button', {name: 'Approve'})).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', {name: /Body/}), '# Plan');
    expect(screen.getByText(/Write under each heading: Plan\./)).toBeInTheDocument();
  });

  it("dismisses a refusal's message", async () => {
    const {user} = render(testLetter(), {[`PUT ${BASE}`]: () => json({title: 'Changed in another tab.'}, 409)});
    await user.type(await screen.findByRole('textbox', {name: /Letterhead/}), '!');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await findAlert('Changed in another tab.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: /Dismiss|Close/}));
    await waitFor(() => expect(screen.queryByText('Changed in another tab.')).not.toBeInTheDocument());
  });

  it('shows a withdrawn letter with no pathway title, and no one recorded as withdrawing it', async () => {
    render(approvedLetter({status: 'Withdrawn', pathwayTitle: '', withdrawnAtUtc: '2026-11-18T09:00:00Z', canWithdraw: false}));
    expect(await screen.findByRole('link', {name: 'Its pathway'})).toBeInTheDocument();
    expect(screen.getByText('18 Nov 2026, 09:00')).toBeInTheDocument();
  });

  it('opens at /letters/$id (the route)', async () => {
    mockApi({[`GET ${BASE}`]: () => json(testLetter({canEdit: false})), 'GET /api/letter-templates/merge-fields': () => json([])});
    renderApp({path: `/letters/${LETTER_ID.toUpperCase()}`, user: staffUser});
    expect(await screen.findByRole('heading', {name: 'Discharge summary', level: 1})).toBeInTheDocument();
  });
});


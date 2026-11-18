import {screen, waitFor, within} from '@testing-library/react';
import type {Referral} from '../../api/referrals';
import {HalfViewContext} from '../../components/pages/halfView';
import {
  alertLabelName,
  expectAnnounced,
  findAlert,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  staffUser,
  withAnnouncements,
} from '../../test/render';
import {REFERRAL_ID, REFERRAL_PATIENT_ID, testReferral} from '../../test/referrals';
import {ReferralPage, rejectionReasonWords, triageWords} from './ReferralPage';

const REFERRAL = `GET /api/referrals/${REFERRAL_ID}`;
const CHANGED = 'Someone changed this referral meanwhile. Reload it to see what they changed.';

function renderPage(referral: Referral | (() => Response) = testReferral(), handlers: Record<string, (request: Request) => Response | Promise<Response>> = {}, isNarrow = false) {
  const fetch = mockApi({
    [REFERRAL]: typeof referral === 'function' ? referral : () => json(referral),
    'GET /api/attachments': () => json({items: [], totalCount: 0, maxFileBytes: 20971520, allowedExtensions: ['.pdf']}),
    ...handlers,
  });
  const page = <ReferralPage id={REFERRAL_ID} />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {
    user: staffUser,
  });
  return {...result, fetch};
}

describe('ReferralPage', () => {
  it('shows the patient, the details from the letter, the letter, triage and the history', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Referral to Dermatology', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(screen.getAllByText('Awaiting triage').length).toBeGreaterThan(0);
    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getAllByText('2 November 2026').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Synthetic Street Surgery').length).toBeGreaterThan(0);
    expect(screen.getByText('Dr Test')).toBeInTheDocument();
    expect(screen.getByText('A12345')).toBeInTheDocument();
    expect(screen.getByText('A synthetic reason for referral.')).toBeInTheDocument();
    expect(screen.getByText('2 Nov 2026, 10:00 by k.ashford')).toBeInTheDocument();
    expect(await screen.findByText('No letter attached yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: /Add files/})).toBeInTheDocument();
    expect(screen.getByText(/Triage isn't available in MedApp yet/)).toBeInTheDocument();
    const history = within(screen.getByRole('list', {name: 'History'}));
    expect(history.getByText('Registered by k.ashford')).toBeInTheDocument();
    expect(history.getByText('To Dermatology, High priority asked for')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Edit Details'})).toHaveAttribute('href', `/referrals/${REFERRAL_ID}/edit`);
    expect(screen.getByRole('link', {name: 'View Patient'})).toHaveAttribute('href', `/patients/${REFERRAL_PATIENT_ID}`);
    expect(screen.getByRole('button', {name: 'More'})).toBeInTheDocument();
  });

  it('withdraws it after asking why, with the patient shown, from the version on the page', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(testReferral(), {
      [`POST /api/referrals/${REFERRAL_ID}/withdraw`]: () =>
        json(testReferral({status: 'Withdrawn', isOpen: false, canEdit: false, canClose: false, readOnlyReason: 'This referral is withdrawn.'})),
    });

    await user.click(await screen.findByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: /Withdraw/}));
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    await user.type(dialog.getByRole('textbox', {name: /Who withdrew it/}), 'The practice phoned');
    await user.click(dialog.getByRole('button', {name: 'Withdraw Referral'}));

    await expectAnnounced('Referral withdrawn');
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({detail: 'The practice phoned', expectedVersion: 2});
    expect(await screen.findByRole('button', {name: alertLabelName('Read-only referral')})).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Edit Details'})).not.toBeInTheDocument();
  });

  it("marks it entered in error, showing a 400 in the dialog and a 409 on the page with Reload", async () => {
    let attempt = 0;
    const {user, fetch} = renderPage(testReferral(), {
      [`POST /api/referrals/${REFERRAL_ID}/entered-in-error`]: () =>
        ++attempt === 1
          ? json({title: 'Invalid', errors: {Detail: ['Say what was wrong.']}}, 400)
          : json({title: CHANGED, status: 409}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: /Entered in Error/}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.type(dialog.getByRole('textbox', {name: /What was wrong/}), 'x');
    await user.click(dialog.getByRole('button', {name: 'Mark Entered in Error'}));
    expect(await dialog.findByText('Say what was wrong.')).toBeInTheDocument();

    await user.click(dialog.getByRole('button', {name: 'Mark Entered in Error'}));
    expect(await findAlert(CHANGED)).toBeInTheDocument();
    await waitFor(() => expect(openDialog()).toBeNull());
    const reads = () => fetch.mock.calls.filter(([r]) => (r as Request).method === 'GET' && (r as Request).url.endsWith(REFERRAL_ID)).length;
    const before = reads();
    await user.click(screen.getByRole('button', {name: 'Reload'}));
    await waitFor(() => expect(reads()).toBeGreaterThan(before));
    expect(screen.queryByText(CHANGED)).not.toBeInTheDocument();
  });

  it('offers nothing to change once closed, and says what became of it', async () => {
    renderPage(
      testReferral({
        status: 'Rejected',
        isOpen: false,
        canEdit: false,
        canClose: false,
        canAttachFiles: false,
        decidedAtUtc: '2026-11-04T09:00:00Z',
        decidedByUsername: 'd.clinician',
        rejectionReason: 'DoesNotMeetCriteria',
        decisionDetail: 'Not for this clinic.',
        redirectedTo: 'Another provider',
        pathwayId: 'pathway-1',
        referrerClinician: null,
        readOnlyReason: 'This referral has been triaged.',
        history: [],
        letterCount: 1,
      }),
    );

    expect(await screen.findByText('Rejected 4 Nov 2026, 09:00 by d.clinician')).toBeInTheDocument();
    expect(screen.getByText("Why: Doesn't meet the criteria")).toBeInTheDocument();
    expect(screen.getByText('Redirected to Another provider')).toBeInTheDocument();
    expect(screen.getByText('Not for this clinic.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open its pathway'})).toHaveAttribute('href', '/pathways/pathway-1');
    expect(screen.getByText("This referral is rejected, so it isn't triaged.")).toBeInTheDocument();
    expect(screen.getByText('Nothing recorded yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'More'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /Add files/})).not.toBeInTheDocument();
  });

  it("shows the patient's banners, and nothing can be attached while the record is restricted", async () => {
    renderPage(
      testReferral({
        patientFlags: {isRestricted: true, restrictionReason: 'Patient asked', restrictedAtUtc: '2026-11-01T09:00:00Z', isDisputed: false, isAnonymised: false},
        canEdit: false,
        readOnlyReason: 'The record is restricted.',
      }),
    );

    expect(await screen.findByRole('button', {name: alertLabelName('Restricted record')})).toBeInTheDocument();
    expect(await screen.findByText('No letter attached yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /Add files/})).not.toBeInTheDocument();
  });

  it('says when it is missing or could not be loaded', async () => {
    const {unmount} = renderPage(() => json({title: 'Not found'}, 404));
    expect(await findAlert('Referral not found. It may have been deleted.')).toBeInTheDocument();
    unmount();

    renderPage(() => json({title: 'Down'}, 500));
    expect(await findAlert('The referral could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('folds into one card in a narrow window or half view', async () => {
    const {user} = renderPage(testReferral(), {}, true);
    const history = await screen.findByRole('heading', {name: 'History'});
    expect(screen.getByRole('heading', {name: 'Letter'})).toBeInTheDocument();
    expect(screen.getByText('Registered by k.ashford')).toBeInTheDocument();
    await user.click(history);
    await waitFor(() => expect(screen.queryByText('Registered by k.ashford')).not.toBeInTheDocument());
  });
});

describe('referral words', () => {
  it('words rejection reasons and triage', () => {
    expect(rejectionReasonWords('Duplicate')).toBe('A duplicate');
    expect(rejectionReasonWords('Unknown')).toBe('Unknown');
    expect(rejectionReasonWords(null)).toBe('');
    expect(triageWords({isOpen: true, status: 'AwaitingTriage', whyCantTriage: 'You are not a Dermatology clinician.'})).toBe(
      'You are not a Dermatology clinician.',
    );
  });
});

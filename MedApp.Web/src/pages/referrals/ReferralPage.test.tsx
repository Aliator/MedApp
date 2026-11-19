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
  userWithRoles,
  withAnnouncements,
} from '../../test/render';
import {DERMATOLOGY_ID, REFERRAL_ID, REFERRAL_PATIENT_ID, TEAMS, testReferral, testReferralSummary} from '../../test/referrals';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import {ReferralPage, triageWords} from './ReferralPage';

const REFERRAL = `GET /api/referrals/${REFERRAL_ID}`;
const CHANGED = 'Someone changed this referral meanwhile. Reload it to see what they changed.';

function renderPage(
  referral: Referral | (() => Response) = testReferral(),
  handlers: Record<string, (request: Request) => Response | Promise<Response>> = {},
  isNarrow = false,
  user = staffUser,
) {
  const fetch = mockApi({
    [REFERRAL]: typeof referral === 'function' ? referral : () => json(referral),
    'GET /api/attachments': () => json({items: [], totalCount: 0, maxFileBytes: 20971520, allowedExtensions: ['.pdf']}),
    'GET /api/referrals/search': () =>
      json({
        items: [
          testReferralSummary({id: REFERRAL_ID}),
          testReferralSummary({id: 'other-referral', status: 'Withdrawn', teamName: 'Urology', receivedOn: '2026-10-01'}),
        ],
        totalCount: 2,
      }),
    ...handlers,
  });
  const page = <ReferralPage id={REFERRAL_ID} />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {user});
  return {...result, fetch};
}

describe('ReferralPage', () => {
  it('shows the patient, the details from the letter, the letter, triage and the history', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Referral to Dermatology', level: 1})).toBeInTheDocument();
    expectPatientFirstInDetails();
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
    expect(screen.getByText(/Only a member of its team who can triage referrals/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Accept'})).not.toBeInTheDocument();
    const history = within(screen.getByRole('list', {name: 'History'}));
    expect(history.getByText('Registered by k.ashford')).toBeInTheDocument();
    expect(history.getByText('To Dermatology, High priority asked for')).toBeInTheDocument();
    // The patient's other referrals, not this one, so a duplicate is seen before triage (hazard H-44).
    expect(screen.getByRole('heading', {name: "This patient's other referrals"})).toBeInTheDocument();
    const others = within(await screen.findByRole('list', {name: 'Other referrals'}));
    expect(others.getAllByRole('link', {name: /^Referral to/}).map((link) => link.textContent)).toEqual(['Referral to Urology']);
    expect(screen.getByRole('link', {name: 'Edit Details'})).toHaveAttribute('href', `/referrals/${REFERRAL_ID}/edit`);
    expect(screen.getByRole('link', {name: 'View Patient'})).toHaveAttribute('href', `/patients/${REFERRAL_PATIENT_ID}`);
    expect(screen.getByRole('button', {name: 'More'})).toBeInTheDocument();
  });

  it('says when the practice was chosen from the directory', async () => {
    renderPage(testReferral({practiceId: 'p-test', referrerPractice: 'Testbrook Surgery'}));

    expect(await screen.findByText('Testbrook Surgery (from the directory)')).toBeInTheDocument();
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
        canTriage: true,
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
    expect(screen.getByText('Why: Does not meet the criteria')).toBeInTheDocument();
    expect(screen.getByText('Redirected to Another provider')).toBeInTheDocument();
    expect(screen.getByText('Not for this clinic.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open its pathway'})).toHaveAttribute('href', '/pathways/pathway-1');
    expect(screen.getByText("This referral has been triaged: rejected. A referral isn't triaged again.")).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reject'})).not.toBeInTheDocument();
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

  it("dismisses a 409's message, and cancels withdrawing", async () => {
    const {user} = renderPage(testReferral(), {
      [`POST /api/referrals/${REFERRAL_ID}/withdraw`]: () => json({title: CHANGED, status: 409}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: /Withdraw/}));
    let dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: /Withdraw/}));
    dialog = within(await waitFor(() => openDialog()!));
    await user.type(dialog.getByRole('textbox', {name: /Who withdrew it/}), 'The practice phoned');
    await user.click(dialog.getByRole('button', {name: 'Withdraw Referral'}));
    expect(await findAlert(CHANGED)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: /Dismiss/}));
    await waitFor(() => expect(screen.queryByText(CHANGED)).not.toBeInTheDocument());
  });

  it('folds each of its sections but Details, first with the patient, in a narrow window', async () => {
    const {user} = renderPage(testReferral(), {}, true);

    await screen.findByRole('heading', {name: 'Details'});
    expectPatientFirstInDetails();
    expect(screen.getByText('A synthetic reason for referral.')).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: 'Details'}));
    expect(screen.getByText('A synthetic reason for referral.')).toBeInTheDocument();
    expect(screen.getByText('This referral has no waiting-time clock.')).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: 'Waiting time'}));
    await waitFor(() => expect(screen.queryByText('This referral has no waiting-time clock.')).not.toBeInTheDocument());
    expect(await screen.findByText('No letter attached yet.')).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: 'Letter'}));
    await waitFor(() => expect(screen.queryByText('No letter attached yet.')).not.toBeInTheDocument());
    await user.click(screen.getByRole('heading', {name: 'Triage'}));
    await waitFor(() => expect(screen.queryByText(/Only a member of its team/)).not.toBeInTheDocument());
    expect(await screen.findByRole('list', {name: 'Other referrals'})).toBeInTheDocument();
    await user.click(screen.getByRole('heading', {name: "This patient's other referrals"}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Other referrals'})).not.toBeInTheDocument());
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
  it('words triage: why not, what a triager can do, and a referral already closed', () => {
    expect(triageWords({isOpen: true, status: 'AwaitingTriage', canTriage: false, whyCantTriage: 'You are not a Dermatology clinician.'})).toBe(
      'You are not a Dermatology clinician.',
    );
    expect(triageWords({isOpen: true, status: 'AwaitingTriage', canTriage: true})).toMatch(/^Read the referral and its letter, then decide/);
    expect(triageWords({isOpen: false, status: 'Accepted'})).toBe("This referral has been triaged: accepted. A referral isn't triaged again.");
    expect(triageWords({isOpen: false, status: 'Withdrawn'})).toBe("This referral is withdrawn, so it isn't triaged.");
    expect(triageWords({isOpen: true, status: 'InformationRequested', canTriage: true})).toMatch(/^More information has been asked of the referrer/);
  });

  it("lists the referral's letters, with Write Letter for a letter writer", async () => {
    renderPage(testReferral(), {
      'GET /api/letters/search': () =>
        json({items: [{id: 'l1', kind: 'ReferralResponse', templateName: 'Referral reply', status: 'Draft', authorUsername: 'k.ashford', lastUpdated: '2026-11-17T10:00:00Z'}], totalCount: 1}),
    }, false, userWithRoles('d.clinician', 'Clinician'));

    expect(await screen.findByRole('link', {name: 'Referral response: Referral reply'})).toHaveAttribute('href', '/letters/l1');
    expect(screen.getByRole('button', {name: 'Write Letter'})).toBeInTheDocument();
  });
});

describe('ReferralPage triage', () => {
  const clinician = userWithRoles('d.clinician', 'Clinician');
  const triager = testReferral({canTriage: true});
  const pathwayId = '00000042-0000-0000-0000-0000000000aa';
  const templates = [{id: 't-derm', name: 'Skin check', isArchived: false, teamId: DERMATOLOGY_ID, stages: [{stageOrder: 1, name: 'Review'}]}];

  it("shows why someone can't triage it, in the API's words, with no actions", async () => {
    renderPage(testReferral({canTriage: false, whyCantTriage: 'Only a member of Dermatology can triage this referral.'}), {}, false, clinician);

    expect(await screen.findByText('Only a member of Dermatology can triage this referral.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Accept'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reject'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Redirect'})).not.toBeInTheDocument();
  });

  it('accepts it, then opens the new pathway', async () => {
    withAnnouncements();
    const {user, router, fetch} = renderPage(
      triager,
      {
        'GET /api/pathways/patient-pathway-stage/templates': () => json(templates),
        [`POST /api/referrals/${REFERRAL_ID}/accept`]: () => json({id: pathwayId, patientId: REFERRAL_PATIENT_ID, title: 'Skin check'}, 201),
        [`GET /api/pathways/patient-pathways/${pathwayId}`]: () => json({id: pathwayId, patientId: REFERRAL_PATIENT_ID, title: 'Skin check'}),
      },
      false,
      clinician,
    );

    expect(await screen.findByText(/^Read the referral and its letter/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Accept'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: 'Skin check'}));
    await user.click(dialog.getByRole('combobox', {name: /Priority/}));
    await user.click(await screen.findByRole('option', {name: 'Urgent'}));
    await user.click(dialog.getByRole('button', {name: 'Continue'}));
    await user.click(await dialog.findByRole('button', {name: 'Accept Referral'}));

    await expectAnnounced('Referral accepted: pathway created');
    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${pathwayId}`));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    const body = await post.json();
    expect(body).toMatchObject({templateId: 't-derm', priority: 'Urgent', expectedVersion: 2});
    expect(body).not.toHaveProperty('dueDate');
  });

  it('rejects it with a reason, and shows the outcome', async () => {
    withAnnouncements();
    const rejected = testReferral({
      status: 'Rejected',
      isOpen: false,
      canEdit: false,
      canClose: false,
      canTriage: true,
      decidedAtUtc: '2026-11-04T09:00:00Z',
      decidedByUsername: 'd.clinician',
      rejectionReason: 'NotOfferedHere',
      decisionDetail: 'Not seen at this centre.',
    });
    const {user, fetch} = renderPage(triager, {[`POST /api/referrals/${REFERRAL_ID}/reject`]: () => json(rejected)}, false, clinician);

    await user.click(await screen.findByRole('button', {name: 'Reject'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('combobox', {name: /Reason/}));
    await user.click(await screen.findByRole('option', {name: 'Not offered here'}));
    await user.type(dialog.getByRole('textbox', {name: /Detail/}), 'Not seen at this centre.');
    await user.click(dialog.getByRole('button', {name: 'Continue'}));
    await user.click(await dialog.findByRole('button', {name: 'Reject Referral'}));

    await expectAnnounced('Referral rejected');
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({reason: 'NotOfferedHere', detail: 'Not seen at this centre.', expectedVersion: 2});
    expect(await screen.findByText('Why: Not offered here')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reject'})).not.toBeInTheDocument();
  });

  it("keeps a 409 in the dialog, in the API's words, and reads the referral again", async () => {
    const TRIAGED = 'k.ashford has already accepted this referral.';
    const {user, fetch} = renderPage(
      triager,
      {[`POST /api/referrals/${REFERRAL_ID}/reject`]: () => json({title: TRIAGED, status: 409}, 409)},
      false,
      clinician,
    );

    await user.click(await screen.findByRole('button', {name: 'Reject'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('combobox', {name: /Reason/}));
    await user.click(await screen.findByRole('option', {name: 'Duplicate'}));
    await user.click(dialog.getByRole('button', {name: 'Continue'}));
    const reads = () => fetch.mock.calls.filter(([r]) => (r as Request).method === 'GET' && (r as Request).url.endsWith(REFERRAL_ID)).length;
    const before = reads();
    await user.click(await dialog.findByRole('button', {name: 'Reject Referral'}));

    expect(await dialog.findByText(TRIAGED)).toBeInTheDocument();
    await waitFor(() => expect(reads()).toBeGreaterThan(before));
  });

  it('cancels each triage decision without sending anything', async () => {
    const {user, fetch} = renderPage(
      triager,
      {'GET /api/pathways/patient-pathway-stage/templates': () => json(templates), 'GET /api/teams': () => json(TEAMS)},
      false,
      clinician,
    );

    for (const action of ['Accept', 'Reject', 'Redirect']) {
      await user.click(await screen.findByRole('button', {name: action}));
      const dialog = within(await waitFor(() => openDialog()!));
      await user.click(dialog.getByRole('button', {name: 'Cancel'}));
      await waitFor(() => expect(openDialog()).toBeNull());
    }
    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(false);
  });

  it('redirects it to another team, announcing where', async () => {
    withAnnouncements();
    const moved = testReferral({teamId: TEAMS[1].id, teamName: 'Urology', canTriage: false, redirectDetail: 'Synthetic: a urology problem.'});
    const {user} = renderPage(
      triager,
      {'GET /api/teams': () => json(TEAMS), [`POST /api/referrals/${REFERRAL_ID}/redirect`]: () => json(moved)},
      false,
      clinician,
    );

    await user.click(await screen.findByRole('button', {name: 'Redirect'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('radio', {name: /Another clinic here/}));
    await user.click(dialog.getByRole('combobox', {name: /Clinic/}));
    await user.click(await screen.findByRole('option', {name: 'Urology'}));
    await user.type(dialog.getByRole('textbox', {name: /Why/}), 'Synthetic: a urology problem.');
    await user.click(dialog.getByRole('button', {name: 'Continue'}));
    await user.click(await dialog.findByRole('button', {name: 'Redirect Referral'}));

    await expectAnnounced('Referral redirected to Urology');
    expect(await screen.findByRole('heading', {name: 'Referral to Urology', level: 1})).toBeInTheDocument();
    expect(screen.getByText('Redirected from another team')).toBeInTheDocument();
    expect(screen.getByText('Synthetic: a urology problem.')).toBeInTheDocument();
  });

  it('shows what it was accepted into: the template and priority from its history, and the pathway', async () => {
    renderPage(
      testReferral({
        status: 'Accepted',
        isOpen: false,
        canEdit: false,
        canClose: false,
        decidedAtUtc: '2026-11-04T09:00:00Z',
        decidedByUsername: 'd.clinician',
        pathwayId,
        history: [
          {id: 'e1', action: 'TeamChanged', atUtc: '2026-11-03T10:00:00Z', username: 'u.clinician', previousTeamName: 'Urology', teamName: 'Dermatology'},
          {id: 'e2', action: 'Accepted', atUtc: '2026-11-04T09:00:00Z', username: 'd.clinician', templateName: 'Skin check', priority: 'High', pathwayId},
        ],
        redirectDetail: 'Synthetic: a skin problem.',
      }),
    );

    expect(await screen.findByText('Accepted 4 Nov 2026, 09:00 by d.clinician')).toBeInTheDocument();
    expect(screen.getByText('Template: Skin check, Priority: High')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open its pathway'})).toHaveAttribute('href', `/pathways/${pathwayId}`);
    expect(screen.getByText('Redirected from Urology')).toBeInTheDocument();
    const history = within(screen.getByRole('list', {name: 'History'}));
    expect(history.getByText('Redirected by u.clinician')).toBeInTheDocument();
    expect(history.getByText('Team Urology → Dermatology')).toBeInTheDocument();
    expect(history.getByText('Accepted by d.clinician')).toBeInTheDocument();
    expect(history.getByText('Skin check, High priority')).toBeInTheDocument();
  });

  it('asks the referrer for more information, then records that it came', async () => {
    withAnnouncements();
    const {user, fetch, unmount} = renderPage(
      triager,
      {[`POST /api/referrals/${REFERRAL_ID}/ask-for-information`]: () => json(testReferral({canTriage: true, status: 'InformationRequested'}))},
      false,
      clinician,
    );

    expect(await screen.findByText(/^Read the referral and its letter/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Information Received'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Ask for More Information'}));
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Ask for More Information'}));
    await expectAnnounced('More information asked for');
    expect(await (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!).json()).toEqual({expectedVersion: 2});
    await waitFor(() => expect(screen.getByRole('button', {name: 'Information Received'})).toBeInTheDocument());
    unmount();

    const second = renderPage(
      testReferral({canTriage: true, status: 'InformationRequested'}),
      {[`POST /api/referrals/${REFERRAL_ID}/information-received`]: () => json(testReferral({canTriage: true}))},
      false,
      clinician,
    );
    expect(await screen.findByText(/^More information has been asked of the referrer/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Ask for More Information'})).not.toBeInTheDocument();
    await second.user.click(screen.getByRole('button', {name: 'Information Received'}));
    const received = within(await waitFor(() => openDialog()!));
    await second.user.click(received.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await second.user.click(screen.getByRole('button', {name: 'Information Received'}));
    await second.user.click(within(await waitFor(() => openDialog()!)).getByRole('button', {name: 'Information Received'}));
    await expectAnnounced('Information received');
  });

  it('shows its waiting-time clock, its pauses and the clock in its history, pointing to its pathway to resume it', async () => {
    renderPage(
      testReferral({
        status: 'Accepted',
        isOpen: false,
        canEdit: false,
        canClose: false,
        pathwayId,
        canResumeClock: true,
        clock: {
          state: 'Paused',
          targetDays: 126,
          waitStartOn: '2026-11-02',
          targetOn: '2027-03-08',
          daysWaited: 10,
          pausedDays: 0,
          isBreached: false,
          pausedSinceOn: '2026-11-12',
          pauseReviewOn: '2026-12-01',
          isReviewOverdue: false,
        },
        pauses: [{id: 'p1', pausedOn: '2026-11-12', pausedByUsername: 'k.ashford', reason: 'PatientChoseToWait', detail: 'Synthetic: on holiday.', reviewOn: '2026-12-01'}],
        history: [
          {id: 'e1', action: 'TargetChanged', atUtc: '2026-11-04T09:00:00Z', username: 'd.clinician', previousTargetDays: 28, targetDays: 126, priority: 'Low'},
          {id: 'e2', action: 'ClockPaused', atUtc: '2026-11-12T09:00:00Z', username: 'k.ashford', pauseReason: 'PatientChoseToWait', reviewOn: '2026-12-01'},
        ],
      }),
    );

    expect(await screen.findByRole('heading', {name: 'Waiting time'})).toBeInTheDocument();
    expect(screen.getByText('Day 10 of 126')).toBeInTheDocument();
    expect(screen.getByText('Paused')).toBeInTheDocument();
    expect(screen.getByText('8 March 2027')).toBeInTheDocument();
    const pauses = within(screen.getByRole('list', {name: 'Pauses'}));
    expect(pauses.getByText('Patient chose to wait')).toBeInTheDocument();
    expect(pauses.getByText('Synthetic: on holiday.')).toBeInTheDocument();
    expect(pauses.getByText('Still paused')).toBeInTheDocument();
    expect(screen.getByText(/Resume the clock from/)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'its pathway'})).toHaveAttribute('href', `/pathways/${pathwayId}`);
    const history = within(screen.getByRole('list', {name: 'History'}));
    expect(history.getByText('Target changed')).toBeInTheDocument();
    expect(history.getByText('28 → 126 days (Low priority)')).toBeInTheDocument();
    expect(history.getByText('Clock paused by k.ashford')).toBeInTheDocument();
    expect(history.getByText('Patient chose to wait, review 1 Dec 2026')).toBeInTheDocument();
  });

  it('points to its pathway to pause a running clock, and says when a referral has no clock', async () => {
    const {unmount} = renderPage(
      testReferral({
        status: 'Accepted',
        isOpen: false,
        pathwayId,
        canPauseClock: true,
        clock: {state: 'Running', targetDays: 28, daysWaited: 3, targetOn: '2026-11-30', waitStartOn: '2026-11-02'},
        pauses: [],
      }),
    );
    expect(await screen.findByText(/Pause the clock from/)).toBeInTheDocument();
    expect(screen.getByText('No pauses.')).toBeInTheDocument();
    unmount();

    renderPage(testReferral({clock: undefined}));
    expect(await screen.findByText('This referral has no waiting-time clock.')).toBeInTheDocument();
    expect(screen.queryByText(/the clock from/)).not.toBeInTheDocument();
  });
});

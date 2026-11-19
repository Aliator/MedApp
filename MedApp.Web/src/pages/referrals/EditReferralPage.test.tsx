import {screen, waitFor} from '@testing-library/react';
import type {Referral} from '../../api/referrals';
import {expectAnnounced, findAlert, json, mockApi, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {DERMATOLOGY_ID, REFERRAL_ID, TEAMS, testReferral} from '../../test/referrals';
import {EditReferralPage} from './EditReferralPage';

const REFERRAL = `GET /api/referrals/${REFERRAL_ID}`;
const SAVE = `PUT /api/referrals/${REFERRAL_ID}`;
const CHANGED = 'Someone changed this referral meanwhile.';

const PRACTICES = [{id: 'p-test', name: 'Testbrook Surgery', odsCode: 'ZZZ123', isArchived: false}];

function renderPage(read: () => Response = () => json(testReferral()), save: (request: Request) => Response | Promise<Response> = () => json(testReferral())) {
  const fetch = mockApi({[REFERRAL]: read, 'GET /api/teams': () => json(TEAMS), 'GET /api/practices': () => json(PRACTICES), [SAVE]: save});
  const result = renderWithApp(<EditReferralPage id={REFERRAL_ID} />, {user: staffUser});
  return {...result, fetch};
}

const bodies = (fetch: ReturnType<typeof renderPage>['fetch']) =>
  Promise.all(
    fetch.mock.calls
      .map(([request]) => request as Request)
      .filter((request) => request.method === 'PUT')
      .map((request) => request.clone().json()),
  );

describe('EditReferralPage', () => {
  it("starts from the referral's details, with the patient shown, and saves from the version shown", async () => {
    withAnnouncements();
    const {user, fetch, router} = renderPage();

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    const practice = await screen.findByRole('textbox', {name: /Practice/});
    expect(practice).toHaveValue('Synthetic Street Surgery');
    await user.clear(practice);
    await user.type(practice, 'Another Surgery');
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/referrals/${REFERRAL_ID}`));
    expect(await bodies(fetch)).toEqual([
      {
        teamId: DERMATOLOGY_ID,
        receivedOn: '2026-11-02',
        practiceId: null,
        referrerPractice: 'Another Surgery',
        referrerClinician: 'Dr Test',
        referrerOdsCode: 'A12345',
        requestedPriority: 'High',
        reason: 'A synthetic reason for referral.',
        expectedVersion: 2,
      },
    ]);
    await expectAnnounced('Referral saved');
  });

  it('starts with the practice from the directory the referral has, and keeps sending it', async () => {
    const {user, fetch} = renderPage(() => json(testReferral({practiceId: 'p-test', referrerPractice: 'Testbrook Surgery', referrerOdsCode: 'ZZZ123'})));

    const picker = await screen.findByRole('combobox', {name: /Practice from the directory/});
    await waitFor(() => expect(picker).toHaveTextContent('Testbrook Surgery (ZZZ123)'));
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await waitFor(async () => expect((await bodies(fetch))[0]).toMatchObject({practiceId: 'p-test', referrerPractice: 'Testbrook Surgery'}));
  });

  it("keeps what was typed after a 409, saving from the old version until Reload starts again from what's there now", async () => {
    let reads = 0;
    const {user, fetch} = renderPage(
      () => json(++reads === 1 ? testReferral() : testReferral({version: 3, referrerPractice: 'Changed Surgery'})),
      () => json({title: CHANGED, status: 409}, 409),
    );

    const practice = await screen.findByRole('textbox', {name: /Practice/});
    await user.type(practice, ' 2');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await findAlert(CHANGED)).toBeInTheDocument();
    expect(practice).toHaveValue('Synthetic Street Surgery 2');
    await waitFor(() => expect(reads).toBe(2));

    await user.click(screen.getByRole('button', {name: 'Save'}));
    await waitFor(async () => expect((await bodies(fetch)).map((body) => body.expectedVersion)).toEqual([2, 2]));

    await user.click(await screen.findByRole('button', {name: 'Reload'}));
    expect(screen.getByRole('textbox', {name: /Practice/})).toHaveValue('Changed Surgery');
    expect(screen.queryByText(CHANGED)).not.toBeInTheDocument();
  });

  it("shows the API's errors on the fields", async () => {
    const {user} = renderPage(undefined, () => json({title: 'One or more validation errors occurred.', errors: {Reason: ['Reason is required.']}}, 400));
    await user.click(await screen.findByRole('button', {name: 'Save'}));
    expect((await screen.findAllByText('Reason is required.')).length).toBeGreaterThan(0);
  });

  it("says why a referral can't be edited", async () => {
    const closed: Referral = testReferral({canEdit: false, readOnlyReason: 'This referral is withdrawn, so it can\'t be changed.'});
    const {unmount} = renderPage(() => json(closed));
    expect(await findAlert("This referral is withdrawn, so it can't be changed.")).toBeInTheDocument();
    unmount();

    renderPage(() => json(testReferral({canEdit: false, readOnlyReason: null})));
    expect(await findAlert(/only a referral awaiting triage can be/)).toBeInTheDocument();
  });

  it('says when it is missing or could not be loaded, and cancels back to it', async () => {
    const {unmount} = renderPage(() => json({title: 'Not found'}, 404));
    expect(await findAlert('It may have been deleted.')).toBeInTheDocument();
    unmount();

    const failed = renderPage(() => json({title: 'Down'}, 500));
    expect(await findAlert('Referral not loaded')).toBeInTheDocument();
    failed.unmount();

    const {user, router} = renderPage();
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/referrals/${REFERRAL_ID}`));
  });
});

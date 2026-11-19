import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, expectAnnounced, findAlert, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT, DATA_REQUEST_PATIENT_ID, FILE_ID, NOTE_ID, readyDataRequest, testDataRequest, testReviewItems} from '../../test/dataRequests';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import type {DataRequest} from '../../api/dataRequests';
import type {CurrentUser} from '../../auth/auth';
import {askedByWords, DataRequestPage, outcomeWords} from './DataRequestPage';

const BASE = `/api/data-requests/${DATA_REQUEST_ID}`;

function render(request: DataRequest | (() => Response), handlers: Parameters<typeof mockApi>[0] = {}, user: CurrentUser = adminUser) {
  const fetch = mockApi({
    [`GET ${BASE}`]: typeof request === 'function' ? request : () => json(request),
    [`GET /api/patients/${DATA_REQUEST_PATIENT_ID}`]: () => json(DATA_REQUEST_PATIENT),
    ...handlers,
  });
  return {fetch, ...renderWithApp(<DataRequestPage id={DATA_REQUEST_ID} />, {user})};
}

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

describe('DataRequestPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('says who asked, and how it ended', () => {
    expect(askedByWords({requestedByPatient: true})).toBe('The patient');
    expect(askedByWords({requestedByPatient: false, requesterName: 'Synthetic Requester', requesterRelationship: 'Parent'})).toBe(
      'Synthetic Requester (Parent)',
    );
    expect(askedByWords({requestedByPatient: false})).toBe('Someone on their behalf');
    expect(outcomeWords(testDataRequest())).toBeNull();
    expect(outcomeWords(testDataRequest({status: 'Sent', sentOn: '2026-11-20'}))).toBe('Sent 20 November 2026');
    expect(outcomeWords(testDataRequest({status: 'Refused'}))).toBe('Refused');
    expect(outcomeWords(testDataRequest({status: 'Withdrawn'}))).toBe('Withdrawn');
    expect(outcomeWords(testDataRequest({status: 'Withdrawn', closedAtUtc: '2026-11-10T12:00:00Z', closedByUsername: 'test-admin'}))).toBe(
      'Withdrawn 10 Nov 2026, 12:00 by test-admin',
    );
    expect(outcomeWords(testDataRequest({status: 'Sent', sentOn: '2026-11-20', sendMethod: 'SecureEmail', closedByUsername: 'test-admin'}))).toBe(
      'Sent 20 November 2026, secure email by test-admin',
    );
  });

  it("shows an open request to a privacy manager: the patient, dates, what's missing, and only the actions it allows", async () => {
    render(testDataRequest({isOverdue: true}));

    expect(await screen.findByRole('heading', {name: 'Data Request', level: 1})).toBeInTheDocument();
    // The heading shows while the request loads; the subtitle only once it has.
    expect(await screen.findByText('Freya Ahmed · Open')).toBeInTheDocument();
    expect(await screen.findByRole('link', {name: /Ahmed, Freya/})).toBeInTheDocument();
    expectPatientFirstInDetails();
    expect(screen.getByText('The patient')).toBeInTheDocument();
    expect(screen.getAllByText('2 December 2026').length).toBeGreaterThan(0);
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    expect(screen.getByText(/Not checked yet/)).toBeInTheDocument();
    expect(screen.getByText(/Not reviewed yet/)).toBeInTheDocument();
    expect(screen.getByText('Pack: Record the identity check first.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Download Pack'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Review'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Record Identity Check'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Record as Sent'})).toBeInTheDocument();
  });

  it('records the identity check from its dialog, then announces it', async () => {
    withAnnouncements();
    const {user} = render(testDataRequest(), {[`POST ${BASE}/identity-check`]: () => json(readyDataRequest({reviewedAtUtc: null, withheld: []}))});

    await user.click(await screen.findByRole('button', {name: 'Record Identity Check'}));
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('radio', {name: 'Documents'}));
    await user.click(dialog.getByRole('button', {name: 'Record Identity Check'}));

    await expectAnnounced('Identity check recorded');
    expect(await screen.findByText('Passport seen')).toBeInTheDocument();
  });

  it('shows the review: who, when and what was withheld with why, and downloads the pack', async () => {
    const createObjectURL = vi.fn(() => 'blob:pack');
    vi.stubGlobal('URL', Object.assign(URL, {createObjectURL, revokeObjectURL: vi.fn()}));
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const {user} = render(readyDataRequest(), {
      [`GET ${BASE}/pack`]: () => new Response('PK', {headers: {'Content-Type': 'application/zip'}}),
    });

    expect(await screen.findByText('d.clinician')).toBeInTheDocument();
    expect(screen.getByText('One item withheld')).toBeInTheDocument();
    expect(screen.getByText('Likely to cause serious harm')).toBeInTheDocument();
    expect(screen.getByText('Documents')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Download Pack'}));

    await waitFor(() => expect(click).toHaveBeenCalledTimes(1));
    click.mockRestore();
  });

  it("shows why the pack couldn't be made in the API's words", async () => {
    const {user} = render(readyDataRequest(), {[`GET ${BASE}/pack`]: () => json({title: 'Review the record first.'}, 409)});

    await user.click(await screen.findByRole('button', {name: 'Download Pack'}));
    expect(await findAlert('Review the record first.')).toBeInTheDocument();
  });

  it('offers the review only as the response allows (an admin, while it is open), listing the items to withhold', async () => {
    const {user} = render(testDataRequest({canReview: true}), {
      [`GET ${BASE}/review-items`]: () => json(testReviewItems()),
    });

    await user.click(await screen.findByRole('button', {name: 'Review'}));
    expect(screen.getByRole('button', {name: 'Record Identity Check'})).toBeInTheDocument();
    const dialog = within(await dialogOpen());
    expect(await dialog.findByRole('checkbox', {name: 'Withhold this note'})).toBeChecked();
  });

  it('shows how a closed request ended, with no actions', async () => {
    render(
      testDataRequest({
        status: 'Refused',
        refusalReason: 'Manifestly excessive',
        closedAtUtc: '2026-11-10T12:00:00Z',
        closedByUsername: 'test-admin',
        canManage: false,
        packBlockedReason: null,
      }),
    );

    expect(await screen.findByText(/^Refused 10 Nov 2026, 12:00 by test-admin$/)).toBeInTheDocument();
    expect(screen.getByText('Manifestly excessive')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Record as Sent'})).not.toBeInTheDocument();
  });

  it('shows an extended request asked for on their behalf, checked by someone and reviewed with several items withheld', async () => {
    render(
      readyDataRequest({
        requestedByPatient: false,
        requesterName: 'Synthetic Requester',
        requesterRelationship: null,
        extensionMonths: 1,
        dueOn: '2027-01-02',
        extensionReason: 'Several requests at once',
        identityCheckedByUsername: null,
        identityCheckDetail: null,
        reviewedByUsername: null,
        withheld: [
          {kind: 'Note', itemId: NOTE_ID, reason: 'Likely to cause serious harm'},
          {kind: 'File', itemId: FILE_ID, reason: null},
        ],
      }),
    );

    expect(await screen.findByText('Synthetic Requester')).toBeInTheDocument();
    expect(screen.getByText('By one month')).toBeInTheDocument();
    expect(screen.getByText('Several requests at once')).toBeInTheDocument();
    expect(screen.getAllByText('—')).toHaveLength(2);
    expect(screen.getByText('2 items withheld')).toBeInTheDocument();
    expect(screen.getByText(/The pack is ready to make/)).toBeInTheDocument();
  });

  it('says how long an extension of two months is, and offers no more', async () => {
    const {user} = render(testDataRequest({extensionMonths: 2, extensionReason: null}));

    expect(await screen.findByText('By 2 months')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'More'}));
    expect(await screen.findByRole('menuitem', {name: /Refuse/})).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: /Extend Due Date/})).not.toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: /Change Identity Check/})).not.toBeInTheDocument();
  });

  it.each([
    ['Change Identity Check', 'Record Identity Check'],
    ['Extend Due Date', 'Extend Due Date'],
    ['Refuse', 'Refuse Request?'],
    ['Withdraw', 'Withdraw Request?'],
  ])('opens %s from More, and closes it on Cancel', async (item, title) => {
    const {user} = render(readyDataRequest({canMakePack: false, packBlockedReason: 'Wait.'}));

    await user.click(await screen.findByRole('button', {name: 'More'}));
    await user.click(await screen.findByRole('menuitem', {name: new RegExp(item)}));
    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('heading', {name: title})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('records it as sent from its dialog, and closes the review dialog on Cancel', async () => {
    withAnnouncements();
    const {user} = render(readyDataRequest({canReview: true}), {
      [`POST ${BASE}/sent`]: () => json(readyDataRequest({status: 'Sent', sentOn: '2026-11-20', canManage: false, canMakePack: false})),
      [`GET ${BASE}/review-items`]: () => json(testReviewItems()),
    });

    await user.click(await screen.findByRole('button', {name: 'Review'}));
    const review = within(await dialogOpen());
    await user.click(review.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'Record as Sent'}));
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('radio', {name: 'Secure email'}));
    await user.click(dialog.getByRole('button', {name: 'Record as Sent'}));
    await expectAnnounced('Data request recorded as sent');
  });

  it('folds and unfolds its sections', async () => {
    const {user} = render(readyDataRequest());

    await user.click(await screen.findByRole('button', {name: 'Hide identity check'}));
    await waitFor(() => expect(screen.queryByText('Passport seen')).not.toBeInTheDocument());
    await user.click(screen.getByRole('button', {name: 'Show identity check'}));
    expect(await screen.findByText('Passport seen')).toBeInTheDocument();
  });

  it('lets an error from the pack be dismissed', async () => {
    const {user} = render(readyDataRequest(), {[`GET ${BASE}/pack`]: () => json({title: 'No pack today.'}, 409)});

    await user.click(await screen.findByRole('button', {name: 'Download Pack'}));
    expect(await findAlert('No pack today.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: /Dismiss/}));
    await waitFor(() => expect(screen.queryByText('No pack today.')).not.toBeInTheDocument());
  });

  it("says when the request isn't there, or can't be loaded", async () => {
    const {unmount} = render(() => json({title: 'Not found'}, 404));
    expect(await screen.findByText('Data request not found.')).toBeInTheDocument();
    unmount();

    render(() => json({title: 'Boom'}, 500));
    expect(await screen.findByText('The data request could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

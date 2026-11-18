import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, expectAnnounced, findAlert, json, mockApi, openDialog, renderWithApp, seniorClinicianUser, withAnnouncements} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT, DATA_REQUEST_PATIENT_ID, readyDataRequest, testDataRequest, testReviewItems} from '../../test/dataRequests';
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
    expect(outcomeWords(testDataRequest())).toBeNull();
    expect(outcomeWords(testDataRequest({status: 'Sent', sentOn: '2026-11-20', sendMethod: 'SecureEmail', closedByUsername: 'test-admin'}))).toBe(
      'Sent 20 November 2026, secure email by test-admin',
    );
  });

  it("shows an open request to a privacy manager: the patient, dates, what's missing, and only the actions it allows", async () => {
    render(testDataRequest({isOverdue: true}));

    expect(await screen.findByRole('heading', {name: 'Data Request', level: 1})).toBeInTheDocument();
    // The heading shows while the request loads; the subtitle only once it has.
    expect(await screen.findByText('Freya Ahmed · Open')).toBeInTheDocument();
    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
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
    const {user} = render(readyDataRequest(), {[`GET ${BASE}/pack`]: () => json({title: 'A clinician must review the record first.'}, 409)});

    await user.click(await screen.findByRole('button', {name: 'Download Pack'}));
    expect(await findAlert('A clinician must review the record first.')).toBeInTheDocument();
  });

  it('offers a clinician only the review, which lists the items to withhold', async () => {
    const {user} = render(testDataRequest({canManage: false, canReview: true, packBlockedReason: null}), {
      [`GET ${BASE}/review-items`]: () => json(testReviewItems()),
    }, seniorClinicianUser);

    await user.click(await screen.findByRole('button', {name: 'Review'}));
    expect(screen.queryByRole('button', {name: 'Record Identity Check'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'More'})).not.toBeInTheDocument();
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

  it("says when the request isn't there, or can't be loaded", async () => {
    const {unmount} = render(() => json({title: 'Not found'}, 404));
    expect(await screen.findByText('Data request not found.')).toBeInTheDocument();
    unmount();

    render(() => json({title: 'Boom'}, 500));
    expect(await screen.findByText('The data request could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import type {Result} from '../../api/results';
import {HalfViewContext} from '../../components/pages/halfView';
import {alertLabelName, calls, expectAnnounced, json, mockApi, nurseUser, openDialog, renderWithApp, seniorClinicianUser, withAnnouncements} from '../../test/render';
import {RESULT_ID, RESULT_PATHWAY_ID, RESULT_REPORT_ID, RESULT_SPECIMEN_ID, SIGN_OFF_HOLDERS, testResult} from '../../test/results';
import {ResultPage} from './ResultPage';

const RESULT = `GET /api/results/${RESULT_ID}`;

function renderPage(result: Result | (() => Response) = testResult(), handlers: Record<string, (request: Request) => Response> = {}, user = seniorClinicianUser, isNarrow = false) {
  const fetch = mockApi({
    [RESULT]: typeof result === 'function' ? result : () => json(result),
    'GET /api/calendar/staff': () => json(SIGN_OFF_HOLDERS),
    ...handlers,
  });
  const page = <ResultPage id={RESULT_ID} />;
  const rendered = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {user});
  return {...rendered, fetch};
}

describe('ResultPage', () => {
  it('shows the patient, where it came from, who reported it, who signs it off, and the report to download', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Skin biopsy (punch) report', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(screen.getAllByText('Awaiting sign-off').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', {name: 'Specimen MA26-000123'})).toHaveAttribute('href', `/specimens/${RESULT_SPECIMEN_ID}`);
    expect(screen.getByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', `/pathways/${RESULT_PATHWAY_ID}`);
    expect(screen.getByText('4 Nov 2026, 10:00 by test-lab')).toBeInTheDocument();
    expect(screen.getByText('3 days')).toBeInTheDocument();
    expect(screen.getByText('test-senior')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Download histology-report.pdf'})).toHaveAttribute('href', `/api/attachments/${RESULT_REPORT_ID}/content`);
    expect(document.querySelector('img')).toBeNull();
    expect(screen.getAllByText('Not yet')).toHaveLength(2);
  });

  it("offers the responsible clinician Mark as Seen, Reassign, Sign Off and Entered in Error, but not Take Over", async () => {
    renderPage();

    expect(await screen.findByRole('button', {name: 'Mark as Seen'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Reassign'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Sign Off'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'More'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Take Over'})).not.toBeInTheDocument();
  });

  it("offers Take Over on someone else's result, with Needs a clinician", async () => {
    renderPage(testResult({responsibleUserId: SIGN_OFF_HOLDERS[1].userId, responsibleUsername: 'dr.synthetic', needsClinician: true}));

    expect(await screen.findByRole('button', {name: 'Take Over'})).toBeInTheDocument();
    expect(screen.getByText('Needs a clinician')).toBeInTheDocument();
  });

  it('shows only what its flags allow', async () => {
    renderPage(testResult({canMarkSeen: false, canSignOff: false, canReassign: false, canMarkEnteredInError: false}), {}, nurseUser);

    expect(await screen.findByRole('heading', {name: 'Skin biopsy (punch) report', level: 1})).toBeInTheDocument();
    for (const name of ['Mark as Seen', 'Sign Off', 'Reassign', 'Take Over', 'More'])
      expect(screen.queryByRole('button', {name})).not.toBeInTheDocument();
  });

  it('marks it seen from the version shown, and announces it', async () => {
    withAnnouncements();
    const seen = testResult({status: 'Seen', seenAtUtc: '2026-11-05T09:00:00Z', seenByUsername: 'test-senior', canMarkSeen: false, version: 3});
    const {user, fetch} = renderPage(testResult(), {[`POST /api/results/${RESULT_ID}/seen`]: () => json(seen)});

    await user.click(await screen.findByRole('button', {name: 'Mark as Seen'}));

    await expectAnnounced('Result marked seen');
    const sent = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await sent.json()).toEqual({expectedVersion: 2});
    expect(await screen.findByText('5 Nov 2026, 09:00 by test-senior')).toBeInTheDocument();
  });

  it("shows the API's words when marking it seen is refused", async () => {
    const {user} = renderPage(testResult(), {[`POST /api/results/${RESULT_ID}/seen`]: () => json({title: 'Someone else changed this result.'}, 409)});

    await user.click(await screen.findByRole('button', {name: 'Mark as Seen'}));

    expect(await screen.findByText('Someone else changed this result.')).toBeInTheDocument();
  });

  it('shows what was done once signed off, and that the file was deleted once entered in error', async () => {
    renderPage(
      testResult({
        status: 'SignedOff',
        isAwaitingSignOff: false,
        signedOffAtUtc: '2026-11-06T09:30:00Z',
        signedOffByUsername: 'test-senior',
        action: 'Other',
        actionDetail: 'Synthetic: discussed with the GP.',
        canMarkSeen: false,
        canSignOff: false,
        canReassign: false,
        readOnlyReason: 'This result was signed off, so it can no longer change.',
      }),
    );

    expect(await screen.findByText('6 Nov 2026, 09:30 by test-senior')).toBeInTheDocument();
    expect(screen.getByText('Other')).toBeInTheDocument();
    expect(screen.getByText('Synthetic: discussed with the GP.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: alertLabelName('Read-only result')})).toBeInTheDocument();
  });

  it('says the report was deleted once entered in error, with what was wrong', async () => {
    renderPage(
      testResult({
        status: 'EnteredInError',
        isAwaitingSignOff: false,
        reportFileName: null,
        enteredInErrorAtUtc: '2026-11-05T11:00:00Z',
        enteredInErrorByUsername: 'test-senior',
        errorDetail: 'Synthetic: wrong patient.',
        canMarkSeen: false,
        canSignOff: false,
        canReassign: false,
        canMarkEnteredInError: false,
      }),
      {},
      seniorClinicianUser,
      true,
    );

    expect(await screen.findByText("The report's file was deleted when the result was marked entered in error.")).toBeInTheDocument();
    expect(screen.getByText('5 Nov 2026, 11:00 by test-senior')).toBeInTheDocument();
    expect(screen.getByText('Synthetic: wrong patient.')).toBeInTheDocument();
  });

  it('shows a thumbnail for a picture, and an external report has no specimen', async () => {
    renderPage(testResult({reportHasThumbnail: true, specimenId: null, specimenNumber: null, specimenTypeName: null, source: 'ExternalReport', pathwayId: null, pathwayTitle: null}));

    expect(await screen.findByText('Not on a pathway')).toBeInTheDocument();
    expect(screen.getAllByText('External report').length).toBeGreaterThan(0);
    expect(document.querySelector('img')).toHaveAttribute('src', `/api/attachments/${RESULT_REPORT_ID}/thumbnail`);
  });

  it('opens each dialog its flags allow, and closes it again', {timeout: 30_000}, async () => {
    const {user, fetch} = renderPage(testResult({responsibleUserId: SIGN_OFF_HOLDERS[1].userId, responsibleUsername: 'dr.synthetic'}));

    const opens = async (open: () => Promise<void>, heading: string) => {
      await open();
      const dialog = within(await waitFor(() => openDialog()!));
      expect(dialog.getByRole('heading', {name: heading})).toBeInTheDocument();
      expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
      await user.click(dialog.getByRole('button', {name: 'Cancel'}));
      await waitFor(() => expect(openDialog()).toBeNull());
    };
    const button = (name: string) => async () => user.click(await screen.findByRole('button', {name}));

    await opens(button('Sign Off'), 'Sign Off Result');
    await opens(button('Take Over'), 'Take Over Result?');
    await opens(button('Reassign'), 'Reassign Result');
    await opens(async () => {
      await user.click(screen.getByRole('button', {name: 'More'}));
      await user.click(await screen.findByRole('menuitem', {name: /^Entered in Error/}));
    }, 'Mark Entered in Error?');
    expect(calls(fetch)).toContain('GET /api/calendar/staff?permission=SignOffResults');
  });

  it("shows the patient's flags", async () => {
    renderPage(testResult({patientFlags: {isRestricted: true, isDisputed: false, isAnonymised: false}}));
    expect(await screen.findByRole('button', {name: alertLabelName('Restricted record')})).toBeInTheDocument();
  });

  it('says when it is not found', async () => {
    renderPage(() => json({title: 'Not found'}, 404));
    expect(await screen.findByText('Result not found. It may have been deleted.')).toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderPage(() => json({}, 500));
    expect(await screen.findByText('The result could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

describe('ResultPage, folding', () => {
  it('hides and shows its sections', async () => {
    const {user} = renderPage();

    await user.click(await screen.findByRole('button', {name: 'Hide details'}));
    expect(await screen.findByRole('button', {name: 'Show details'})).toHaveAttribute('aria-expanded', 'false');
    await user.click(screen.getByRole('button', {name: 'Show details'}));
    expect(await screen.findByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
  });
});

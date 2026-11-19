import {screen, waitFor, within} from '@testing-library/react';
import type {Specimen} from '../../api/specimens';
import {HalfViewContext} from '../../components/pages/halfView';
import {
  alertLabelName,
  expectAnnounced,
  json,
  mockApi,
  nurseUser,
  openDialog,
  renderWithApp,
  staffUser,
  userWithRoles,
  withAnnouncements,
} from '../../test/render';
import {REPORT_ID, SPECIMEN_ID, SPECIMEN_PATHWAY_ID, testSpecimen, testSummary} from '../../test/specimens';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import {SpecimenPage, specimenSteps} from './SpecimenPage';

const SPECIMEN = `GET /api/specimens/${SPECIMEN_ID}`;
const labUser = userWithRoles('test-lab', 'Lab');

const received = testSpecimen({
  status: 'Received',
  sentAtUtc: '2026-11-02T15:00:00Z',
  sentByUsername: 'test-nurse',
  courierReference: 'CR-42',
  receivedAtUtc: '2026-11-03T10:00:00Z',
  receivedByUsername: 'test-lab',
  labReference: 'NP-77',
  canEdit: false,
  canMarkSent: false,
  canMarkReceived: false,
  canReject: true,
  canReport: true,
  canMarkEnteredInError: false,
});

const reported = testSpecimen({
  ...received,
  status: 'Reported',
  isOpen: false,
  reportedAtUtc: '2026-11-05T12:00:00Z',
  reportedByUsername: 'test-lab',
  reportAttachmentId: REPORT_ID,
  canReject: false,
  canReport: false,
  canMarkLost: false,
});

function renderPage(specimen: Specimen | (() => Response) = testSpecimen(), handlers: Record<string, (request: Request) => Response> = {}, user = nurseUser, isNarrow = false) {
  const fetch = mockApi({
    [SPECIMEN]: typeof specimen === 'function' ? specimen : () => json(specimen),
    'GET /api/attachments': () => json({items: [], totalCount: 0}),
    ...handlers,
  });
  const page = <SpecimenPage id={SPECIMEN_ID} />;
  const result = renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{page}</HalfViewContext.Provider> : page, {user});
  return {...result, fetch};
}

describe('SpecimenPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-04T09:00:00Z')});
  });

  it('shows the patient, the details, the steps so far and its stage, with the actions a taken one offers', async () => {
    renderPage(testSpecimen({otherSpecimensFromStage: [testSummary({id: 'other', number: 'MA26-000100', status: 'Lost'})]}));

    expect(await screen.findByRole('heading', {name: 'Sample MA26-000123', level: 1})).toBeInTheDocument();
    expect(screen.getAllByText('Skin biopsy (punch)')[0]).toBeInTheDocument();
    expectPatientFirstInDetails();
    expect(screen.getAllByText('Taken').length).toBeGreaterThan(0);
    expect(screen.getByText('7 Nov 2026, 09:00 (5 days)')).toBeInTheDocument();
    expect(screen.getAllByText('dr.synthetic').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Left forearm').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Synthetic notes for the lab.').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', `/pathways/${SPECIMEN_PATHWAY_ID}`);

    const steps = within(screen.getByRole('list', {name: 'Steps'}));
    expect(steps.getByText('2 Nov 2026, 09:00 by test-nurse')).toBeInTheDocument();
    expect(steps.getAllByText('Not yet')).toHaveLength(3);
    expect(screen.queryByText('Turnaround')).not.toBeInTheDocument();
    expect(screen.getByText('No report yet.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'MA26-000100'})).toHaveAttribute('href', '/specimens/other');

    expect(screen.getByRole('link', {name: 'Print Label'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}/print?sheet=label`);
    expect(screen.getByRole('link', {name: 'Print Request Sheet'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}/print?sheet=request`);
    expect(screen.getByRole('button', {name: 'Edit'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Mark Sent'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Mark Received'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Attach Report'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'More'})).toBeInTheDocument();
  });

  it('marks it sent from the version shown, then shows what came back', async () => {
    withAnnouncements();
    const sent = testSpecimen({status: 'Sent', sentAtUtc: '2026-11-04T09:00:00Z', sentByUsername: 'test-nurse', canEdit: false, canMarkSent: false, version: 4});
    const {user, fetch} = renderPage(testSpecimen(), {[`POST /api/specimens/${SPECIMEN_ID}/sent`]: () => json(sent)});

    await user.click(await screen.findByRole('button', {name: 'Mark Sent'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('button', {name: 'Mark Sent'}));

    await expectAnnounced('Sample marked sent');
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({atUtc: null, reference: null, expectedVersion: 3});
    expect(await screen.findByText('4 Nov 2026, 09:00 by test-nurse')).toBeInTheDocument();
    expect(screen.getByText('Taken to sent')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Mark Sent'})).not.toBeInTheDocument();
  });

  it("offers the lab's steps to its staff only: Attach Report, and Reject under More", async () => {
    const {user} = renderPage(received, {}, labUser);

    expect(await screen.findByRole('button', {name: 'Attach Report'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
    expect(screen.getByText('Courier or batch reference: CR-42')).toBeInTheDocument();
    expect(screen.getByText("Lab's reference: NP-77")).toBeInTheDocument();
    expect(screen.getByText('Taken to sent')).toBeInTheDocument();
    expect(screen.getByText('6 hours')).toBeInTheDocument();
    expect(screen.getByText('19 hours')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'More'}));
    expect(await screen.findByRole('menuitem', {name: /Reject/})).toBeInTheDocument();
    expect(screen.getByRole('menuitem', {name: /Mark Lost/})).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', {name: /Entered in Error/})).not.toBeInTheDocument();
  });

  it('links its lab case (MC15)', async () => {
    renderPage({...received, labCaseId: 'case-1', labCaseNumber: 'MA26-000300'}, {}, labUser);
    expect(await screen.findByRole('link', {name: 'MA26-000300'})).toHaveAttribute('href', '/lab/cases/case-1');
    expect(screen.queryByRole('button', {name: 'Open Lab Case'})).not.toBeInTheDocument();
  });

  it('offers the lab Open Lab Case for a received sample with no case', async () => {
    renderPage({...received, labCaseId: null, canOpenLabCase: true}, {}, labUser);
    expect(await screen.findByRole('button', {name: 'Open Lab Case'})).toBeInTheDocument();
    expect(screen.getByText('None yet')).toBeInTheDocument();
  });

  it('offers nothing that changes it to someone who neither records nor processes samples', async () => {
    renderPage(received, {}, staffUser);
    expect(await screen.findByRole('link', {name: 'Print Label'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Attach Report'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'More'})).not.toBeInTheDocument();
  });

  it('shows the report as the patient\'s files list it, with the whole turnaround', async () => {
    const report = {
      id: REPORT_ID,
      fileName: 'histology.pdf',
      contentType: 'application/pdf',
      typeName: 'PDF',
      sizeBytes: 2048,
      attachedTo: 'Pathway',
      pathwayId: SPECIMEN_PATHWAY_ID,
      pathwayTitle: 'Skin lesion review',
      uploadedByUsername: 'test-lab',
      uploadedAtUtc: '2026-11-05T12:00:00Z',
      scanStatus: 'NotScanned',
      hasThumbnail: false,
    };
    renderPage(reported, {'GET /api/attachments': () => json({items: [report], totalCount: 1})}, labUser);

    expect(await screen.findByText('histology.pdf')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Download histology.pdf'})).toHaveAttribute('href', `/api/attachments/${REPORT_ID}/content`);
    expect(screen.getByText('Received to reported')).toBeInTheDocument();
    expect(screen.getByText('3 days 3 hours')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'More'})).not.toBeInTheDocument();
  });

  it("offers the report as a plain download when the files list doesn't have it", async () => {
    renderPage(reported, {}, labUser, true);
    expect(await screen.findByRole('link', {name: 'Download Report'})).toHaveAttribute('href', `/api/attachments/${REPORT_ID}/content`);
  });

  it('shows how a closed one was closed, and why', async () => {
    renderPage(
      testSpecimen({
        ...received,
        status: 'RejectedByLab',
        isOpen: false,
        rejectionReason: 'InsufficientSample',
        closedDetail: 'Too little tissue.',
        closedAtUtc: '2026-11-03T11:00:00Z',
        closedByUsername: 'test-lab',
        canReject: false,
        canReport: false,
        canMarkLost: false,
        readOnlyReason: 'This sample was rejected by the lab, so it can no longer change.',
      }),
    );

    const steps = within(await screen.findByRole('list', {name: 'Steps'}));
    expect(steps.getByText('Rejected by the lab')).toBeInTheDocument();
    expect(steps.getByText('3 Nov 2026, 11:00 by test-lab')).toBeInTheDocument();
    expect(steps.getByText('Reason: Insufficient sample')).toBeInTheDocument();
    expect(steps.getByText('Too little tissue.')).toBeInTheDocument();
    expect(steps.queryByText('Reported')).not.toBeInTheDocument();
    expect(screen.getByText('No report: the sample was closed without one.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: alertLabelName('Read-only sample')})).toBeInTheDocument();
  });

  // Seven dialogs in turn: slow under coverage on a loaded machine (CI), so it gets room.
  it('opens each dialog its flags allow, and closes it again, and folds its sections', {timeout: 30_000}, async () => {
    const everything = testSpecimen({canMarkReceived: true, canReject: true, canReport: true, canMarkLost: true, canMarkEnteredInError: true});
    const {user} = renderPage(
      everything,
      {
        'GET /api/specimen-types': () => json([]),
        'GET /api/calendar/staff': () => json([]),
      },
      labUser,
    );

    const opens = async (open: () => Promise<void>, heading: string) => {
      await open();
      const dialog = within(await waitFor(() => openDialog()!));
      expect(dialog.getByRole('heading', {name: heading})).toBeInTheDocument();
      await user.click(dialog.getByRole('button', {name: 'Cancel'}));
      await waitFor(() => expect(openDialog()).toBeNull());
    };
    const button = (name: string) => async () => user.click(await screen.findByRole('button', {name}));
    const menu = (name: RegExp) => async () => {
      await user.click(screen.getByRole('button', {name: 'More'}));
      await user.click(await screen.findByRole('menuitem', {name}));
    };

    await opens(button('Edit'), 'Edit Sample MA26-000123');
    await opens(button('Mark Sent'), 'Mark Sent');
    await opens(button('Mark Received'), 'Mark Received');
    await opens(button('Attach Report'), 'Attach Report');
    await opens(menu(/^Reject/), 'Reject Sample');
    await opens(menu(/^Mark Lost/), 'Mark Lost');
    await opens(menu(/^Mark Entered in Error/), 'Mark Entered in Error?');

    await user.click(screen.getByRole('button', {name: 'Hide steps'}));
    expect(screen.queryByRole('list', {name: 'Steps'})).not.toBeInTheDocument();
  });

  it("shows the patient's flags", async () => {
    renderPage(testSpecimen({patientFlags: {isRestricted: true, isDisputed: false, isAnonymised: false}}));
    expect(await screen.findByRole('button', {name: alertLabelName('Restricted record')})).toBeInTheDocument();
  });

  it('says when it is not found, or could not be loaded', async () => {
    renderPage(() => json({title: 'Not found'}, 404));
    expect(await screen.findByText('It may have been deleted.')).toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderPage(() => json({}, 500));
    expect(await screen.findByText('Sample not loaded')).toBeInTheDocument();
  });

  it('lists the steps in order, a closed one ending with how it closed', () => {
    expect(specimenSteps(testSpecimen()).map((s) => s.label)).toEqual(['Taken', 'Sent', 'Received', 'Reported']);
    expect(specimenSteps(testSpecimen({status: 'Lost', closedAtUtc: '2026-11-03T11:00:00Z', closedDetail: ' '})).map((s) => [s.label, s.details])).toEqual([
      ['Taken', []],
      ['Lost', []],
    ]);
  });
});

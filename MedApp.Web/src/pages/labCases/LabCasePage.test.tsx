import {screen, within} from '@testing-library/react';
import {json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {CASE_ID, testCase, testClock, testHospitalCase} from '../../test/labCases';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import {LabCasePage} from './LabCasePage';

const labUser = userWithRoles('test-lab', 'Lab');
const clinician = userWithRoles('test-clinician', 'Clinician');

function renderPage(answer: () => Response, user = labUser) {
  mockApi({[`GET /api/lab-cases/${CASE_ID}`]: answer});
  return renderWithApp(<LabCasePage id={CASE_ID} />, {user});
}

describe('LabCasePage', () => {
  it('shows the case, its steps with the outcomes recorded, and each outcome of the next choice as a button of its own', async () => {
    const {user} = renderPage(() => json(testCase()));

    expect(await screen.findByRole('heading', {name: 'Lab Case MA26-000300', level: 1})).toBeInTheDocument();
    expectPatientFirstInDetails();
    const steps = within(screen.getByRole('list', {name: 'Steps'}));
    expect(steps.getByText('Sample adequate? Adequate')).toBeInTheDocument();
    expect(steps.getByRole('button', {name: 'Entered in Error'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Skin lesion review'})).toHaveAttribute('href', '/pathways/00000051-0000-0000-0000-000000000003');
    expect(screen.getByRole('link', {name: 'MA26-000123'})).toBeInTheDocument();

    // None is chosen or suggested: each outcome is its own button, in a fixed order.
    const outcomes = ['No more work', 'Extra tests (special stains)', 'A second opinion'];
    for (const name of outcomes) expect(screen.getByRole('button', {name})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'A second opinion'}));
    expect(within(openDialog()!).getByRole('heading', {name: 'Needs more work? A second opinion'})).toBeInTheDocument();
  });

  it('offers the next step to record, and the results to a pathologist once every step is done', async () => {
    renderPage(() => json(testCase({next: {kind: 'Step', name: 'Cut-up', testStepId: 'step-1', stepKind: 'Standard'}})));
    expect(await screen.findByRole('button', {name: 'Record Cut-up'})).toBeInTheDocument();
  });

  it('says a lab stage’s case waits for its sample, with nothing to record yet (MC16)', async () => {
    renderPage(() => json(testCase({next: {kind: 'Receive', name: 'Receive the sample'}, canRecord: false, labTestName: null})));
    expect(await screen.findByText('Waiting for the sample. Receiving it at the lab starts the case, and the lab chooses the test.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /Record/})).not.toBeInTheDocument();
  });

  it('shows the report once authorised, and reads only for anyone outside the lab', async () => {
    renderPage(
      () =>
        json(
          testCase({
            status: 'Reported',
            next: undefined,
            canRecord: false,
            reportText: 'Synthetic results.',
            reportedAtUtc: '2026-11-04T10:00:00Z',
            reportedByUsername: 'test-lab',
            resultId: 'result-1',
            readOnlyReason: "This case has been reported, so it can't be changed.",
          }),
        ),
      clinician,
    );

    expect(await screen.findByText('Synthetic results.')).toBeInTheDocument();
    expect(screen.getByText('Nothing: the report has been authorised.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Dermatology: its result'})).toHaveAttribute('href', '/results/result-1');
    expect(screen.queryByRole('button', {name: /Record/})).not.toBeInTheDocument();
  });

  it('shows its clock: both deadlines, which one applies, and the countdown (MC19)', async () => {
    renderPage(() => json(testCase({clock: testClock()})));
    expect(await screen.findByText("The hospital's date (the sooner applies)")).toBeInTheDocument();
    expect(screen.getByText('7 November 2026')).toBeInTheDocument();
    expect(screen.getByText('Due tomorrow')).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(screen.getByText('10 November 2026 (limit 13 Nov 2026)')).toBeInTheDocument();
    expect(screen.getByText('7 November 2026, as the request gave it')).toBeInTheDocument();
  });

  it('shows a hospital case’s report as sent, and offers the lab its surgery details (MC20)', async () => {
    const {user} = renderPage(() => json(testHospitalCase()));
    expect(await screen.findByText('Ms A. Example (synthetic), Riverside General (test)')).toBeInTheDocument();
    expect(screen.getByText('Awaiting the hospital’s surgery details.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Add Surgery Details'}));
    expect(within(openDialog()!).getByRole('heading', {name: 'Add Surgery Details'})).toBeInTheDocument();
  });

  it('shows the surgery details as written, and when the case closed (MC20)', async () => {
    renderPage(() =>
      json(
        testHospitalCase({
          status: 'Closed',
          canAddSurgeryDetails: false,
          closedAtUtc: '2026-11-06T10:00:00Z',
          closedByUsername: 'test-lab',
          surgeryDetails: [
            {id: 'sd-1', surgeryOn: '2026-11-05', procedure: 'Excision (synthetic)', outcome: 'Completed (synthetic).', noSurgeryExpected: false, detail: null, addedAtUtc: '2026-11-06T10:00:00Z', addedByUsername: 'test-lab'},
          ],
        }),
      ),
    );
    const list = within(await screen.findByRole('list', {name: 'Surgery details'}));
    expect(list.getByText('Excision (synthetic), 5 November 2026')).toBeInTheDocument();
    expect(list.getByText('Completed (synthetic).')).toBeInTheDocument();
    expect(screen.getByText(/^Case closed/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add Surgery Details'})).not.toBeInTheDocument();
  });

  it('says when it isn’t there, or is another team’s', async () => {
    renderPage(() => json({title: 'Not found', status: 404}, 404));
    expect(await screen.findByText('Lab case not found')).toBeInTheDocument();
    expect(screen.getByText("Lab case not found. It may have been deleted, or it's another team's.")).toBeInTheDocument();
  });
});

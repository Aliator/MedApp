import {screen, within} from '@testing-library/react';
import {json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {CASE_ID, testCase} from '../../test/labCases';
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
    expect(screen.getByRole('region', {name: /Ahmed, Freya/})).toBeInTheDocument();
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

  it('says when it isn’t there, or is another team’s', async () => {
    renderPage(() => json({title: 'Not found', status: 404}, 404));
    expect(await screen.findByText(/Lab case not found/)).toBeInTheDocument();
  });
});

import {screen, within} from '@testing-library/react';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles, withAnnouncements} from '../../test/render';
import {CASE_ID, testCase} from '../../test/labCases';
import {LabCaseDialog, type LabCaseDialogKind} from './LabCaseDialog';

const pathologist = userWithRoles('test-lab', 'Pathologist');

function renderDialog(kind: LabCaseDialogKind, handlers: Record<string, (request: Request) => Response>, labCase = testCase()) {
  const onClose = vi.fn();
  const fetch = mockApi(handlers);
  const result = renderWithApp(<LabCaseDialog dialog={kind} labCase={labCase} onClose={onClose} />, {user: pathologist});
  return {...result, fetch, onClose};
}

const dialog = () => within(openDialog()!);
const bodyOf = (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls[0][0] as Request).clone().json();

describe('LabCaseDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T14:05:00Z')});
  });

  it('records the next step now, with the patient and the case shown, from the version shown', async () => {
    withAnnouncements();
    const labCase = testCase({next: {kind: 'Step', name: 'Cut-up', testStepId: 'step-1', stepKind: 'Standard'}});
    const {user, fetch, onClose} = renderDialog({kind: 'step'}, {[`POST /api/lab-cases/${CASE_ID}/steps`]: () => json(labCase)}, labCase);

    expect(await screen.findByRole('heading', {name: 'Record Cut-up'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('MA26-000300')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Record Step'}));

    await expectAnnounced('Cut-up recorded');
    expect(await bodyOf(fetch)).toEqual({testStepId: 'step-1', fixedStep: null, atUtc: null, detail: null, expectedVersion: 3});
    expect(onClose).toHaveBeenCalled();
  });

  it('asks where a second opinion went before recording it', async () => {
    const labCase = testCase({next: {kind: 'Step', name: 'Sent for a second opinion', testStepId: 'step-2', stepKind: 'SentForSecondOpinion'}});
    const {user, fetch} = renderDialog({kind: 'step'}, {[`POST /api/lab-cases/${CASE_ID}/steps`]: () => json(labCase)}, labCase);

    await screen.findByRole('heading', {name: 'Record Sent for a second opinion'});
    expect(isDisabled(dialog().getByRole('button', {name: 'Record Step'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Where it went/}), 'Regional service');
    await user.click(dialog().getByRole('button', {name: 'Record Step'}));

    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await bodyOf(fetch)).toMatchObject({testStepId: 'step-2', detail: 'Regional service'});
  });

  it('records the outcome chosen on the page, asking why a sample is insufficient, and shows a 409 in its words', async () => {
    const labCase = testCase({
      next: {kind: 'Choice', name: 'Sample adequate?', choice: {choice: 'SampleAdequate', question: 'Sample adequate?', outcomes: ['Adequate', 'Insufficient']}},
    });
    const {user, fetch} = renderDialog(
      {kind: 'outcome', outcome: 'Insufficient'},
      {[`POST /api/lab-cases/${CASE_ID}/outcomes`]: () => json({title: 'Someone else changed this lab case.'}, 409)},
      labCase,
    );

    expect(await screen.findByRole('heading', {name: 'Sample adequate? Insufficient – repeat needed'})).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Record Outcome'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Why it.s insufficient/}), 'Too little to test');
    await user.click(dialog().getByRole('button', {name: 'Record Outcome'}));

    expect(await dialog().findByText('Someone else changed this lab case.')).toBeInTheDocument();
    expect(await bodyOf(fetch)).toEqual({choice: 'SampleAdequate', outcome: 'Insufficient', detail: 'Too little to test', expectedVersion: 3});
  });

  it('enters the results and authorises the report, then opens the pathway for the lab to finish its stage', async () => {
    const labCase = testCase({next: {kind: 'Report', name: 'Results and report'}, canAuthorise: true});
    const {user, fetch, router} = renderDialog(
      {kind: 'report'},
      {[`POST /api/lab-cases/${CASE_ID}/report`]: () => json(testCase({status: 'Reported', reportText: 'Synthetic results.'}))},
      labCase,
    );

    expect(await screen.findByRole('heading', {name: 'Enter Results and Authorise'})).toBeInTheDocument();
    expect(dialog().getByText(/finish the lab stage to hand it back/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Authorise and Open Pathway'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /^Results/}), 'Synthetic results.');
    await user.click(dialog().getByRole('button', {name: 'Authorise and Open Pathway'}));

    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await bodyOf(fetch)).toEqual({results: 'Synthetic results.', expectedVersion: 3});
    await vi.waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${labCase.pathwayId}`));
  });

  it('authorises a case with no pathway (the hospital’s) and stays', async () => {
    const labCase = testCase({next: {kind: 'Report', name: 'Results and report'}, canAuthorise: true, pathwayId: null, stageId: null});
    const {user, router, onClose} = renderDialog(
      {kind: 'report'},
      {[`POST /api/lab-cases/${CASE_ID}/report`]: () => json(testCase({status: 'Reported', pathwayId: null}))},
      labCase,
    );
    expect(await screen.findByRole('heading', {name: 'Enter Results and Authorise'})).toBeInTheDocument();
    const before = router.state.location.pathname;

    await user.type(dialog().getByRole('textbox', {name: /^Results/}), 'Synthetic results.');
    await user.click(dialog().getByRole('button', {name: 'Authorise Report'}));

    await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(router.state.location.pathname).toBe(before);
  });
});

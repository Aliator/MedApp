import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {ApiError, ApiValidationError} from '../../api/errors';
import type {PathwayStage} from '../../api/pathways';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {OutcomeChoice, ReinstateStageDialog, SkipStageDialog} from './StageSkipDialogs';

/** The open dialog, once it has opened. */
async function shownDialog() {
  await waitFor(() => expect(openDialog()).not.toBeNull());
  return within(openDialog()!);
}

const patient = {firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: '9990000026'};

const stage = (overrides: Partial<PathwayStage> = {}): PathwayStage => ({
  id: 's2',
  stageOrder: 2,
  name: 'Physio',
  state: 'NotStarted',
  isCurrent: true,
  isOptional: true,
  canSkip: true,
  ...overrides,
});

const biopsy = stage({
  id: 's1',
  stageOrder: 1,
  name: 'Biopsy',
  isOptional: false,
  choiceKind: 'LabResult',
  needsOutcome: true,
  outcomes: [
    {name: 'Benign', stageNames: ['Final letter']},
    {name: 'Malignant', stageNames: ['Referral to hospital', 'Letter to GP']},
  ],
});

describe('SkipStageDialog', () => {
  it('shows the patient and the stage, asks why with nothing chosen, and needs a detail for Other', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(<SkipStageDialog stage={stage()} patient={patient} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(dialog.getByText('Stage to skip: 2. Physio')).toBeInTheDocument();
    expect(dialog.getAllByRole('radio').map((r) => [r.getAttribute('value'), (r as HTMLInputElement).checked])).toEqual([
      ['NotNeeded', false],
      ['PatientDeclined', false],
      ['Other', false],
    ]);
    const skip = dialog.getByRole('button', {name: 'Skip Stage'});
    expect(isDisabled(skip)).toBe(true);

    await user.click(dialog.getByRole('radio', {name: 'Other'}));
    expect(isDisabled(skip)).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Detail/}), ' Seen at another hospital ');
    await user.click(skip);

    expect(onConfirm).toHaveBeenCalledWith({reason: 'Other', detail: 'Seen at another hospital'}, expect.any(Function));
  });

  it("shows the API's 400 on its field, and anything else in its words", async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(<SkipStageDialog stage={stage()} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('radio', {name: 'Patient declined'}));
    await user.click(dialog.getByRole('button', {name: 'Skip Stage'}));
    const fail = onConfirm.mock.calls[0][1] as (error: unknown) => void;

    fail(new ApiValidationError({title: 'Invalid', fields: {reason: ['Choose why the stage is skipped.']}, general: []}));
    expect(await dialog.findByText('Choose why the stage is skipped.')).toBeInTheDocument();
    fail(new ApiError(409, undefined, 'Only the current stage can be skipped.'));
    expect(await dialog.findByText('Only the current stage can be skipped.')).toBeInTheDocument();
  });

  it('cancels', async () => {
    const onCancel = vi.fn();
    const {user} = renderWithApp(<SkipStageDialog stage={stage()} isProcessing={false} onConfirm={vi.fn()} onCancel={onCancel} />);

    await user.click((await shownDialog()).getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });
});

describe('ReinstateStageDialog', () => {
  it('says how the stage was skipped, and needs a reason', async () => {
    const onConfirm = vi.fn();
    const skipped = stage({state: 'Skipped', isCurrent: false, skipReason: 'BranchNotChosen'});
    const {user} = renderWithApp(<ReinstateStageDialog stage={skipped} patient={patient} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(dialog.getByText('It was skipped: Another branch was chosen.')).toBeInTheDocument();
    const reinstate = dialog.getByRole('button', {name: 'Reinstate Stage'});
    expect(isDisabled(reinstate)).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Reason/}), ' Wrong branch chosen ');
    await user.click(reinstate);

    expect(onConfirm).toHaveBeenCalledWith('Wrong branch chosen', expect.any(Function));
    (onConfirm.mock.calls[0][1] as (error: unknown) => void)(new ApiError(409, undefined, 'The pathway is cancelled.'));
    expect(await dialog.findByText('The pathway is cancelled.')).toBeInTheDocument();
  });
});

describe('OutcomeChoice', () => {
  function Harness() {
    const [outcome, setOutcome] = useState('');
    return <OutcomeChoice stage={biopsy} outcome={outcome} onOutcomeChange={setOutcome} />;
  }

  it("lists the outcomes in the template's order with the stages each adds, none chosen, then says what recording one adds", async () => {
    const {user} = renderWithApp(<Harness />);

    const radios = await screen.findAllByRole('radio');
    expect(radios.map((r) => [r.getAttribute('value'), (r as HTMLInputElement).checked])).toEqual([
      ['Benign', false],
      ['Malignant', false],
    ]);
    expect(screen.getByText('Adds: Referral to hospital, Letter to GP')).toBeInTheDocument();
    expect(screen.queryByText(/will be added/)).not.toBeInTheDocument();

    await user.click(screen.getByRole('radio', {name: /Benign/}));
    expect(screen.getByText('These stages will be added: Final letter.')).toBeInTheDocument();
  });
});

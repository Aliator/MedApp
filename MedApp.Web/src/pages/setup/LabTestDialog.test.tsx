import {screen, within} from '@testing-library/react';
import {isDisabled, openDialog, renderWithApp, adminUser} from '../../test/render';
import {LAB_TESTS} from '../../test/specimens';
import {NO_PROBLEM} from '../saveProblem';
import {LabTestDialog, type LabTestDialogProps} from './LabTestDialog';

function renderDialog(props: Partial<LabTestDialogProps> = {}) {
  const onSave = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <LabTestDialog isOpen test={null} isProcessing={false} problem={NO_PROBLEM} onSave={onSave} onCancel={onCancel} {...props} />,
    {user: adminUser},
  );
  return {...result, onSave, onCancel};
}

const dialog = () => within(openDialog()!);

describe('LabTestDialog', () => {
  it('starts a new test at the RCPath turnaround with one step, and saves it once named', async () => {
    const {user, onSave} = renderDialog();

    expect(await screen.findByRole('heading', {name: 'Add Lab Test'})).toBeInTheDocument();
    expect(dialog().getByRole('spinbutton', {name: /Target turnaround/})).toHaveValue('7');
    expect(dialog().getByRole('spinbutton', {name: /Maximum turnaround/})).toHaveValue('10');
    expect(dialog().getByRole('spinbutton', {name: /Flag at risk/})).toHaveValue('2');
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Lab Test'}))).toBe(true);
    // The only step can't be removed.
    expect(isDisabled(dialog().getByRole('button', {name: 'Remove step 1'}))).toBe(true);

    await user.type(dialog().getByRole('textbox', {name: /^Name/}), 'Blood culture');
    await user.click(dialog().getByRole('combobox', {name: /Kind/}));
    await user.click(await screen.findByRole('option', {name: 'Microbiology'}));
    await user.type(dialog().getByRole('textbox', {name: /Step 1/}), 'Incubation');
    await user.click(dialog().getByRole('button', {name: 'Add Step'}));
    await user.type(dialog().getByRole('textbox', {name: /Step 2/}), 'Identification');
    await user.click(dialog().getAllByRole('switch', {name: 'Optional'})[0]);
    await user.click(dialog().getByRole('button', {name: 'Add Lab Test'}));

    expect(onSave).toHaveBeenCalledWith({
      id: undefined,
      name: 'Blood culture',
      kind: 'Microbiology',
      snomedCode: null,
      targetDays: 7,
      limitDays: 10,
      atRiskDays: 2,
      steps: [
        {name: 'Incubation', kind: 'Standard', isOptional: true},
        {name: 'Identification', kind: 'Standard', isOptional: false},
      ],
    });
  });

  it('edits a test, moving and removing its steps', async () => {
    const {user, onSave} = renderDialog({test: LAB_TESTS[0]});

    expect(await screen.findByRole('heading', {name: 'Edit Lab Test'})).toBeInTheDocument();
    expect(dialog().getByRole('textbox', {name: /SNOMED CT code/})).toHaveValue('252416005');
    expect(dialog().getByRole('textbox', {name: /Step 1/})).toHaveValue('Cut-up');
    await user.click(dialog().getByRole('button', {name: 'Move step 2 up'}));
    expect(dialog().getByRole('textbox', {name: /Step 1/})).toHaveValue('Sent for a second opinion');
    await user.click(dialog().getByRole('button', {name: 'Remove step 2'}));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        id: LAB_TESTS[0].id,
        kind: 'Histology',
        snomedCode: '252416005',
        steps: [{name: 'Sent for a second opinion', kind: 'SentForSecondOpinion', isOptional: true}],
      }),
    );
  });

  it('shows the API’s refusals', async () => {
    renderDialog({problem: {validation: null, message: "There's already a lab test called 'Skin histology'."}});
    expect(await screen.findByText("There's already a lab test called 'Skin histology'.")).toBeInTheDocument();
  });
});

import {screen, waitFor, within} from '@testing-library/react';
import type {Procedure} from '../../api/procedures';
import {isDisabled, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {AddToProcedureDialog} from './AddToProcedureDialog';

/** The dialog, once it's open. */
const dialogShown = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

const procedure = (overrides: Partial<Procedure> = {}): Procedure => ({
  id: 'p1',
  title: 'Synthetic knee review',
  patientName: 'Freya Ahmed',
  patient: {id: 'pat1', firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', nhsNumber: '9990000018'},
  canAddNotes: true,
  ...overrides,
});

function renderDialog(text: string, procedures = [{key: 'a', procedure: procedure()}]) {
  const onAdded = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <AddToProcedureDialog note={{text, isSelection: false}} procedures={procedures} onAdded={onAdded} onCancel={onCancel} />,
    {user: staffUser},
  );
  return {...result, onAdded, onCancel};
}

describe('AddToProcedureDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows the patient as the banner does, the procedure and the text, then adds it', async () => {
    mockApi({'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201)});
    const {user, onAdded} = renderDialog('Synthetic note to add.');

    const dialog = within(await dialogShown());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(dialog.getByText('Synthetic knee review')).toBeInTheDocument();
    expect(dialog.getByText('Synthetic note to add.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(onAdded).toHaveBeenCalledWith(true));
  });

  it("says why when the API refuses, and stays open", async () => {
    mockApi({
      'POST /api/procedures/patient-procedures/p1/notes': () =>
        json({title: "This patient's record is restricted, so it can't be changed until an admin lifts the restriction."}, 409),
    });
    const {user, onAdded} = renderDialog('Synthetic note.');

    await user.click(within(await dialogShown()).getByRole('button', {name: 'Add Note'}));

    expect(await screen.findByText(/record is restricted/)).toBeInTheDocument();
    expect(onAdded).not.toHaveBeenCalled();
  });

  it('refuses text longer than a note can be', async () => {
    renderDialog('a'.repeat(4001));

    const dialog = within(await dialogShown());
    expect(dialog.getByText(/Too long for a note/)).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Note'}))).toBe(true);
  });

  it('cancels', async () => {
    const {user, onCancel} = renderDialog('Synthetic note.');

    await user.click(within(await dialogShown()).getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalled();
  });
});

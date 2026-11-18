import {screen, waitFor, within} from '@testing-library/react';
import type {Pathway} from '../../api/pathways';
import {isDisabled, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {AddToPathwayDialog, CANT_REMOVE_SELECTION, type NoteToAdd} from './AddToPathwayDialog';

/** The dialog, once it's open. */
const dialogShown = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

const pathway = (overrides: Partial<Pathway> = {}): Pathway => ({
  id: 'p1',
  title: 'Synthetic knee review',
  patientName: 'Freya Ahmed',
  patient: {id: 'pat1', firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', nhsNumber: '9990000018'},
  canAddNotes: true,
  ...overrides,
});

function renderDialog(text: string, pathways = [{key: 'a', pathway: pathway()}], note: Partial<NoteToAdd> = {}) {
  const onAdded = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <AddToPathwayDialog
      note={{text, isSelection: false, remove: () => '', ...note}}
      pathways={pathways}
      onAdded={onAdded}
      onCancel={onCancel}
    />,
    {user: staffUser},
  );
  return {...result, onAdded, onCancel};
}

describe('AddToPathwayDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows the patient as the banner does, the pathway and the text, then adds it', async () => {
    mockApi({'POST /api/pathways/patient-pathways/p1/notes': () => json({id: 'n1', version: 1}, 201)});
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
      'POST /api/pathways/patient-pathways/p1/notes': () =>
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

  it("leaves a selection in the notepad, and says why, when it can't be taken out exactly", async () => {
    mockApi({'POST /api/pathways/patient-pathways/p1/notes': () => json({id: 'n1', version: 1}, 201)});
    const {user, onAdded} = renderDialog('Synthetic selected text.', undefined, {isSelection: true, remove: undefined});

    const dialog = within(await dialogShown());
    const checkbox = dialog.getByRole('checkbox', {name: 'Remove it from the notepad'});
    expect(checkbox).not.toBeChecked();
    expect(isDisabled(checkbox)).toBe(true);
    expect(dialog.getByText(CANT_REMOVE_SELECTION)).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(onAdded).toHaveBeenCalledWith(false));
  });

  it('cancels', async () => {
    const {user, onCancel} = renderDialog('Synthetic note.');

    await user.click(within(await dialogShown()).getByRole('button', {name: 'Cancel'}));

    expect(onCancel).toHaveBeenCalled();
  });
});

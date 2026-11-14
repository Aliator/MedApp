import {screen} from '@testing-library/react';
import type {ProcedureStage} from '../../api/procedures';
import {isDisabled, renderWithApp} from '../../test/render';
import {StageWork} from './StageWork';

const stage = (overrides: Partial<ProcedureStage> = {}): ProcedureStage => ({
  id: 's1',
  stageDefinitionId: 'd1',
  stageOrder: 1,
  name: 'Consultation',
  description: '',
  instructions: '',
  notes: 'Seen in clinic.',
  state: 'InProgress',
  isCurrent: true,
  isLocked: false,
  canEdit: true,
  canUnlock: false,
  ...overrides,
});

function renderWork(overrides: Partial<ProcedureStage> = {}, isAdmin = false) {
  const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
  const result = renderWithApp(<StageWork stage={stage(overrides)} isAdmin={isAdmin} isBusy={false} {...handlers} />);
  return {...result, ...handlers};
}

describe('StageWork', () => {
  it('has a five-line notes box, or one that fills the space around it (from two lines) when asked', async () => {
    const {unmount} = renderWork();
    expect(await screen.findByRole('textbox', {name: 'Notes'})).toHaveAttribute('rows', '5');
    unmount();

    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
    renderWithApp(<StageWork stage={stage()} isAdmin={false} isBusy={false} isNotesFilling {...handlers} />);
    // Nothing is laid out in tests, so it stays at its two-line minimum (FillingTextArea's tests cover the fill).
    expect(await screen.findByRole('textbox', {name: 'Notes'})).toHaveAttribute('rows', '2');
  });

  it('edits the notes, saving only once they change', async () => {
    const {user, onSaveNotes} = renderWork();

    const box = await screen.findByRole('textbox', {name: 'Notes'});
    expect(box).toHaveValue('Seen in clinic.');
    expect(isDisabled(screen.getByRole('button', {name: 'Save Notes'}))).toBe(true);

    await user.type(box, ' Plan agreed.');
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    expect(onSaveNotes).toHaveBeenCalledWith('Seen in clinic. Plan agreed.');
  });

  it('finishes the stage with the notes as typed', async () => {
    const {user, onFinish} = renderWork();

    await user.type(await screen.findByRole('textbox', {name: 'Notes'}), ' Done.');
    await user.click(screen.getByRole('button', {name: 'Finish Stage'}));

    expect(onFinish).toHaveBeenCalledWith('Seen in clinic. Done.');
  });

  it('offers to start a stage nobody has started yet', async () => {
    const {user, onStart} = renderWork({state: 'NotStarted', notes: ''});

    await user.click(await screen.findByRole('button', {name: 'Start Stage'}));

    expect(onStart).toHaveBeenCalled();
  });

  it('shows the notes read-only when the stage cannot be edited', async () => {
    renderWork({canEdit: false, state: 'Completed', isLocked: true});

    expect(await screen.findByText('Seen in clinic.')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Unlock Stage'})).not.toBeInTheDocument();
  });

  it('lets an admin unlock a finished, locked stage', async () => {
    const {user, onUnlock} = renderWork({canEdit: false, canUnlock: true, state: 'Completed', isLocked: true}, true);

    await user.click(await screen.findByRole('button', {name: 'Unlock Stage'}));

    expect(onUnlock).toHaveBeenCalled();
  });
});

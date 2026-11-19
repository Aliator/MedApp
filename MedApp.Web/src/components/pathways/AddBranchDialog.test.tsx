import {screen, waitFor, within} from '@testing-library/react';
import {ApiError} from '../../api/errors';
import type {Pathway} from '../../api/pathways';
import {isDisabled, openDialog, renderWithApp} from '../../test/render';
import {AddBranchDialog} from './AddBranchDialog';

const pathway: Pathway = {
  id: 'p1',
  patient: {firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: '9990000026'},
  stages: [
    {id: 's1', stageOrder: 1, name: 'Biopsy'},
    {id: 's2', stageOrder: 2, name: 'Histology'},
    {id: 's3', stageOrder: 3, name: 'Final letter'},
  ],
  addableBranches: [
    {templateBranchId: 'b1', name: 'Melanoma route', stageNames: ['Excision', 'Follow-up'], afterStageIds: ['s2']},
    {templateBranchId: 'b2', name: 'Used once', stageNames: ['Scan'], afterStageIds: []},
  ],
};

describe('AddBranchDialog', () => {
  it('offers only the branches that can be added, chooses nothing, and needs where and why', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(<AddBranchDialog pathway={pathway} isProcessing={false} onConfirm={onConfirm} onCancel={vi.fn()} />);
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    const add = dialog.getByRole('button', {name: 'Add Branch'});
    expect(isDisabled(add)).toBe(true);

    await user.click(dialog.getByRole('combobox', {name: /Branch/}));
    expect(screen.queryByRole('option', {name: 'Used once'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Melanoma route'}));
    expect(dialog.getByText('Its stages: 1. Excision and 2. Follow-up.')).toBeInTheDocument();
    await user.click(dialog.getByRole('combobox', {name: /Comes after/}));
    await user.click(screen.getByRole('option', {name: '2. Histology'}));
    expect(isDisabled(add)).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Reason/}), ' New histology ');
    await user.click(add);

    expect(onConfirm).toHaveBeenCalledWith({templateBranchId: 'b1', afterStageId: 's2', reason: 'New histology'}, expect.any(Function));
    (onConfirm.mock.calls[0][1] as (error: unknown) => void)(new ApiError(409, undefined, 'It was added already.'));
    expect(await dialog.findByText('It was added already.')).toBeInTheDocument();
  });
});

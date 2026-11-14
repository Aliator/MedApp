import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp, isDisabled} from '../../test/render';
import {SelectionDialog, type SelectionItem} from './SelectionDialog';

const roles: SelectionItem[] = [
  {value: 'Admin', label: 'Admin'},
  {value: 'User', label: 'User'},
];

function AssignRoles({items = roles, onConfirm = () => {}}: {items?: SelectionItem[]; onConfirm?: (selected: string[]) => void}) {
  const [selected, setSelected] = useState<string[]>([]);
  return (
    <SelectionDialog
      isOpen
      title="Assign Roles"
      message="Select roles to assign to a.hartwell."
      listLabel="Roles"
      items={items}
      selected={selected}
      onSelectedChange={setSelected}
      confirmText="Assign Roles"
      onConfirm={() => onConfirm(selected)}
      onCancel={() => {}}
      emptyTitle="This user already has all available roles."
    />
  );
}

describe('SelectionDialog', () => {
  it('lets the user tick items, then confirm with them', async () => {
    const onConfirm = vi.fn();
    const {user} = renderWithApp(<AssignRoles onConfirm={onConfirm} />);

    const confirm = await screen.findByRole('button', {name: 'Assign Roles'});
    expect(isDisabled(confirm)).toBe(true);

    await user.click(screen.getByRole('checkbox', {name: 'User'}));
    expect(isDisabled(confirm)).toBe(false);
    await user.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith(['User']);
  });

  it('shows the empty state, and no way to confirm, when there is nothing to choose', async () => {
    renderWithApp(<AssignRoles items={[]} />);

    expect(await screen.findByText('This user already has all available roles.')).toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Assign Roles'}))).toBe(true);
  });
});

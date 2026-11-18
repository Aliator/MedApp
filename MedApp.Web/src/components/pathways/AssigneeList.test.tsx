import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {AssigneeList} from './AssigneeList';

const assignment = {
  userId: 'u1',
  username: 'a.user',
  assignedByUserId: 'u2',
  assignedByUsername: 'admin',
  assignedAtUtc: '2026-11-01T11:00:00Z',
};

describe('AssigneeList', () => {
  it('shows who is assigned, by whom and when', async () => {
    renderWithApp(<AssigneeList assignments={[assignment]} canManage={false} isBusy={false} onAssign={vi.fn()} onUnassign={vi.fn()} />);

    expect(await screen.findByText('a.user')).toBeInTheDocument();
    expect(screen.getByText('Assigned by admin, 1 Nov 2026, 11:00')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('says when nobody is assigned', async () => {
    renderWithApp(<AssigneeList assignments={[]} canManage={false} isBusy={false} onAssign={vi.fn()} onUnassign={vi.fn()} />);

    expect(await screen.findByText('Nobody is assigned yet.')).toBeInTheDocument();
  });

  it('lets an admin assign and unassign staff', async () => {
    const onAssign = vi.fn();
    const onUnassign = vi.fn();
    const {user} = renderWithApp(
      <AssigneeList assignments={[assignment]} canManage isBusy={false} onAssign={onAssign} onUnassign={onUnassign} />,
    );

    await user.click(await screen.findByRole('button', {name: 'Assign Staff'}));
    await user.click(screen.getByRole('button', {name: 'Unassign a.user'}));

    expect(onAssign).toHaveBeenCalled();
    expect(onUnassign).toHaveBeenCalledWith(assignment);
  });
});

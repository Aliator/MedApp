import {screen, waitFor} from '@testing-library/react';
import {renderWithApp, isDisabled} from '../../test/render';
import {AppIcon} from '../shared/icons';
import {AvatarCell} from './AvatarCell';
import {TableActions} from './TableActions';

describe('AvatarCell', () => {
  it('shows the avatar, the name and the secondary text', async () => {
    renderWithApp(<AvatarCell primaryText="Freya Ahmed" secondaryText="ID: f184a7b8" />);

    expect(await screen.findByRole('img', {name: 'Freya Ahmed'})).toBeInTheDocument();
    expect(screen.getByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.getByText('ID: f184a7b8')).toBeInTheDocument();
  });

  it('can do without secondary text', async () => {
    renderWithApp(<AvatarCell primaryText="a.hartwell" />);

    expect(await screen.findByText('a.hartwell')).toBeInTheDocument();
  });
});

describe('TableActions', () => {
  it('links, runs and disables actions', async () => {
    const onDelete = vi.fn();
    const {user, router} = renderWithApp(
      <TableActions
        actions={[
          {label: 'View', icon: AppIcon.View, href: '/patients/p1'},
          {label: 'Delete', icon: AppIcon.Delete, onClick: onDelete, isDestructive: true},
          {label: 'Reset', icon: AppIcon.Password, onClick: () => {}, isDisabled: true},
        ]}
      />,
      {path: '/patients'},
    );

    await user.click(await screen.findByRole('button', {name: 'Delete'}));
    expect(onDelete).toHaveBeenCalledOnce();
    expect(isDisabled(screen.getByRole('button', {name: 'Reset'}))).toBe(true);

    await user.click(await screen.findByRole('link', {name: 'View'}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients/p1'));
  });
});

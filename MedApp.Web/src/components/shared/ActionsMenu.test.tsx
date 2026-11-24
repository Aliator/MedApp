import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ActionsMenu} from './ActionsMenu';
import {AppIcon} from './icons';

describe('ActionsMenu', () => {
  it('opens a menu of the actions, running the one chosen', async () => {
    const keep = vi.fn();
    const {user} = renderWithApp(
      <ActionsMenu
        name="Actions for the record of Test Synthetic"
        actions={[
          {label: 'Keep', icon: AppIcon.Archive, onClick: keep},
          {label: 'Anonymise', icon: AppIcon.Delete, isDestructive: true, onClick: () => {}},
        ]}
      />,
    );

    const button = await screen.findByRole('button', {name: 'Actions for the record of Test Synthetic'});
    expect(button).toHaveTextContent('Actions');
    await user.click(button);
    expect(await screen.findByRole('menuitem', {name: 'Anonymise'})).toBeInTheDocument();
    await user.click(screen.getByRole('menuitem', {name: 'Keep'}));

    expect(keep).toHaveBeenCalledTimes(1);
  });

  it('shows a disabled action as disabled', async () => {
    const {user} = renderWithApp(<ActionsMenu actions={[{label: 'Edit roles', isDisabled: true}, {label: 'Edit teams'}]} />);

    await user.click(await screen.findByRole('button', {name: 'Actions'}));

    expect(await screen.findByRole('menuitem', {name: 'Edit roles'})).toHaveAttribute('aria-disabled', 'true');
  });
});

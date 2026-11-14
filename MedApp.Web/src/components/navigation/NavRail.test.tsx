import {SideNavItem} from '@astryxdesign/core/SideNav';
import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {AppIcon} from '../shared/icons';
import {NavRail, type NavRailEntry} from './NavRail';

const entries: NavRailEntry[] = [
  {key: 'home', label: 'Home', icon: AppIcon.Home, href: '/', isSelected: true, row: <SideNavItem label="Home row" icon={AppIcon.Home} href="/" />},
  {key: 'logout', label: 'Logout', icon: AppIcon.Logout, isSelected: false, row: <SideNavItem label="Logout row" icon={AppIcon.Logout} />},
];

describe('NavRail', () => {
  it('shows each item as an icon, the current one marked, linking where it goes', async () => {
    renderWithApp(<NavRail groups={[entries]} />);

    const home = await screen.findByRole('link', {name: 'Home'});
    expect(home).toHaveAttribute('href', '/');
    expect(home).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', {name: 'Logout'})).toBeInTheDocument();
  });

  it("brings an item's row out while the pointer is on its icon, and takes it away after", async () => {
    const {user} = renderWithApp(<NavRail groups={[entries]} wrapRow={(row) => <div data-testid="wrapped">{row}</div>} />);

    const home = await screen.findByRole('link', {name: 'Home'});
    await user.hover(home);
    await waitFor(() => expect(screen.getByText('Home row')).toBeVisible());
    expect(screen.getAllByTestId('wrapped').length).toBeGreaterThan(0);

    await user.unhover(home);
    await waitFor(() => expect(screen.queryByText('Home row')).not.toBeVisible());
  });

  it('brings rows out at once when the hover delay is None (Account Settings)', async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const {user} = renderWithApp(<NavRail groups={[entries]} />);

    await user.hover(await screen.findByRole('link', {name: 'Home'}));

    expect(screen.getByText('Home row')).toBeVisible();
  });
});

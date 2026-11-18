import {SideNavItem} from '@astryxdesign/core/SideNav';
import {act, fireEvent, screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {AppIcon} from '../shared/icons';
import {useContext} from 'react';
import {FlyoutHoldContext, NavRail, type NavRailEntry} from './NavRail';

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

  it('keeps a row out while something in it holds it (a pinned menu), and takes it away when let go', async () => {
    function Holder() {
      const hold = useContext(FlyoutHoldContext);
      return (
        <>
          <button type="button" onClick={() => hold?.(true)}>
            Hold
          </button>
          <button type="button" onClick={() => hold?.(false)}>
            Let go
          </button>
        </>
      );
    }
    const held: NavRailEntry[] = [{...entries[0], row: <Holder />}];
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const {user} = renderWithApp(<NavRail groups={[held]} />);

    const home = await screen.findByRole('link', {name: 'Home'});
    await user.hover(home);
    await user.click(await screen.findByRole('button', {name: 'Hold'}));
    // The pointer leaves altogether: the row stays.
    await user.unhover(home);
    await user.hover(document.body);
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(screen.getByRole('button', {name: 'Let go', hidden: true})).toBeVisible();

    // Let go with the pointer away: it goes.
    fireEvent.click(screen.getByRole('button', {name: 'Let go', hidden: true}));
    await waitFor(() => expect(screen.queryByRole('button', {name: 'Let go', hidden: true})).not.toBeVisible());
  });

  it('keeps a row out when let go while the pointer is still on it', async () => {
    function Holder() {
      const hold = useContext(FlyoutHoldContext);
      return (
        <button type="button" onClick={() => (hold?.(true), hold?.(false))}>
          Hold and let go
        </button>
      );
    }
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const {user} = renderWithApp(<NavRail groups={[[{...entries[0], row: <Holder />}]]} />);

    await user.hover(await screen.findByRole('link', {name: 'Home'}));
    // No pointer movement: the pointer stays on the item. Then longer than the row's wait to close.
    fireEvent.click(await screen.findByRole('button', {name: 'Hold and let go'}));
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(screen.getByRole('button', {name: 'Hold and let go'})).toBeVisible();
  });

  it('brings rows out at once when the hover delay is None (Account Settings)', async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const {user} = renderWithApp(<NavRail groups={[entries]} />);

    await user.hover(await screen.findByRole('link', {name: 'Home'}));

    expect(screen.getByText('Home row')).toBeVisible();
  });

  it('swaps rows at once once one is out, rather than waiting at every icon', async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'short');
    const {user} = renderWithApp(<NavRail groups={[entries.slice(0, 1), entries.slice(1)]} />);

    await user.hover(await screen.findByRole('link', {name: 'Home'}));
    await waitFor(() => expect(screen.getByText('Home row')).toBeVisible());
    await user.hover(screen.getByRole('button', {name: 'Logout'}));

    expect(screen.getByText('Logout row')).toBeVisible();
    await waitFor(() => expect(screen.queryByText('Home row')).not.toBeVisible());
  });

  it('waits the whole delay from when the pointer first rests on an icon, and none for one it has left', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    localStorage.setItem('medapp.navFlyoutDelay', 'long');
    renderWithApp(<NavRail groups={[entries]} />);
    const home = await screen.findByRole('link', {name: 'Home'});
    const logout = screen.getByRole('button', {name: 'Logout'});

    // Passing over Home on the way to Logout: Home's row never comes out.
    fireEvent.mouseOver(home);
    await act(() => vi.advanceTimersByTimeAsync(500));
    fireEvent.mouseLeave(home);
    fireEvent.mouseOver(logout);
    await act(() => vi.advanceTimersByTimeAsync(600));
    // Moving over the icon's own parts doesn't start the wait again.
    fireEvent.mouseOver(logout.firstElementChild ?? logout);
    expect(screen.queryByText('Logout row')).not.toBeVisible();
    await act(() => vi.advanceTimersByTimeAsync(400));

    expect(screen.getByText('Logout row')).toBeVisible();
    expect(screen.queryByText('Home row')).not.toBeVisible();
  });

  it('shows a row with nothing to link to (Logout) when its icon is clicked', async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'longer');
    const {user} = renderWithApp(<NavRail groups={[entries]} />);

    fireEvent.click(await screen.findByRole('button', {name: 'Logout'}));

    await waitFor(() => expect(screen.getByText('Logout row')).toBeVisible());
    await user.unhover(screen.getByRole('button', {name: 'Logout'}));
  });

  it('brings a row out and takes it away with no animation', async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    const finish = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'getAnimations', {configurable: true, value: () => [{finish}]});
    try {
      const {user} = renderWithApp(<NavRail groups={[entries]} />);
      const home = await screen.findByRole('link', {name: 'Home'});

      await user.hover(home);
      expect(screen.getByText('Home row')).toBeVisible();
      expect(finish).toHaveBeenCalled();
      finish.mockClear();

      await user.unhover(home);
      await waitFor(() => expect(screen.queryByText('Home row')).not.toBeVisible());
      expect(finish).toHaveBeenCalled();
    } finally {
      delete (HTMLElement.prototype as {getAnimations?: unknown}).getAnimations;
    }
  });

  it("starts the rows under an item's own at the nav's edge, so the icons below stay in sight", async () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'none');
    // jsdom has no layout: a nav 64px wide, whose hover areas are padded 8px across and 2px above and below, and
    // rows 240px wide.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.classList.contains('astryx-app-shell-sidenav')) return new DOMRect(0, 0, 64, 800);
      return new DOMRect(0, 0, 240, 40);
    });
    const realStyle = window.getComputedStyle.bind(window);
    vi.spyOn(window, 'getComputedStyle').mockImplementation((element, pseudo) => {
      const style = realStyle(element, pseudo);
      const padded: Record<string, string> = {paddingLeft: '8px', paddingTop: '2px', paddingBottom: '2px', borderBottomLeftRadius: '6px'};
      return new Proxy(style, {
        get: (target, name) => {
          if (typeof name === 'string' && name in padded) return padded[name];
          const value = Reflect.get(target, name, target) as unknown;
          return typeof value === 'function' ? value.bind(target) : value;
        },
      });
    });
    const patients: NavRailEntry = {
      key: 'patients',
      label: 'Patients',
      icon: AppIcon.Patients,
      href: '/patients',
      isSelected: false,
      isHalfView: true,
      row: (
        <SideNavItem label="Patients row" icon={AppIcon.Patients} href="/patients" collapsible={false}>
          {/* With its own buttons, as a pinned patient has: the row is then the link and its buttons. */}
          <SideNavItem label="Test Patient" icon={AppIcon.SubItem} href="/patients/test" actions={<button type="button">Test Patient options</button>} />
        </SideNavItem>
      ),
    };
    const {user} = renderWithApp(
      <div className="astryx-app-shell-sidenav">
        <NavRail groups={[[patients]]} />
      </div>,
    );

    await user.hover(await screen.findByRole('link', {name: 'Patients'}));

    const under = screen.getByRole('link', {name: 'Test Patient'}).closest<HTMLElement>('.astryx-side-nav-item')!;
    await waitFor(() => expect(under.style.marginInlineStart).toBe('56px'));
    expect(under.style.inlineSize).toBe('184px');
    expect(under.style.marginBlockStart).toBe('4px');
    // No indent: the arrow and name sit at the row's start.
    expect(under.querySelector('a')!.style.paddingInlineStart).toBe('0px');
  });
});

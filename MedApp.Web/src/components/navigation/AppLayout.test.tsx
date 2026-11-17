import {SideNavRenderContext} from '@astryxdesign/core/SideNav';
import {render, screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {adminUser, isDisabled, json, mockFetch, pageOf, renderApp, rowMenuItem} from '../../test/render';
import {SideNavTheme} from './AppLayout';

/** Every list is empty: enough for the pages to render their headers. */
/** No data anywhere: empty lists (an empty page from the search endpoints, which the rest are). */
const emptyApi = () =>
  mockFetch((request) => {
    const {pathname} = new URL(request.url);
    const isPlainList = /\/(templates|definitions|roles)$/.test(pathname);
    return json(isPlainList ? [] : pageOf([]));
  });

// The whole app renders, and its first route chunk can be slow to load.
const heading = (name: string) => screen.findByRole('heading', {name, level: 1}, {timeout: 15000});

/** A nav item's "Open in half view", from its row's menu. */
const halfViewItem = (user: ReturnType<typeof userEvent.setup>, item: string) =>
  rowMenuItem(user, item, 'Open in half view');

/**
 * A nav item's row, brought out of the folded nav (a half view is open) by hovering its icon. It comes out after a
 * short delay.
 */
async function railRow(user: ReturnType<typeof userEvent.setup>, item: string) {
  const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
  // The icon is a link too; the row is the second one.
  await user.hover(nav.getByRole('link', {name: item}));
  await waitFor(() => expect(nav.getAllByRole('link', {name: item})).toHaveLength(2), {timeout: 3000});
  return nav.getAllByRole('link', {name: item})[1];
}

describe('SideNavTheme', () => {
  it("keeps the side nav dark in its column and drawer, but takes the app's mode on a phone's top bar", () => {
    const {unmount} = render(
      <SideNavRenderContext value="default">
        <SideNavTheme>
          <p>Side nav</p>
        </SideNavTheme>
      </SideNavRenderContext>,
    );
    expect(screen.getByText('Side nav').closest('[data-theme]')).toHaveAttribute('data-theme', 'dark');
    unmount();

    // The top bar sits on the page's own background: white text there was unreadable in light mode.
    render(
      <SideNavRenderContext value="topbar">
        <SideNavTheme>
          <p>Top bar</p>
        </SideNavTheme>
      </SideNavRenderContext>,
    );
    expect(screen.getByText('Top bar').closest('[data-theme="dark"]')).toBeNull();
  });
});

describe('AppLayout scrollbar room', {timeout: 15_000}, () => {
  beforeEach(() => sessionStorage.clear());

  // jsdom has no layout, but it does apply the rule (`:has()` included), so its effect can be read as computed styles.
  const gutterOf = (element: Element | null) => (element ? getComputedStyle(element).scrollbarGutter : 'missing');

  it('keeps room for a scrollbar on both sides of the main area, so pages that scroll line up with pages that do not', async () => {
    emptyApi();
    renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    expect(document.querySelectorAll('[role="main"]')).toHaveLength(1);
    expect(gutterOf(document.querySelector('[role="main"]'))).toBe('stable both-edges');
  });

  it('leaves the room to the halves in a half view, each keeping its own on both sides', async () => {
    sessionStorage.setItem('medapp.halfView', '/admin/audit');
    emptyApi();
    renderApp({path: '/patients', user: adminUser});

    await heading('Audit Log');
    // The halves scroll, not the main area, so the main area keeps no room of its own (two gutters would be uneven).
    expect(gutterOf(document.querySelector('[role="main"]'))).toBe('auto');
    for (const half of ['Left half', 'Right half']) {
      expect(gutterOf(document.querySelector(`[aria-label="${half}"]`))).toBe('stable both-edges');
    }
  });
});

// The whole app renders, and the first test loads its routes: more than the default 5s on a slow machine.
describe('AppLayout half views', {timeout: 15_000}, () => {
  beforeEach(() => sessionStorage.clear());

  it("opens a nav item's page beside the current one, folding the nav away", async () => {
    emptyApi();
    const {user} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    // The page that's open can't be opened again.
    const patients = await halfViewItem(user, 'Patients');
    expect(isDisabled(patients)).toBe(true);
    expect(patients).toHaveTextContent('Patients is already open');
    await user.unhover(screen.getAllByRole('link', {name: 'Patients'})[0]);
    await user.click(await halfViewItem(user, 'Audit Log'));

    expect(await heading('Audit Log')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Patients', level: 1})).toBeInTheDocument();
    expect(screen.getAllByRole('button', {name: 'Close half view'})).toHaveLength(2);
    expect(sessionStorage.getItem('medapp.halfView')).toBe('/admin/audit');
  });

  it('closing the half view leaves the current page on its own, with the nav open', async () => {
    emptyApi();
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    await user.click(await halfViewItem(user, 'Audit Log'));
    await heading('Audit Log');
    const closes = screen.getAllByRole('button', {name: 'Close half view'});
    await user.click(closes[closes.length - 1]);

    await waitFor(() => expect(screen.queryByRole('heading', {name: 'Audit Log', level: 1})).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe('/patients');
    expect(screen.queryByRole('button', {name: 'Close half view'})).not.toBeInTheDocument();
    expect(await halfViewItem(user, 'My Tasks')).toBeInTheDocument();
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it("closing the current page's half opens the half view's page on its own", async () => {
    emptyApi();
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    await user.click(await halfViewItem(user, 'Audit Log'));
    await heading('Audit Log');
    // The Patients page's header: the nearest box round its title that holds a close button.
    let patientsHeader = screen.getByRole('heading', {name: 'Patients', level: 1}).parentElement!;
    while (!within(patientsHeader).queryByRole('button', {name: 'Close half view'})) patientsHeader = patientsHeader.parentElement!;
    await user.click(within(patientsHeader).getByRole('button', {name: 'Close half view'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/audit'));
    expect(await heading('Audit Log')).toBeInTheDocument();
    expect(screen.queryByRole('heading', {name: 'Patients', level: 1})).not.toBeInTheDocument();
  });

  it('closes the half view when the main view goes to the same page', async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/admin/audit');
    const {router} = renderApp({path: '/patients', user: adminUser});

    await heading('Audit Log');
    await router.navigate({to: '/admin/audit'});

    await waitFor(() => expect(screen.queryByRole('button', {name: 'Close half view'})).not.toBeInTheDocument());
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it('with a half view open, clicking a nav item opens its page on its own', async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/admin/audit');
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Audit Log');
    await user.click(await railRow(user, 'Templates'));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/templates'));
    await waitFor(() => expect(screen.queryByRole('button', {name: 'Close half view'})).not.toBeInTheDocument());
    expect(screen.queryByRole('heading', {name: 'Audit Log', level: 1})).not.toBeInTheDocument();
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it('with the setting off, clicking a nav item opens it on the left, keeping the half view', async () => {
    emptyApi();
    localStorage.setItem('medapp.navClosesHalfView', 'false');
    sessionStorage.setItem('medapp.halfView', '/admin/audit');
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Audit Log');
    await user.click(await railRow(user, 'Users'));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/users'));
    expect(await heading('User Management')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Audit Log', level: 1})).toBeInTheDocument();
  });

  it("with a half view open, a nav item's menu opens it on the left or the right", async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/admin/audit');
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Audit Log');
    // A page open on either side can't be opened on either side.
    const patientsLeft = await rowMenuItem(user, 'Patients', 'Open on the left');
    expect(isDisabled(patientsLeft)).toBe(true);
    expect(patientsLeft).toHaveTextContent('Patients is already open on the left');
    const patientsMenu = within(patientsLeft.closest<HTMLElement>('[role="menu"]')!);
    expect(isDisabled(patientsMenu.getByRole('menuitem', {name: (name) => name.startsWith('Open on the right'), hidden: true}))).toBe(true);
    await user.unhover(screen.getAllByRole('link', {name: 'Patients'})[0]);
    const auditRight = await rowMenuItem(user, 'Audit Log', 'Open on the right');
    expect(isDisabled(auditRight)).toBe(true);
    expect(auditRight).toHaveTextContent('Audit Log is already open on the right');

    await user.unhover(screen.getAllByRole('link', {name: 'Audit Log'})[0]);
    await user.click(await rowMenuItem(user, 'Users', 'Open on the right'));
    await waitFor(() => expect(sessionStorage.getItem('medapp.halfView')).toBe('/admin/users'));

    await user.click(await rowMenuItem(user, 'Templates', 'Open on the left'));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/templates'));
    expect(sessionStorage.getItem('medapp.halfView')).toBe('/admin/users');
  });

  it('keeps the half view open across a reload', async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/admin/templates');
    renderApp({path: '/patients', user: adminUser});

    expect(await heading('Templates')).toBeInTheDocument();
    expect(await heading('Patients')).toBeInTheDocument();
  });

  it('opens on the page alone when this browser blocks storage', async () => {
    emptyApi();
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });
    renderApp({path: '/patients', user: adminUser});

    expect(await heading('Patients')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Close half view'})).not.toBeInTheDocument();
  });
});

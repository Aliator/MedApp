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
      // Equal halves: neither grows to its content's narrowest width.
      expect(getComputedStyle(document.querySelector(`[aria-label="${half}"]`)!).minWidth).toBe('0px');
      expect(getComputedStyle(document.querySelector(`[aria-label="${half}"]`)!).flexBasis).toMatch(/^0(px)?$/);
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

  it('shows the notepad in the half view, and closing the page beside it docks the notepad rather than leaving the app for it', async () => {
    mockFetch((request) => {
      const {pathname} = new URL(request.url);
      if (pathname === '/api/me/notepad') return json({text: 'Synthetic jotting.', lastUpdatedUtc: '2026-11-17T10:32:00Z', version: 3, keepDays: 30});
      return json(/\/(templates|definitions|roles)$/.test(pathname) ? [] : pageOf([]));
    });
    sessionStorage.setItem('medapp.halfView', '/notepad');
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    const right = screen.getByLabelText('Right half');
    expect(await within(right).findByRole('region', {name: 'Notepad'}, {timeout: 15000})).toBeInTheDocument();
    // The notepad has no page header: the only Close half view is the Patients page's.
    await user.click(screen.getByRole('button', {name: 'Close half view'}));

    await waitFor(() => expect(screen.queryByLabelText('Right half')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe('/patients');
    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    expect(localStorage.getItem('medapp.notepadOpen')).toBe('true');
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it("docking the notepad from the side nav closes the half view showing it, so it isn't open twice side by side", async () => {
    mockFetch((request) => {
      const {pathname} = new URL(request.url);
      if (pathname === '/api/me/notepad') return json({text: 'Synthetic jotting.', lastUpdatedUtc: '2026-11-17T10:32:00Z', version: 3, keepDays: 30});
      return json(/\/(templates|definitions|roles)$/.test(pathname) ? [] : pageOf([]));
    });
    sessionStorage.setItem('medapp.halfView', '/notepad');
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    expect(await within(screen.getByLabelText('Right half')).findByRole('region', {name: 'Notepad'}, {timeout: 15000})).toBeInTheDocument();
    // The folded nav's Notepad icon brings its row out; the row is the toggle.
    const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
    await user.click(nav.getByRole('button', {name: 'Notepad'}));
    await waitFor(() => expect(nav.getAllByRole('button', {name: 'Notepad'})).toHaveLength(2), {timeout: 3000});
    await user.click(nav.getAllByRole('button', {name: 'Notepad'})[1]);

    await waitFor(() => expect(screen.queryByLabelText('Right half')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe('/patients');
    expect(await screen.findAllByRole('region', {name: 'Notepad'})).toHaveLength(1);
    expect(localStorage.getItem('medapp.notepadOpen')).toBe('true');
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
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

// The whole app renders: more than the default 5s on a slow machine.
describe('AppLayout notepad', {timeout: 20_000}, () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('opens the notepad from the side nav, docked beside the page, and keeps it open moving to another page', async () => {
    emptyApi();
    const {user} = renderApp({path: '/patients', user: adminUser});
    await heading('Patients');

    const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
    await user.click(nav.getByRole('button', {name: 'Notepad'}));

    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    expect(document.querySelector('[data-notepad-docked]')).not.toBeNull();
    expect(localStorage.getItem('medapp.notepadOpen')).toBe('true');

    await user.click(nav.getAllByRole('link', {name: 'Procedures'})[0]);
    await heading('Procedures');
    expect(screen.getByRole('region', {name: 'Notepad'})).toBeInTheDocument();

    // The same button closes it.
    await user.click(nav.getByRole('button', {name: 'Notepad'}));
    await waitFor(() => expect(screen.queryByRole('region', {name: 'Notepad'})).not.toBeInTheDocument());
    expect(localStorage.getItem('medapp.notepadOpen')).toBeNull();
  });

  it('opens the notepad again after a reload, and leaves the page room beside it', async () => {
    localStorage.setItem('medapp.notepadOpen', 'true');
    emptyApi();
    renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    // The page scrolls on its own beside it, so the main area keeps no scrollbar room of its own.
    expect(getComputedStyle(document.querySelector('[role="main"]')!).scrollbarGutter).toBe('auto');
  });

  it('keeps the page as it is (the same elements, and so anything typed) when the notepad opens and closes', async () => {
    emptyApi();
    const {user} = renderApp({path: '/patients', user: adminUser});
    const page = await heading('Patients');

    const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
    await user.click(nav.getByRole('button', {name: 'Notepad'}));
    await screen.findByRole('region', {name: 'Notepad'});
    expect(page.isConnected).toBe(true);
    expect(screen.getByRole('heading', {name: 'Patients', level: 1})).toBe(page);

    await user.click(nav.getByRole('button', {name: 'Notepad', pressed: true}));
    await waitFor(() => expect(screen.queryByRole('region', {name: 'Notepad'})).not.toBeInTheDocument());
    expect(page.isConnected).toBe(true);
    expect(screen.getByRole('heading', {name: 'Patients', level: 1})).toBe(page);
  });

  it('keeps both halves of a half view, the right one on the page it has got to, when the notepad opens and closes', async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/patients');
    const {user} = renderApp({path: '/admin/audit', user: adminUser});
    const left = await heading('Audit Log');
    await heading('Patients');

    // Move on in the right half, from the patients list to adding one.
    await user.click(screen.getByRole('link', {name: 'Add Patient'}));
    const right = await heading('Add New Patient');
    await waitFor(() => expect(sessionStorage.getItem('medapp.halfView')).toBe('/patients/add'));

    // The nav is folded to its icons: the icon brings its row out, and the row's button opens the notepad.
    const toggleNotepad = async () => {
      const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
      await user.click(nav.getAllByRole('button', {name: 'Notepad'})[0]);
      await waitFor(() => expect(screen.getAllByRole('button', {name: 'Notepad'}).length).toBeGreaterThan(1));
      const buttons = screen.getAllByRole('button', {name: 'Notepad'});
      await user.click(buttons[buttons.length - 1]);
    };
    await toggleNotepad();
    await screen.findByRole('region', {name: 'Notepad'});
    expect(left.isConnected && right.isConnected).toBe(true);

    await toggleNotepad();
    await waitFor(() => expect(screen.queryByRole('region', {name: 'Notepad'})).not.toBeInTheDocument());
    expect(left.isConnected && right.isConnected).toBe(true);
    expect(screen.getByRole('heading', {name: 'Add New Patient', level: 1})).toBe(right);
  });

  it('gives the focus to the notepad as it opens, and back to the side nav when its own Close closes it', async () => {
    emptyApi();
    const {user} = renderApp({path: '/patients', user: adminUser});
    await heading('Patients');

    const nav = within(screen.getAllByRole('navigation', {name: 'Side navigation'})[0]);
    await user.click(nav.getByRole('button', {name: 'Notepad'}));
    await waitFor(() => expect(screen.getByRole('textbox', {name: 'Notepad'})).toHaveFocus());

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));
    await waitFor(() => expect(screen.queryByRole('region', {name: 'Notepad'})).not.toBeInTheDocument());
    await waitFor(() => expect(nav.getByRole('button', {name: 'Notepad'})).toHaveFocus());
  });
});

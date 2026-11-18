import {AppShellMobileContext, type AppShellMobileContextValue} from '@astryxdesign/core/AppShell';
import {Button} from '@astryxdesign/core/Button';
import {Link} from '@astryxdesign/core/Link';
import {DataTable} from '../tables/DataTable';
import {screen, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {HalfViewsContext, type HalfViews} from '../pages/halfView';
import {renderWithApp} from '../../test/render';
import {HALF_VIEW_HREF, halfViewHref, HalfViewClicks, halfViewSide} from './HalfViewClicks';

const phone: AppShellMobileContextValue = {
  isMobile: true,
  isMobileNavOpen: false,
  toggleMobileNav: () => {},
  openMobileNav: () => {},
  closeMobileNav: () => {},
  isMobileNavEnabled: true,
  hasAutoToggle: true,
};

function renderLinks(ui: ReactNode, {openPath = null, path = '/', isPhone = false}: {openPath?: string | null; path?: string; isPhone?: boolean} = {}) {
  const halfViews: HalfViews = {open: vi.fn(), openPath, openFull: vi.fn(), openOnLeft: vi.fn(), close: vi.fn()};
  const content = (
    <HalfViewsContext.Provider value={halfViews}>
      <HalfViewClicks />
      {ui}
    </HalfViewsContext.Provider>
  );
  const result = renderWithApp(isPhone ? <AppShellMobileContext.Provider value={phone}>{content}</AppShellMobileContext.Provider> : content, {path});
  return {...result, halfViews};
}

/** A click with a key held: user-event keeps it held between the keyboard calls. */
async function clickWith(key: string, user: ReturnType<typeof renderLinks>['user'], element: HTMLElement) {
  await user.keyboard(`{${key}>}`);
  await user.click(element);
  await user.keyboard(`{/${key}}`);
}
const shiftClick = (user: ReturnType<typeof renderLinks>['user'], element: HTMLElement) => clickWith('Shift', user, element);

describe('HalfViewClicks', () => {
  it('opens a button link in the half view, and leaves the main view where it was', async () => {
    const {user, halfViews, router} = renderLinks(<Button label="Add Patient" href="/patients/add" />, {path: '/patients'});

    await shiftClick(user, await screen.findByRole('link', {name: 'Add Patient'}));

    expect(halfViews.open).toHaveBeenCalledWith('/patients/add');
    expect(router.state.location.pathname).toBe('/patients');
  });

  it('opens a link in a grid row, with its query, in place of the page already in the half view', async () => {
    const {user, halfViews, router} = renderLinks(
      <DataTable
        data={[{id: 'p1'}]}
        idKey="id"
        columns={[{key: 'name', header: 'Name', renderCell: (row) => <Link href={`/patients/${row.id}?tab=timeline`}>Synthetic Patient</Link>}]}
      />,
      {openPath: '/admin/audit', path: '/patients'},
    );

    await shiftClick(user, await screen.findByRole('link', {name: 'Synthetic Patient'}));

    expect(halfViews.open).toHaveBeenCalledWith('/patients/p1?tab=timeline');
    expect(router.state.location.pathname).toBe('/patients');
  });

  it('opens a link the same way with Shift+Enter', async () => {
    const {user, halfViews} = renderLinks(<Link href="/calendar">Calendar</Link>);

    (await screen.findByRole('link', {name: 'Calendar'})).focus();
    await user.keyboard('{Shift>}{Enter}{/Shift}');

    expect(halfViews.open).toHaveBeenCalledWith('/calendar');
  });

  it('opens what an element marked with its page opens (the Notepad toggle), instead of its own click', async () => {
    const onClick = vi.fn();
    const {user, halfViews} = renderLinks(<Button label="Notepad" onClick={onClick} {...{[HALF_VIEW_HREF]: '/notepad'}} />);

    await shiftClick(user, await screen.findByRole('button', {name: 'Notepad'}));

    expect(halfViews.open).toHaveBeenCalledWith('/notepad');
    expect(onClick).not.toHaveBeenCalled();
  });

  it('leaves a plain click to the link, in the main view', async () => {
    const {user, halfViews, router} = renderLinks(<Link href="/tasks">My Worklist</Link>, {openPath: '/admin/audit'});

    await user.click(await screen.findByRole('link', {name: 'My Worklist'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/tasks'));
    expect(halfViews.open).not.toHaveBeenCalled();
    expect(halfViews.openOnLeft).not.toHaveBeenCalled();
  });

  it.each(['Control', 'Meta'])('opens a link on the left with %s held, with or without a half view, and not in a new tab', async (key) => {
    for (const openPath of [null, '/admin/audit']) {
      const {user, halfViews, router, unmount} = renderLinks(<Link href="/tasks">My Worklist</Link>, {openPath, path: '/patients'});

      const link = await screen.findByRole('link', {name: 'My Worklist'});
      const clicked = vi.fn((event: MouseEvent) => event.defaultPrevented);
      document.addEventListener('click', clicked);
      await clickWith(key, user, link);
      document.removeEventListener('click', clicked);

      expect(halfViews.openOnLeft).toHaveBeenCalledWith('/tasks');
      expect(halfViews.open).not.toHaveBeenCalled();
      // Stopped before the link or anything else saw it.
      expect(clicked).not.toHaveBeenCalled();
      expect(router.state.location.pathname).toBe('/patients');
      unmount();
    }
  });

  it('opens a link on the left with Ctrl+Enter', async () => {
    const {user, halfViews} = renderLinks(<Link href="/calendar">Calendar</Link>);

    (await screen.findByRole('link', {name: 'Calendar'})).focus();
    await user.keyboard('{Control>}{Enter}{/Control}');

    expect(halfViews.openOnLeft).toHaveBeenCalledWith('/calendar');
  });

  it('opens the notepad toggle on the right with Ctrl too, as its page is only for the half view', async () => {
    const {user, halfViews} = renderLinks(<Button label="Notepad" onClick={() => {}} {...{[HALF_VIEW_HREF]: '/notepad'}} />);

    await clickWith('Control', user, await screen.findByRole('button', {name: 'Notepad'}));

    expect(halfViews.open).toHaveBeenCalledWith('/notepad');
    expect(halfViews.openOnLeft).not.toHaveBeenCalled();
  });

  it('does what a plain click does on a button with no page (a dialog action)', async () => {
    const onClick = vi.fn();
    const {user, halfViews} = renderLinks(<Button label="Confirm" onClick={onClick} />);

    await shiftClick(user, await screen.findByRole('button', {name: 'Confirm'}));

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(halfViews.open).not.toHaveBeenCalled();
  });

  it('opens a link to the page already on the left as a plain click would, not twice side by side', async () => {
    const {user, halfViews, router} = renderLinks(<Link href="/patients?page=2">Next page</Link>, {path: '/patients'});

    await shiftClick(user, await screen.findByRole('link', {name: 'Next page'}));

    await waitFor(() => expect(router.state.location.search).toEqual({page: 2}));
    expect(halfViews.open).not.toHaveBeenCalled();
  });

  it('leaves everything to the browser on a phone, where there is no half view', async () => {
    const {user, halfViews} = renderLinks(<Link href="/tasks">My Worklist</Link>, {isPhone: true});

    await shiftClick(user, await screen.findByRole('link', {name: 'My Worklist'}));

    expect(halfViews.open).not.toHaveBeenCalled();
  });
});

describe('halfViewSide', () => {
  const keys = {shiftKey: false, ctrlKey: false, metaKey: false, altKey: false};

  it('takes Shift for the right and Ctrl or Cmd for the left, and nothing else', () => {
    expect(halfViewSide({...keys, shiftKey: true})).toBe('right');
    expect(halfViewSide({...keys, ctrlKey: true})).toBe('left');
    expect(halfViewSide({...keys, metaKey: true})).toBe('left');
    expect(halfViewSide(keys)).toBeNull();
    expect(halfViewSide({...keys, altKey: true})).toBeNull();
    expect(halfViewSide({...keys, shiftKey: true, altKey: true})).toBeNull();
    expect(halfViewSide({...keys, shiftKey: true, ctrlKey: true})).toBeNull();
  });
});

describe('halfViewHref', () => {
  const element = (html: string) => {
    const host = document.createElement('div');
    host.innerHTML = html;
    return host.querySelector('span') ?? host.firstElementChild;
  };

  it('takes an in-app link, from anything inside it', () => {
    expect(halfViewHref(element('<a href="/patients"><span>Patients</span></a>'))).toBe('/patients');
    expect(halfViewHref(element('<a href="/pathways?status=Blocked" target="_self">Pathways</a>'))).toBe('/pathways?status=Blocked');
  });

  it('leaves external links, downloads, other tabs, disabled links and non-links alone', () => {
    for (const html of [
      '<a href="https://example.org/">External</a>',
      '<a href="//example.org/">Protocol-relative</a>',
      '<a href="/\\example.org/">Backslash, which browsers read as protocol-relative</a>',
      '<a href="mailto:someone@example.org">Email</a>',
      '<a href="#main">Skip</a>',
      '<a href="/api/files/1" download>Download</a>',
      '<a href="/patients" target="_blank">New tab</a>',
      '<a href="/patients" aria-disabled="true">Disabled</a>',
      '<button>Save</button>',
    ])
      expect(halfViewHref(element(html)), html).toBeNull();
    expect(halfViewHref(null)).toBeNull();
  });

  it('leaves a control inside a link (a button or a field) its own Shift- or Ctrl-click', () => {
    const host = document.createElement('div');
    host.innerHTML = '<a href="/patients"><button>Pin</button><input aria-label="Note"></a>';
    expect(halfViewHref(host.querySelector('button'))).toBeNull();
    expect(halfViewHref(host.querySelector('input'))).toBeNull();
    expect(halfViewHref(host.querySelector('a'))).toBe('/patients');
  });

  it("takes the router's base path off (the light demo's)", () => {
    expect(halfViewHref(element('<a href="/MedApp/patients">Patients</a>'), '/MedApp/')).toBe('/patients');
    expect(halfViewHref(element('<a href="/MedApp">Home</a>'), '/MedApp/')).toBe('/');
    expect(halfViewHref(element('<a href="/patients">Patients</a>'), '/MedApp/')).toBe('/patients');
  });
});

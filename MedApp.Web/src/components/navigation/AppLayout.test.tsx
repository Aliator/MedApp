import {screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {adminUser, isDisabled, json, mockFetch, pageOf, renderApp} from '../../test/render';

/** Every list is empty: enough for the pages to render their headers. */
const emptyApi = () => mockFetch(() => json(pageOf([])));

// The whole app renders, and its first route chunk can be slow to load.
const heading = (name: string) => screen.findByRole('heading', {name, level: 1}, {timeout: 15000});

/** A nav item's half-view button, shown while its row is hovered. */
async function halfViewButton(user: ReturnType<typeof userEvent.setup>, item: string, name = `Open ${item} in half view`) {
  await user.hover(within(screen.getByRole('navigation')).getByRole('link', {name: item}));
  return screen.findByRole('button', {name});
}

describe('AppLayout half views', () => {
  beforeEach(() => sessionStorage.clear());

  it("opens a nav item's page beside the current one, folding the nav away", async () => {
    emptyApi();
    const {user} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    // The page that's open can't be opened again.
    expect(isDisabled(await halfViewButton(user, 'Patients', 'Patients is already open'))).toBe(true);
    await user.click(await halfViewButton(user, 'Audit Log'));

    expect(await heading('Audit Log')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Patients', level: 1})).toBeInTheDocument();
    expect(screen.getAllByRole('button', {name: 'Close half view'})).toHaveLength(2);
    expect(sessionStorage.getItem('medapp.halfView')).toBe('/admin/audit');
  });

  it('closing the half view leaves the current page on its own, with the nav open', async () => {
    emptyApi();
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    await user.click(await halfViewButton(user, 'Audit Log'));
    await heading('Audit Log');
    const closes = screen.getAllByRole('button', {name: 'Close half view'});
    await user.click(closes[closes.length - 1]);

    await waitFor(() => expect(screen.queryByRole('heading', {name: 'Audit Log', level: 1})).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe('/patients');
    expect(screen.queryByRole('button', {name: 'Close half view'})).not.toBeInTheDocument();
    expect(await halfViewButton(user, 'Home')).toBeInTheDocument();
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it("closing the current page's half opens the half view's page on its own", async () => {
    emptyApi();
    const {user, router} = renderApp({path: '/patients', user: adminUser});

    await heading('Patients');
    await user.click(await halfViewButton(user, 'Audit Log'));
    await heading('Audit Log');
    const patientsHeader = screen.getByRole('heading', {name: 'Patients', level: 1}).parentElement!.parentElement!;
    await user.click(within(patientsHeader).getByRole('button', {name: 'Close half view'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/audit'));
    expect(await heading('Audit Log')).toBeInTheDocument();
    expect(screen.queryByRole('heading', {name: 'Patients', level: 1})).not.toBeInTheDocument();
  });

  it('keeps the half view open across a reload', async () => {
    emptyApi();
    sessionStorage.setItem('medapp.halfView', '/admin/templates');
    renderApp({path: '/patients', user: adminUser});

    expect(await heading('Templates')).toBeInTheDocument();
    expect(await heading('Patients')).toBeInTheDocument();
  });
});

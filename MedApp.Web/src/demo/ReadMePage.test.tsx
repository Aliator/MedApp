import {screen, waitFor} from '@testing-library/react';
import {NotepadProvider, useNotepad} from '../components/notepad/NotepadContext';
import {HalfViewsContext, InHalfPaneContext, type HalfViews} from '../components/pages/halfView';
import {renderWithApp} from '../test/render';
import {asCurrentUser, startLightDemo} from '../test/lightDemo';
import {ADMIN, STAFF} from './accounts';
import {PATHWAYS, PATIENTS} from './ids';
import {ReadMePage} from './ReadMePage';
import {hasSeenReadMe} from './readMeSeen';
import {demoNavigation} from './session';
import {db} from './store';

/** Stand-in half views, with this page in the half view (or none). */
const halfViewsWith = (openPath: string | null): HalfViews => ({
  open: vi.fn(),
  openPath,
  openFull: vi.fn(),
  openOnLeft: vi.fn(),
  showOnLeft: vi.fn(),
  close: vi.fn(),
});

describe('ReadMePage', () => {
  // A place kept by one test's button isn't for the next test's Read Me.
  afterEach(() => sessionStorage.removeItem('medapp.readMePlace'));

  it('says what the light version is, the three accounts, the tour and what it leaves out, and counts as seen', async () => {
    startLightDemo(ADMIN);
    renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    expect(await screen.findByRole('heading', {name: 'Read Me', level: 1})).toBeInTheDocument();
    // The demo's banner sits at the top of the Read Me (not above every page).
    expect(screen.getByText('Light demo: synthetic data, kept only in this browser')).toBeInTheDocument();
    expect(screen.getByText(/runs entirely in your browser/)).toBeInTheDocument();
    expect(screen.getByText('Centre administrator (admin): you, now')).toBeInTheDocument();
    expect(screen.getByText('Ravi Aldous (r.aldous)')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Three accounts'})).toBeInTheDocument();
    expect(screen.getByText(`${STAFF.fullName} (${STAFF.username})`)).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: /The relay, 1: consent, then finish Triage/})).toBeInTheDocument();
    // The tour in parts: getting started, the relay, then around the app (no side stories).
    expect(screen.getByRole('heading', {name: 'The relay', level: 3})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Around the app', level: 3})).toBeInTheDocument();
    expect(screen.queryByRole('heading', {name: /Side story/})).not.toBeInTheDocument();
    expect(screen.getByRole('heading', {name: /^1. Two pages side by side/, level: 4})).toBeInTheDocument();
    expect(screen.getByText('Real-time notifications')).toBeInTheDocument();
    // The full app's architecture, in plain words.
    expect(screen.getByText('How the full app is built')).toBeInTheDocument();
    expect(screen.getByText('.NET 10 Web API, Clean Architecture')).toBeInTheDocument();
    expect(hasSeenReadMe()).toBe(true);
  });

  it('opens a step’s page as the account already signed in', async () => {
    startLightDemo(ADMIN);
    const {user, router} = renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the list of pathways'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
  });

  it('switches account first when the step needs the other one', async () => {
    const {state} = startLightDemo(ADMIN);
    const assign = vi.spyOn(demoNavigation, 'assign').mockImplementation(() => {});
    const {user} = renderWithApp(<ReadMePage />, {user: asCurrentUser(ADMIN), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the pathway at Triage'}));

    await waitFor(() => expect(assign).toHaveBeenCalledWith(`/pathways/${PATHWAYS.relay}`));
    expect(state.user).toBe(STAFF.username);
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
  });

  it('explains instead of opening a record that’s gone, and says when a step was done already', async () => {
    startLightDemo(STAFF);
    db().patients = db().patients.filter((p) => p.id !== PATIENTS.samuelOkafor);
    db().pathways.find((p) => p.id === PATHWAYS.relay)!.stages[0].completedAtUtc = new Date().toISOString();
    const {user, router} = renderWithApp(<ReadMePage />, {user: asCurrentUser(STAFF), path: '/read-me'});

    await user.click(await screen.findByRole('button', {name: 'Open the patient to pin'}));
    expect(await screen.findByText(/isn’t in your copy of the demo any more/)).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/read-me');

    await user.click(screen.getByRole('button', {name: 'Open the pathway at Triage'}));
    expect(await screen.findByText(/You've finished her Triage already/)).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${PATHWAYS.relay}`));
  });

  it('numbers the half-view step, and opens Account Settings on the left for its fold setting', async () => {
    startLightDemo(ADMIN);
    const halfViews = halfViewsWith('/patients');
    const {user, router} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <ReadMePage />
      </HalfViewsContext.Provider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    const first = await screen.findByText(/choose Open in half view on the right\. Patients opens on the right/);
    expect(first.closest('ol')).not.toBeNull();
    expect(screen.getByText(/Shift-click Patients in the side nav: it opens on the right again/)).toBeInTheDocument();
    expect(screen.getByText(/Ctrl-click \(⌘-click on a Mac\) Patients: it opens on the left, and this Read Me moves to the right/)).toBeInTheDocument();
    expect(screen.getByText(/expand button \(Expand sidebar\) at the bottom of the icon rail/)).toBeInTheDocument();
    expect(screen.getByText(/This folding is a setting in Account Settings \(Shrink the side nav when two pages are open\), on by default/)).toBeInTheDocument();
    expect(screen.getByText(/carry on with the tour from it/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Open Account Settings'}));

    expect(halfViews.openOnLeft).toHaveBeenCalledWith('/account');
    expect(router.state.location.pathname).toBe('/read-me');
  });

  it('opens a step’s page on the left, moving the Read Me on its own to the right, as Ctrl-click does', async () => {
    startLightDemo(ADMIN);
    const halfViews = halfViewsWith(null);
    const {user, router} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <ReadMePage />
      </HalfViewsContext.Provider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    expect(await screen.findByText(/opens the exact page on the left, keeping this Read Me on the right/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Open the list of pathways'}));
    expect(halfViews.openOnLeft).toHaveBeenCalledWith('/pathways');
    // The Read Me starts again in the right half: it's kept where the reader was, at that step.
    expect(JSON.parse(sessionStorage.getItem('medapp.readMePlace')!)).toMatchObject({position: 'right'});
    await user.click(screen.getByRole('button', {name: 'Start Using MedApp'}));
    expect(halfViews.openOnLeft).toHaveBeenCalledWith('/');
    expect(router.state.location.pathname).toBe('/read-me');
  });

  it('from the Read Me in the right half, opens a step’s page on the left and keeps the Read Me', async () => {
    startLightDemo(ADMIN);
    const halfViews = halfViewsWith('/read-me');
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViews}>
        <InHalfPaneContext.Provider value={true}>
          <ReadMePage />
        </InHalfPaneContext.Provider>
      </HalfViewsContext.Provider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    await user.click(await screen.findByRole('button', {name: 'Open the patient to pin'}));

    expect(halfViews.showOnLeft).toHaveBeenCalledWith(`/patients/${PATIENTS.samuelOkafor}`);
    expect(halfViews.openOnLeft).not.toHaveBeenCalled();
  });

  it('switching account for a step comes back with its page on the left and the Read Me on the right', async () => {
    startLightDemo(ADMIN);
    const assign = vi.spyOn(demoNavigation, 'assign').mockImplementation(() => {});
    const {user} = renderWithApp(
      <HalfViewsContext.Provider value={halfViewsWith('/read-me')}>
        <InHalfPaneContext.Provider value={true}>
          <ReadMePage />
        </InHalfPaneContext.Provider>
      </HalfViewsContext.Provider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    await user.click(await screen.findByRole('button', {name: 'Open the pathway at Triage'}));

    await waitFor(() => expect(assign).toHaveBeenCalledWith(`/pathways/${PATHWAYS.relay}`));
    expect(sessionStorage.getItem('medapp.halfView')).toBe('/read-me');
    // After the reload, the Read Me on the right is put back where the reader was.
    expect(JSON.parse(sessionStorage.getItem('medapp.readMePlace')!)).toMatchObject({position: 'right'});
  });

  it('goes back to where the reader was, once, when a tour button moved it here', async () => {
    startLightDemo(ADMIN);
    sessionStorage.setItem('medapp.readMePlace', JSON.stringify({place: 'pin-patient', offset: 0, position: 'right', at: Date.now()}));
    // jsdom has no layout: here the page itself has more than it shows, so it's what scrolls.
    vi.spyOn(Element.prototype, 'scrollHeight', 'get').mockImplementation(function (this: Element) {
      return this === document.documentElement ? 3000 : 0;
    });
    renderWithApp(
      <HalfViewsContext.Provider value={halfViewsWith('/read-me')}>
        <InHalfPaneContext.Provider value={true}>
          <ReadMePage />
        </InHalfPaneContext.Provider>
      </HalfViewsContext.Provider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    await screen.findByRole('button', {name: 'Open the patient to pin'});
    await waitFor(() => expect(sessionStorage.getItem('medapp.readMePlace')).toBeNull());
  });

  it('opens the notepad beside the Read Me, staying on it, and says Shift-click opens it in the half view', async () => {
    startLightDemo(ADMIN);
    function NotepadShown() {
      return <p>{useNotepad()!.isOpen ? 'notepad open' : 'notepad closed'}</p>;
    }
    const {user, router} = renderWithApp(
      <NotepadProvider>
        <ReadMePage />
        <NotepadShown />
      </NotepadProvider>,
      {user: asCurrentUser(ADMIN), path: '/read-me'},
    );

    expect(await screen.findByText(/Shift-click Notepad in the side nav to open it in the half view on the right/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Open the notepad'}));

    expect(await screen.findByText('notepad open')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/read-me');
  });

  it('shows the notepad step’s sample notes as text, and copies them', async () => {
    startLightDemo(STAFF);
    const {user} = renderWithApp(<ReadMePage />, {user: asCurrentUser(STAFF), path: '/read-me'});

    expect(await screen.findByText('Skin scraping taken and sent to the lab.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Copy the Notes'}));

    expect(await screen.findByRole('button', {name: 'Copied'})).toBeInTheDocument();
    const notes = [
      'Seen yesterday: scaly patch on the left forearm, about 3 cm, itchy for two months.',
      'Skin scraping taken and sent to the lab.',
      'To go through the results at the results appointment.',
    ];
    expect(await navigator.clipboard.readText()).toBe(notes.join('\n'));
  });
});

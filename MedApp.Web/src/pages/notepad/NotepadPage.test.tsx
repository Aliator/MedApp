import {screen, waitFor, within} from '@testing-library/react';
import {useEffect} from 'react';
import type {Notepad} from '../../api/notepad';
import type {Pathway} from '../../api/pathways';
import {NOTEPAD_OPEN_KEY, NotepadProvider, useNotepad, useOfferToNotepad} from '../../components/notepad/NotepadContext';
import {HalfViewContext, InHalfPaneContext} from '../../components/pages/halfView';
import {
  isDisabled,
  json,
  mockApi,
  mockFetch,
  mustChangePasswordUser,
  openDialog,
  pageOf,
  renderApp,
  renderWithApp,
  signedOutUser,
  staffUser,
} from '../../test/render';
import {setRichText} from '../../test/richText';
import {NotepadPage} from './NotepadPage';

const NOTEPAD = '/api/me/notepad';
const notepad = (overrides: Partial<Notepad> = {}): Notepad => ({
  text: 'Synthetic jotting: ring the ward at 2pm.',
  lastUpdatedUtc: '2026-11-17T10:32:00Z',
  version: 3,
  keepDays: 30,
  ...overrides,
});

const pathway = (id: string, title: string): Pathway => ({
  id,
  title,
  patientId: `patient-${id}`,
  patientName: `Synthetic Person ${id}`,
  patient: {id: `patient-${id}`, firstName: 'Freya', lastName: `Ahmed${id}`, dateOfBirth: '1976-12-01', nhsNumber: '9990000018'},
  canAddNotes: true,
  noteList: [],
});

const putBodies = async (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT').map((r) => r.clone().json()));

describe('NotepadPage in a half view', () => {
  afterEach(() => localStorage.clear());

  /** Whether the signed-in layout's (docked) notepad is open. */
  function DockedShown() {
    return <p>{useNotepad()!.isOpen ? 'docked open' : 'docked closed'}</p>;
  }

  /** A pathway page on the left, offering itself to the signed-in layout's notepad as the pathway page does. */
  function PathwayOnLeft({pathway: offered}: {pathway: Pathway}) {
    return (
      <HalfViewContext.Provider value={{isHalf: true, side: 'left'}}>
        <Offer pathway={offered} />
      </HalfViewContext.Provider>
    );
  }
  function Offer({pathway: offered}: {pathway: Pathway}) {
    useOfferToNotepad(offered);
    return null;
  }

  /** Opens the docked notepad first, as someone might have before opening it in the half view. */
  function DockedOpen() {
    const docked = useNotepad()!;
    const {open} = docked;
    useEffect(() => open(), [open]);
    return null;
  }

  /** The page in the half view's router, inside the signed-in layout's notepad, as `AppLayout` puts it. */
  function renderInHalf({onClose = vi.fn(), left, isDockedOpen = false}: {onClose?: () => void; left?: Pathway; isDockedOpen?: boolean} = {}) {
    const result = renderWithApp(
      <NotepadProvider>
        {isDockedOpen && <DockedOpen />}
        <DockedShown />
        {left && <PathwayOnLeft pathway={left} />}
        <InHalfPaneContext.Provider value={true}>
          <HalfViewContext.Provider value={{isHalf: true, side: 'right', onClose}}>
            <NotepadPage />
          </HalfViewContext.Provider>
        </InHalfPaneContext.Provider>
      </NotepadProvider>,
      {user: staffUser, strict: true},
    );
    return {...result, onClose};
  }

  it("is a region in its half, with the focus in its box: no main landmark of its own, and the tab keeps the main page's name", async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    document.title = 'Patients - MedApp';
    renderInHalf();

    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(screen.getByRole('region', {name: 'Notepad'})).toContainElement(box);
    expect(screen.queryByRole('main')).not.toBeInTheDocument();
    expect(document.title).toBe('Patients - MedApp');
    await waitFor(() => expect(box).toHaveFocus());
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBeNull();
  });

  it('saves, then closes its half', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic, typed in the half view.', version: 4})),
    });
    const {user, onClose} = renderInHalf();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, typed in the half view.');

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed in the half view.', expectedVersion: 3}]);
  });

  it('stays open when that save fails, so nothing typed is lost', async () => {
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => new Response(null, {status: 500}),
    });
    const {user, onClose} = renderInHalf();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, unsaved.');

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    expect(await screen.findByRole('button', {name: 'Try Again'})).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes the docked notepad as it opens, so it is not open twice side by side', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderInHalf({isDockedOpen: true});

    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('docked closed')).toBeInTheDocument());
  });

  it("keeps the focus in its box as the docked notepad closes, rather than the docked one's focus return taking it", async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderWithApp(
      <NotepadProvider>
        <DockedOpen />
        <main>
          <h1>Patients</h1>
        </main>
        <section aria-label="Right half">
          <InHalfPaneContext.Provider value={true}>
            <HalfViewContext.Provider value={{isHalf: true, side: 'right', onClose: vi.fn()}}>
              <NotepadPage />
            </HalfViewContext.Provider>
          </InHalfPaneContext.Provider>
        </section>
      </NotepadProvider>,
      {user: staffUser, strict: true},
    );

    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    await waitFor(() => expect(box).toHaveFocus());
    // Past the docked notepad's second focus return (after its sheet's closing animation).
    await new Promise((resolve) => setTimeout(resolve, 700));
    expect(box).toHaveFocus();
  });

  it("adds a note to the pathway page beside it (the signed-in layout's), after the same confirmation", async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: '', version: 4})),
      'POST /api/pathways/patient-pathways/p1/notes': () => json({id: 'n1', text: 'x', version: 1}, 201),
    });
    const {user} = renderInHalf({left: pathway('p1', 'Synthetic knee review')});
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Pathway'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmedp1, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Synthetic knee review')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(new URL(post.url).pathname).toBe('/api/pathways/patient-pathways/p1/notes');
  });

  it('offers nothing to add to with no pathway page beside it', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderInHalf();
    await screen.findByRole('textbox', {name: 'Notepad'});

    expect(isDisabled(screen.getByRole('button', {name: 'Add Note to Pathway'}))).toBe(true);
  });
});

describe('NotepadPage on its own', () => {
  afterEach(() => localStorage.clear());

  it('goes to the start page in place of this address, with the notepad docked there, under StrictMode', async () => {
    const {router} = renderWithApp(<NotepadPage />, {path: '/notepad', user: staffUser, strict: true});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBe('true');
    // Replaced, not added: Back doesn't come here again.
    expect(router.history.length).toBe(1);
  });
});

describe('the /notepad route', {timeout: 20_000}, () => {
  afterEach(() => localStorage.clear());

  it('takes someone signed in to the start page, with the notepad docked', async () => {
    mockFetch((request) => {
      const {pathname} = new URL(request.url);
      if (pathname === NOTEPAD) return json(notepad());
      if (pathname === '/api/auth/me/settings') return json({settings: null});
      return json(/\/(templates|definitions|roles|teams)$/.test(pathname) ? [] : pageOf([]));
    });
    const {router} = renderApp({path: '/notepad', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/'), {timeout: 15000});
    expect(await screen.findByRole('textbox', {name: 'Notepad'}, {timeout: 15000})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Notepad', pressed: true})).toBeInTheDocument();
  });

  it('sends someone signed out to sign in', async () => {
    mockApi({});
    const {router} = renderApp({path: '/notepad', user: signedOutUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBeNull();
  });

  it('keeps an account held to changing its password on that', async () => {
    mockApi({'GET /api/auth/me/settings': () => json({settings: null})});
    const {router} = renderApp({path: '/notepad', user: mustChangePasswordUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
  });
});

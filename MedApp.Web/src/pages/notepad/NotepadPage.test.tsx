import {screen, waitFor, within} from '@testing-library/react';
import {useEffect} from 'react';
import type {Notepad} from '../../api/notepad';
import type {Procedure} from '../../api/procedures';
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

const procedure = (id: string, title: string): Procedure => ({
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

  /** A procedure page on the left, offering itself to the signed-in layout's notepad as the procedure page does. */
  function ProcedureOnLeft({procedure: offered}: {procedure: Procedure}) {
    return (
      <HalfViewContext.Provider value={{isHalf: true, side: 'left'}}>
        <Offer procedure={offered} />
      </HalfViewContext.Provider>
    );
  }
  function Offer({procedure: offered}: {procedure: Procedure}) {
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
  function renderInHalf({onClose = vi.fn(), left, isDockedOpen = false}: {onClose?: () => void; left?: Procedure; isDockedOpen?: boolean} = {}) {
    const result = renderWithApp(
      <NotepadProvider>
        {isDockedOpen && <DockedOpen />}
        <DockedShown />
        {left && <ProcedureOnLeft procedure={left} />}
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

    expect(await screen.findByRole('button', {name: 'Retry'})).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes the docked notepad as it opens, so it is not open twice side by side', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderInHalf({isDockedOpen: true});

    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('docked closed')).toBeInTheDocument());
  });

  it("adds a note to the procedure page beside it (the signed-in layout's), after the same confirmation", async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: '', version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', text: 'x', version: 1}, 201),
    });
    const {user} = renderInHalf({left: procedure('p1', 'Synthetic knee review')});
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmedp1, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Synthetic knee review')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(new URL(post.url).pathname).toBe('/api/procedures/patient-procedures/p1/notes');
  });

  it('offers nothing to add to with no procedure page beside it', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderInHalf();
    await screen.findByRole('textbox', {name: 'Notepad'});

    expect(isDisabled(screen.getByRole('button', {name: 'Add Note to Procedure'}))).toBe(true);
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
      return json(/\/(templates|definitions|roles)$/.test(pathname) ? [] : pageOf([]));
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

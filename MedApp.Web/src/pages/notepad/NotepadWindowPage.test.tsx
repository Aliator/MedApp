import {fireEvent, screen, waitFor} from '@testing-library/react';
import type {Notepad} from '../../api/notepad';
import {NOTEPAD_OPEN_KEY, NotepadProvider, useNotepad} from '../../components/notepad/NotepadContext';
import {closeNotepadWindow} from '../../components/notepad/notepadWindow';
import {HalfViewContext, InHalfPaneContext} from '../../components/pages/halfView';
import {json, mockApi, mustChangePasswordUser, renderApp, renderWithApp, signedOutUser, staffUser} from '../../test/render';
import {setRichText} from '../../test/richText';
import {NotepadWindowPage} from './NotepadWindowPage';

// The window itself can't be closed in a test: closing is checked as a call.
vi.mock('../../components/notepad/notepadWindow', async (original) => ({
  ...(await original<typeof import('../../components/notepad/notepadWindow')>()),
  closeNotepadWindow: vi.fn(),
}));

const NOTEPAD = '/api/me/notepad';
const notepad = (overrides: Partial<Notepad> = {}): Notepad => ({
  text: 'Synthetic jotting: ring the ward at 2pm.',
  lastUpdatedUtc: '2026-11-17T10:32:00Z',
  version: 3,
  keepDays: 30,
  ...overrides,
});

const putBodies = async (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT').map((r) => r.clone().json()));

const beforeUnload = () => {
  const event = new Event('beforeunload', {cancelable: true});
  window.dispatchEvent(event);
  return event.defaultPrevented;
};

describe('NotepadWindowPage', () => {
  it('is the notepad on its own, named Notepad in the browser tab, with the focus in its box', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderWithApp(<NotepadWindowPage />, {user: staffUser});

    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(screen.getByRole('main')).toContainElement(box);
    expect(screen.getByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Notepad - MedApp'));
    await waitFor(() => expect(box).toHaveFocus());
    // No side nav in it.
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('saves before it closes the window', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic, typed before closing.', version: 4})),
    });
    const {user} = renderWithApp(<NotepadWindowPage />, {user: staffUser});
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, typed before closing.');

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    await waitFor(() => expect(closeNotepadWindow).toHaveBeenCalledTimes(1));
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed before closing.', expectedVersion: 3}]);
  });

  it('stays open when that save fails, so nothing typed is lost, and warns before the browser closes it', async () => {
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => new Response(null, {status: 500}),
    });
    const {user} = renderWithApp(<NotepadWindowPage />, {user: staffUser});
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, unsaved.');
    expect(beforeUnload()).toBe(true);

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    expect(await screen.findByRole('button', {name: 'Retry'})).toBeInTheDocument();
    expect(closeNotepadWindow).not.toHaveBeenCalled();
    expect(beforeUnload()).toBe(true);
  });

  it('saves on Ctrl+S, after which the browser closes it without asking', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic, saved by shortcut.', version: 4})),
    });
    renderWithApp(<NotepadWindowPage />, {user: staffUser});
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await setRichText(box, 'Synthetic, saved by shortcut.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});

    await waitFor(async () => expect(await putBodies(fetch)).toHaveLength(1));
    await screen.findByText(/^Saved /);
    expect(beforeUnload()).toBe(false);
  });
});

describe('NotepadWindowPage in a half view', () => {
  /** Whether the signed-in layout's (docked) notepad is open. */
  function DockedShown() {
    return <p>{useNotepad()!.isOpen ? 'docked open' : 'docked closed'}</p>;
  }

  /** The page in the half view's router, inside the signed-in layout's notepad, as `AppLayout` puts it. */
  function renderInHalf(onClose = vi.fn()) {
    const result = renderWithApp(
      <NotepadProvider>
        <DockedShown />
        <InHalfPaneContext.Provider value={true}>
          <HalfViewContext.Provider value={{isHalf: true, side: 'right', onClose}}>
            <NotepadWindowPage />
          </HalfViewContext.Provider>
        </InHalfPaneContext.Provider>
      </NotepadProvider>,
      {user: staffUser},
    );
    return {...result, onClose};
  }

  it("is a region in its half: no main landmark of its own, and the browser tab keeps the main page's name", async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    document.title = 'Patients - MedApp';
    renderInHalf();

    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    expect(screen.queryByRole('main')).not.toBeInTheDocument();
    expect(document.title).toBe('Patients - MedApp');
  });

  it('saves, then closes its half rather than the window', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic, typed in the half view.', version: 4})),
    });
    const {user, onClose} = renderInHalf();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, typed in the half view.');

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(closeNotepadWindow).not.toHaveBeenCalled();
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed in the half view.', expectedVersion: 3}]);
  });

  it('closes the docked notepad as it opens, so it is not open twice side by side', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    localStorage.setItem(NOTEPAD_OPEN_KEY, 'true');
    renderInHalf();

    expect(await screen.findByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('docked closed')).toBeInTheDocument());
  });
});

describe('the /notepad route', () => {
  it('opens the notepad for someone signed in', async () => {
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      'GET /api/auth/me/settings': () => json({settings: null}),
    });
    renderApp({path: '/notepad', user: staffUser});

    expect(await screen.findByRole('textbox', {name: 'Notepad'})).toBeInTheDocument();
  });

  it('sends someone signed out to sign in', async () => {
    mockApi({});
    const {router} = renderApp({path: '/notepad', user: signedOutUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it('keeps an account held to changing its password on that', async () => {
    mockApi({'GET /api/auth/me/settings': () => json({settings: null})});
    const {router} = renderApp({path: '/notepad', user: mustChangePasswordUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'));
  });
});

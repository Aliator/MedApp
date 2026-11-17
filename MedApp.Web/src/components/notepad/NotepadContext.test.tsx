import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {act, render, renderHook, screen, waitFor} from '@testing-library/react';
import {StrictMode, useState, type ReactNode} from 'react';
import type {Notepad} from '../../api/notepad';
import type {Procedure} from '../../api/procedures';
import {otherWindow} from '../../test/broadcastChannel';
import {HalfViewContext} from '../pages/halfView';
import {NOTEPAD_OPEN_KEY, NotepadProvider, useNotepad, useNotepadDocked, useOfferToNotepad, type OpenProcedure} from './NotepadContext';
import {closeNotepadWindow} from './notepadWindow';

// Closing the notepad's own window: checked here as a call (the window itself can't be closed in a test).
vi.mock('./notepadWindow', async (original) => ({...(await original<typeof import('./notepadWindow')>()), closeNotepadWindow: vi.fn()}));

let queryClient = new QueryClient();
beforeEach(() => {
  queryClient = new QueryClient();
});

const wrapper = ({children}: {children: ReactNode}) => (
  <QueryClientProvider client={queryClient}>
    <NotepadProvider>{children}</NotepadProvider>
  </QueryClientProvider>
);

const procedure = {id: 'p1', title: 'Synthetic knee review'} as Procedure;

describe('NotepadProvider', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('starts closed, and remembers being open in this browser', async () => {
    const {result} = renderHook(() => useNotepad()!, {wrapper});
    expect(result.current.isOpen).toBe(false);

    act(() => result.current.open());
    expect(result.current.isOpen).toBe(true);
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBe('true');

    act(() => result.current.close());
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBeNull();
  });

  it('opens again after a reload when it was open', async () => {
    localStorage.setItem(NOTEPAD_OPEN_KEY, 'true');
    const {result} = renderHook(() => useNotepad()!, {wrapper});

    await waitFor(() => expect(result.current.isOpen).toBe(true));
  });

  it('starts closed when the browser has no storage', () => {
    vi.stubGlobal('localStorage', {
      getItem: () => {
        throw new Error('blocked');
      },
      setItem: () => {
        throw new Error('blocked');
      },
      removeItem: () => {
        throw new Error('blocked');
      },
    });
    const {result} = renderHook(() => useNotepad()!, {wrapper});

    act(() => result.current.open());
    expect(result.current.isOpen).toBe(true);
  });

  it('closes through the pane when it has a closer (which saves first), otherwise at once', async () => {
    const {result} = renderHook(() => useNotepad()!, {wrapper});
    act(() => result.current.open());
    act(() => result.current.requestClose());
    expect(result.current.isOpen).toBe(false);

    act(() => result.current.open());
    const closer = vi.fn(async () => {});
    let unregister = () => {};
    act(() => {
      unregister = result.current.registerCloser(closer);
    });
    act(() => result.current.requestClose());
    expect(closer).toHaveBeenCalledTimes(1);
    expect(result.current.isOpen).toBe(true);

    act(() => unregister());
    act(() => result.current.requestClose());
    expect(result.current.isOpen).toBe(false);
  });

  it('takes the focus once when its user opens it, never when it reopens as a page loads', () => {
    localStorage.setItem(NOTEPAD_OPEN_KEY, 'true');
    const {result} = renderHook(() => useNotepad()!, {wrapper});
    expect(result.current.takeFocus()).toBe(false);

    act(() => result.current.open());
    expect(result.current.takeFocus()).toBe(true);
    expect(result.current.takeFocus()).toBe(false);
  });

  it("gives the focus back to what opened it, or, when that's gone (a phone's closed nav drawer), the page's title", async () => {
    function Harness() {
      const notepad = useNotepad()!;
      const [hasOpener, setHasOpener] = useState(true);
      return (
        <>
          {hasOpener && <button onClick={notepad.open}>Opener</button>}
          <button onClick={() => setHasOpener(false)}>Hide opener</button>
          <button onClick={notepad.close}>Close it</button>
          <main>
            <h1>Synthetic page</h1>
          </main>
        </>
      );
    }
    render(<Harness />, {wrapper});

    act(() => screen.getByRole('button', {name: 'Opener'}).focus());
    act(() => screen.getByRole('button', {name: 'Opener'}).click());
    act(() => (document.activeElement as HTMLElement).blur());
    act(() => screen.getByRole('button', {name: 'Close it'}).click());
    act(() => (document.activeElement as HTMLElement).blur());
    await waitFor(() => expect(screen.getByRole('button', {name: 'Opener'})).toHaveFocus());

    // The opener goes (the nav drawer closed): the page's title takes it, from code only.
    act(() => screen.getByRole('button', {name: 'Opener'}).click());
    act(() => screen.getByRole('button', {name: 'Hide opener'}).click());
    act(() => screen.getByRole('button', {name: 'Close it'}).click());
    act(() => (document.activeElement as HTMLElement).blur());
    const title = screen.getByRole('heading', {name: 'Synthetic page'});
    await waitFor(() => expect(title).toHaveFocus());
    expect(title).toHaveAttribute('tabindex', '-1');
  });

  it('lists the procedure pages open, the left half before the right, and forgets one when it closes', () => {
    // As a page holds it: the same object from render to render (a query's data).
    const shown = {left: {...procedure, id: 'left'}, right: {...procedure, id: 'right'}};
    function Page({side}: {side: 'left' | 'right'}) {
      useOfferToNotepad(shown[side]);
      return null;
    }
    function Halves({showLeft}: {showLeft: boolean}) {
      return (
        <>
          <HalfViewContext.Provider value={{isHalf: true, side: 'right'}}>
            <Page side="right" />
          </HalfViewContext.Provider>
          {showLeft && (
            <HalfViewContext.Provider value={{isHalf: true, side: 'left'}}>
              <Page side="left" />
            </HalfViewContext.Provider>
          )}
          <Listed />
        </>
      );
    }
    function Listed() {
      return <p>{useNotepad()!.openProcedures.map((p) => `${p.side}:${p.procedure.id}`).join(',')}</p>;
    }

    const {rerender} = render(<Halves showLeft />, {wrapper});
    expect(screen.getByText('left:left,right:right')).toBeInTheDocument();

    rerender(<Halves showLeft={false} />);
    expect(screen.getByText('right:right')).toBeInTheDocument();
  });

  it('offers nothing outside the notepad, or while the procedure loads', () => {
    expect(() => renderHook(() => useOfferToNotepad(procedure))).not.toThrow();
    const {result} = renderHook(
      () => {
        useOfferToNotepad(undefined);
        return useNotepad()!;
      },
      {wrapper},
    );
    expect(result.current.openProcedures).toEqual([]);
  });

  it('is docked when open on a wide window, and a sheet (not docked) on a narrow one', () => {
    const {result} = renderHook(() => ({notepad: useNotepad()!, isDocked: useNotepadDocked()}), {wrapper});
    expect(result.current.isDocked).toBe(false);

    act(() => result.current.notepad.open());
    expect(result.current.isDocked).toBe(true);
  });
});

describe("NotepadProvider and MedApp's other windows", () => {
  afterEach(() => localStorage.clear());

  type Shared = {type: 'procedures'; windowId: string; procedures: OpenProcedure[]};
  const shares = (received: unknown[]) => received.filter((m) => (m as {type: string}).type === 'procedures') as Shared[];
  const listed = (procedures: OpenProcedure[]) => procedures.map((p) => `${p.key}=${p.procedure.id}`);
  const p1 = {id: 'p1', title: 'Synthetic knee review', patientName: 'Synthetic Person 1'} as Procedure;
  const p2 = {id: 'p2', title: 'Synthetic hip review', patientName: 'Synthetic Person 2'} as Procedure;

  function Offering({procedure: offered}: {procedure: Procedure}) {
    useOfferToNotepad(offered);
    return null;
  }
  function Listed() {
    return <p data-testid="listed">{listed(useNotepad()!.openProcedures).join(',')}</p>;
  }
  const windowWrapper = ({children}: {children: ReactNode}) => (
    <QueryClientProvider client={queryClient}>
      <NotepadProvider isWindow>{children}</NotepadProvider>
    </QueryClientProvider>
  );

  it('shares its procedure pages when they change and when asked, as one window, and none as the window goes', async () => {
    const other = otherWindow();
    const {rerender} = render(<Offering procedure={p1} />, {wrapper});

    await waitFor(() => expect(shares(other.received).at(-1)?.procedures.map((p) => p.procedure.id)).toEqual(['p1']));
    const {windowId} = shares(other.received).at(-1)!;

    rerender(<Offering procedure={p2} />);
    await waitFor(() => expect(shares(other.received).at(-1)?.procedures.map((p) => p.procedure.id)).toEqual(['p2']));

    const before = shares(other.received).length;
    other.post({type: 'ask-procedures'});
    await waitFor(() => expect(shares(other.received)).toHaveLength(before + 1));
    expect(shares(other.received).at(-1)!.procedures.map((p) => p.procedure.id)).toEqual(['p2']);

    act(() => void window.dispatchEvent(new Event('pagehide')));
    await waitFor(() => expect(shares(other.received).at(-1)).toEqual({type: 'procedures', windowId, procedures: []}));
    expect(new Set(shares(other.received).map((m) => m.windowId))).toEqual(new Set([windowId]));
  });

  it("keeps one window's ID however often its effects run (StrictMode)", async () => {
    const other = otherWindow();
    render(
      <StrictMode>
        <Offering procedure={p1} />
      </StrictMode>,
      {wrapper},
    );

    await waitFor(() => expect(shares(other.received).at(-1)?.procedures.map((p) => p.procedure.id)).toEqual(['p1']));
    expect(new Set(shares(other.received).map((m) => m.windowId)).size).toBe(1);
  });

  it("in the notepad's own window: is open, asks for the procedure pages, and offers every window's, each procedure once", async () => {
    const other = otherWindow();
    const {result} = renderHook(() => useNotepad()!, {wrapper: windowWrapper});
    expect(result.current.isOpen).toBe(true);
    expect(result.current.isWindow).toBe(true);

    await waitFor(() => expect(other.received).toContainEqual({type: 'ask-procedures'}));
    other.post({type: 'procedures', windowId: 'w1', procedures: [{key: 'a', procedure: p1}]});
    other.post({type: 'procedures', windowId: 'w2', procedures: [{key: 'a', procedure: p1}, {key: 'b', side: 'right', procedure: p2}]});
    await waitFor(() => expect(listed(result.current.openProcedures)).toEqual(['w1:a=p1', 'w2:b=p2']));

    // The first window goes: the procedure is still open in the second.
    other.post({type: 'procedures', windowId: 'w1', procedures: []});
    await waitFor(() => expect(listed(result.current.openProcedures)).toEqual(['w2:a=p1', 'w2:b=p2']));
  });

  it("in the notepad's own window, under StrictMode: still asks, and lists what it's told", async () => {
    const other = otherWindow();
    render(
      <StrictMode>
        <Listed />
      </StrictMode>,
      {wrapper: windowWrapper},
    );

    await waitFor(() => expect(other.received).toContainEqual({type: 'ask-procedures'}));
    other.post({type: 'procedures', windowId: 'w1', procedures: [{key: 'a', procedure: p1}]});
    await waitFor(() => expect(screen.getByTestId('listed')).toHaveTextContent('w1:a=p1'));
  });

  it("in the notepad's own window: takes the focus as it starts, keeps nothing in this browser, and closing closes the window", () => {
    const {result} = renderHook(() => useNotepad()!, {wrapper: windowWrapper});
    expect(result.current.takeFocus()).toBe(true);
    expect(result.current.takeFocus()).toBe(false);

    act(() => result.current.open());
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBeNull();

    act(() => result.current.requestClose());
    expect(closeNotepadWindow).toHaveBeenCalledTimes(1);
    expect(result.current.isOpen).toBe(true);
  });

  it('reads a procedure afresh when a note was added to it from another window', async () => {
    const other = otherWindow();
    queryClient.setQueryData(['procedures', 'p1'], p1);
    render(<Listed />, {wrapper});

    other.post({type: 'note-added', procedureId: 'p1'});

    await waitFor(() => expect(queryClient.getQueryState(['procedures', 'p1'])?.isInvalidated).toBe(true));
  });

  it('marks an older copy of the notepad out of date when another window saves, and tells the pane the version', async () => {
    const other = otherWindow();
    queryClient.setQueryData(['notepad'], {text: 'Synthetic.', version: 3} satisfies Notepad);
    const {result} = renderHook(() => useNotepad()!, {wrapper});
    const listener = vi.fn();
    act(() => void result.current.onSavedElsewhere(listener));

    other.post({type: 'saved', version: 3});
    await waitFor(() => expect(listener).toHaveBeenCalledWith(3));
    expect(queryClient.getQueryState(['notepad'])?.isInvalidated).toBe(false);

    other.post({type: 'saved', version: 4});
    await waitFor(() => expect(listener).toHaveBeenCalledWith(4));
    expect(queryClient.getQueryState(['notepad'])?.isInvalidated).toBe(true);
  });

  it('tells the other windows about its saves and the notes added from it', async () => {
    const other = otherWindow();
    const {result} = renderHook(() => useNotepad()!, {wrapper});

    act(() => result.current.shareSaved(7));
    act(() => result.current.shareNoteAdded('p1'));

    await waitFor(() =>
      expect(other.received).toEqual(expect.arrayContaining([{type: 'saved', version: 7}, {type: 'note-added', procedureId: 'p1'}])),
    );
  });

  it('works on its own where the browser has no BroadcastChannel', () => {
    vi.stubGlobal('BroadcastChannel', undefined);
    const {result} = renderHook(() => useNotepad()!, {wrapper: windowWrapper});

    expect(() => act(() => result.current.shareSaved(1))).not.toThrow();
    expect(result.current.openProcedures).toEqual([]);
  });
});

import {act, render, renderHook, screen, waitFor} from '@testing-library/react';
import {StrictMode, useState, type ReactNode} from 'react';
import type {Pathway} from '../../api/pathways';
import {HalfViewContext} from '../pages/halfView';
import {NOTEPAD_OPEN_KEY, NotepadProvider, storeNotepadOpen, useNotepad, useNotepadDocked, useOfferToNotepad} from './NotepadContext';

const wrapper = ({children}: {children: ReactNode}) => <NotepadProvider>{children}</NotepadProvider>;

const pathway = {id: 'p1', title: 'Synthetic knee review'} as Pathway;

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

  it('lists the pathway pages open, the left half before the right, and forgets one when it closes', () => {
    // As a page holds it: the same object from render to render (a query's data).
    const shown = {left: {...pathway, id: 'left'}, right: {...pathway, id: 'right'}};
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
      return <p>{useNotepad()!.openPathways.map((p) => `${p.side}:${p.pathway.id}`).join(',')}</p>;
    }

    const {rerender} = render(<Halves showLeft />, {wrapper});
    expect(screen.getByText('left:left,right:right')).toBeInTheDocument();

    rerender(<Halves showLeft={false} />);
    expect(screen.getByText('right:right')).toBeInTheDocument();
  });

  it('offers nothing outside the notepad, or while the pathway loads', () => {
    expect(() => renderHook(() => useOfferToNotepad(pathway))).not.toThrow();
    const {result} = renderHook(
      () => {
        useOfferToNotepad(undefined);
        return useNotepad()!;
      },
      {wrapper},
    );
    expect(result.current.openPathways).toEqual([]);
  });

  it('is docked when open on a wide window, and a sheet (not docked) on a narrow one', () => {
    const {result} = renderHook(() => ({notepad: useNotepad()!, isDocked: useNotepadDocked()}), {wrapper});
    expect(result.current.isDocked).toBe(false);

    act(() => result.current.notepad.open());
    expect(result.current.isDocked).toBe(true);
  });
});

describe('NotepadProvider in a half view', () => {
  afterEach(() => localStorage.clear());

  const p2 = {id: 'p2', title: 'Synthetic hip review'} as Pathway;

  function Offering({pathway: offered, side}: {pathway: Pathway; side?: 'left' | 'right'}) {
    return (
      <HalfViewContext.Provider value={{isHalf: side !== undefined, side}}>
        <OfferOnly pathway={offered} />
      </HalfViewContext.Provider>
    );
  }
  function OfferOnly({pathway: offered}: {pathway: Pathway}) {
    useOfferToNotepad(offered);
    return null;
  }
  function Listed() {
    const notepad = useNotepad()!;
    return <p data-testid="listed">{notepad.openPathways.map((open) => open.pathway.id).join(',')}</p>;
  }

  /** The half view's notepad, inside the signed-in layout's, with that layout's pathway pages offered beside it. */
  function renderInHalf(onCloseHalf = vi.fn(), offered: Pathway[] = [pathway]) {
    const result = render(
      <StrictMode>
        <NotepadProvider>
          {offered.map((p) => (
            <Offering key={p.id} pathway={p} side="left" />
          ))}
          <NotepadProvider onCloseHalf={onCloseHalf}>
            <Listed />
          </NotepadProvider>
        </NotepadProvider>
      </StrictMode>,
    );
    return {...result, onCloseHalf};
  }

  it("offers the signed-in layout's pathway pages, as they change", async () => {
    const {rerender} = renderInHalf();
    await waitFor(() => expect(screen.getByTestId('listed')).toHaveTextContent('p1'));

    rerender(
      <StrictMode>
        <NotepadProvider>
          <Offering pathway={p2} side="left" />
          <NotepadProvider onCloseHalf={vi.fn()}>
            <Listed />
          </NotepadProvider>
        </NotepadProvider>
      </StrictMode>,
    );
    await waitFor(() => expect(screen.getByTestId('listed')).toHaveTextContent(/^p2$/));
  });

  it('is open, takes the focus as it starts, keeps nothing in this browser, and closing closes its half', () => {
    const onCloseHalf = vi.fn();
    const wrapHalf = ({children}: {children: ReactNode}) => <NotepadProvider onCloseHalf={onCloseHalf}>{children}</NotepadProvider>;
    const {result} = renderHook(() => useNotepad()!, {wrapper: wrapHalf});
    expect(result.current.isOpen).toBe(true);
    expect(result.current.takeFocus()).toBe(true);
    expect(result.current.takeFocus()).toBe(false);

    act(() => result.current.open());
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBeNull();

    act(() => result.current.requestClose());
    expect(onCloseHalf).toHaveBeenCalledTimes(1);
    expect(result.current.isOpen).toBe(true);
  });

  it('stays open in its half though the docked notepad was left open in this browser', async () => {
    storeNotepadOpen(true);
    const wrapHalf = ({children}: {children: ReactNode}) => <NotepadProvider onCloseHalf={vi.fn()}>{children}</NotepadProvider>;
    const {result} = renderHook(() => useNotepad()!, {wrapper: wrapHalf});

    act(() => result.current.close());
    expect(result.current.isOpen).toBe(true);
    expect(localStorage.getItem(NOTEPAD_OPEN_KEY)).toBe('true');
  });
});

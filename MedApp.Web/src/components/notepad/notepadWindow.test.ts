import {closeNotepadWindow, NOTEPAD_WINDOW_FEATURES, NOTEPAD_WINDOW_NAME, openNotepadWindow} from './notepadWindow';

/**
 * A window as `window.open` returns it: where it is (or, on another site, unreadable, though it can still be sent
 * somewhere), and what was done to it.
 */
function fakeWindow(location: {origin: string; pathname: string} | 'unreadable') {
  const replace = vi.fn();
  const focus = vi.fn();
  const unreadable = {
    replace,
    get origin(): string {
      throw new DOMException('Blocked a frame from accessing a cross-origin frame.', 'SecurityError');
    },
  };
  const popup = {focus, location: location === 'unreadable' ? unreadable : {...location, replace}};
  return {popup: popup as unknown as Window, replace, focus};
}

describe('openNotepadWindow', () => {
  it('opens a new window, sends it to the notepad, and brings it forward', () => {
    // A new window is blank, on this origin.
    const {popup, replace, focus} = fakeWindow({origin: window.location.origin, pathname: 'blank'});
    const open = vi.spyOn(window, 'open').mockReturnValue(popup);

    expect(openNotepadWindow()).toBe(true);

    expect(open).toHaveBeenCalledWith('', NOTEPAD_WINDOW_NAME, NOTEPAD_WINDOW_FEATURES);
    expect(NOTEPAD_WINDOW_FEATURES).toBe('popup,width=420,height=640');
    expect(replace).toHaveBeenCalledWith(`${window.location.origin}/notepad`);
    expect(focus).toHaveBeenCalled();
  });

  it('brings the window forward as it is when it is open already, without reloading it (which would lose a jotting)', () => {
    const {popup, replace, focus} = fakeWindow({origin: window.location.origin, pathname: '/notepad'});
    vi.spyOn(window, 'open').mockReturnValue(popup);

    expect(openNotepadWindow()).toBe(true);

    expect(replace).not.toHaveBeenCalled();
    expect(focus).toHaveBeenCalled();
  });

  it('sends a window that has gone somewhere else back to the notepad', () => {
    const {popup, replace} = fakeWindow('unreadable');
    vi.spyOn(window, 'open').mockReturnValue(popup);

    expect(openNotepadWindow()).toBe(true);

    expect(replace).toHaveBeenCalledWith(`${window.location.origin}/notepad`);
  });

  it('says so when the browser blocks it', () => {
    vi.spyOn(window, 'open').mockReturnValue(null);

    expect(openNotepadWindow()).toBe(false);
  });
});

describe('closeNotepadWindow', () => {
  it('closes the window', () => {
    vi.useFakeTimers();
    const close = vi.spyOn(window, 'close').mockImplementation(() => {});
    vi.stubGlobal('closed', true);
    const assign = vi.fn();
    vi.stubGlobal('location', {...window.location, assign});

    closeNotepadWindow();
    vi.runAllTimers();

    expect(close).toHaveBeenCalled();
    expect(assign).not.toHaveBeenCalled();
  });

  it("goes to the start page when the browser won't close it (a window MedApp didn't open)", () => {
    vi.useFakeTimers();
    vi.spyOn(window, 'close').mockImplementation(() => {});
    const assign = vi.fn();
    vi.stubGlobal('location', {...window.location, assign});

    closeNotepadWindow();
    expect(assign).not.toHaveBeenCalled();
    vi.runAllTimers();

    expect(assign).toHaveBeenCalledWith('/');
  });
});

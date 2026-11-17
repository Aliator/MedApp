import {screen, waitFor} from '@testing-library/react';
import {useEffect} from 'react';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {NOTEPAD_SHEET_QUERY, NotepadProvider, useNotepad} from './NotepadContext';
import {NotepadSheet} from './NotepadSheet';

/** A window that answers `query` (and only it) as matching. */
function givenWindowMatching(query: string | null) {
  vi.stubGlobal('matchMedia', (media: string) => ({
    matches: media === query,
    media,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }));
}

function Opened() {
  const notepad = useNotepad()!;
  useEffect(() => notepad.open(), [notepad]);
  return null;
}

function renderSheet() {
  mockApi({'GET /api/me/notepad': () => json({text: 'Synthetic jotting.', version: 1, keepDays: 30})});
  return renderWithApp(
    <NotepadProvider>
      <Opened />
      <NotepadSheet />
    </NotepadProvider>,
    {user: staffUser},
  );
}

describe('NotepadSheet', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('shows the open notepad as a full-screen sheet on a narrow window', async () => {
    givenWindowMatching(NOTEPAD_SHEET_QUERY);
    renderSheet();

    expect(await screen.findByRole('textbox', {name: 'Notepad'})).toBeInTheDocument();
    expect(document.querySelector('dialog[open]')).not.toBeNull();
  });

  it('shows nothing on a wide window, where the layout docks it instead', async () => {
    givenWindowMatching(null);
    renderSheet();

    await waitFor(() => expect(localStorage.getItem('medapp.notepadOpen')).toBe('true'));
    expect(screen.queryByRole('textbox', {name: 'Notepad'})).not.toBeInTheDocument();
  });
});

import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {flushSettings} from '../../app/settingsStore';
import {$setSelection, type LexicalEditor} from 'lexical';
import {useEffect} from 'react';
import type {Notepad} from '../../api/notepad';
import type {Procedure} from '../../api/procedures';
import {isDisabled, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {richText, selectRichText, setRichText, typeRichText} from '../../test/richText';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {NotepadProvider, useNotepad, type OpenProcedure} from './NotepadContext';
import {CANT_REMOVE_SELECTION} from './AddToProcedureDialog';
import {AUTOSAVE_MS, NOT_REMOVED, NotepadPane} from './NotepadPane';

const NOTEPAD = '/api/me/notepad';

const notepad = (overrides: Partial<Notepad> = {}): Notepad => ({
  text: 'Synthetic jotting: ring the ward at 2pm.',
  lastUpdatedUtc: '2026-11-17T10:32:00Z',
  version: 3,
  keepDays: 30,
  ...overrides,
});

const procedure = (id: string, title: string, overrides: Partial<Procedure> = {}): Procedure => ({
  id,
  title,
  patientId: `patient-${id}`,
  patientName: `Synthetic Person ${id}`,
  patient: {id: `patient-${id}`, firstName: 'Freya', lastName: `Ahmed${id}`, dateOfBirth: '1976-12-01', nhsNumber: '9990000018'},
  canAddNotes: true,
  noteList: [],
  ...overrides,
});

/** Opens the notepad and offers these procedure pages to it, as open procedure pages do. */
function Offered({procedures}: {procedures: OpenProcedure[]}) {
  const notepad = useNotepad()!;
  useEffect(() => {
    notepad.open();
    const withdraw = procedures.map((p) => notepad.offer(p));
    return () => withdraw.forEach((w) => w());
    // Once, as the test sets them up.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return <>{notepad.isOpen ? 'notepad open' : 'notepad closed'}</>;
}

function renderPane(procedures: OpenProcedure[] = []) {
  return renderWithApp(
    <NotepadProvider>
      <SaveShortcutProvider>
        <Offered procedures={procedures} />
        <NotepadPane />
      </SaveShortcutProvider>
    </NotepadProvider>,
    {user: staffUser},
  );
}

const putBodies = async (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT').map((r) => r.clone().json()));

describe('NotepadPane', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('shows the notepad and when it was saved', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderPane();

    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(richText(box)).toBe('Synthetic jotting: ring the ward at 2pm.');
    // 10:32 UTC is 10:32 in the UK in November.
    expect(screen.getByText('Saved 10:32')).toBeInTheDocument();
  });

  it('has no prompt or explanation in it (removed by the product owner, 17 November 2026)', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad({text: '', version: 0, lastUpdatedUtc: null}))});
    renderPane();

    await screen.findByRole('textbox', {name: 'Notepad'});
    expect(screen.queryByText(/For jottings/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Jot something down/)).not.toBeInTheDocument();
  });

  it('saves by itself after a pause in typing, from the version it read', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic jotting, edited.', version: 4, lastUpdatedUtc: '2026-11-17T10:40:00Z'})),
    });
    renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await setRichText(box, 'Synthetic jotting, edited.');
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();

    expect(await screen.findByText('Saved 10:40', {}, {timeout: AUTOSAVE_MS + 2000})).toBeInTheDocument();
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic jotting, edited.', expectedVersion: 3}]);
  });

  it('saves at once on Ctrl+S', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
    });
    renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await setRichText(box, 'Synthetic, saved by shortcut.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true), {timeout: 500});
  });

  it('says so, with Reload, when it was saved somewhere else meanwhile, and Reload reads it afresh', async () => {
    let reads = 0;
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(reads++ === 0 ? notepad() : notepad({text: 'Synthetic: from the other tab.', version: 5})),
      [`PUT ${NOTEPAD}`]: () => json({title: 'Your notepad was saved somewhere else (another tab or window) since this one loaded it.'}, 409),
    });
    const {user} = renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await setRichText(box, 'Synthetic: from this tab.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});

    expect(await screen.findByText(/saved somewhere else \(another tab or window\)/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Reload'}));

    await waitFor(() => expect(richText(screen.getByRole('textbox', {name: 'Notepad'}))).toBe('Synthetic: from the other tab.'));
  });

  it('offers Retry when a save fails', async () => {
    let attempts = 0;
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => (attempts++ === 0 ? new Response(null, {status: 500}) : json(notepad({version: 4}))),
    });
    const {user} = renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await setRichText(box, 'Synthetic, failing first.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    await user.click(await screen.findByRole('button', {name: 'Retry'}));

    await waitFor(() => expect(fetch.mock.calls.filter(([r]) => (r as Request).method === 'PUT')).toHaveLength(2));
    await waitFor(() => expect(screen.queryByRole('button', {name: 'Retry'})).not.toBeInTheDocument());
  });

  it('saves a change before it closes', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
    });
    const {user} = renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(screen.getByText('notepad open')).toBeInTheDocument();

    await setRichText(box, 'Synthetic, typed just before closing.');
    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    await waitFor(() => expect(screen.getByText('notepad closed')).toBeInTheDocument());
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed just before closing.', expectedVersion: 3}]);
  });

  it('saves a change before signing out', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
    });
    renderPane();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, typed just before signing out.');

    // What useLogout does first.
    await act(() => flushSettings());

    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed just before signing out.', expectedVersion: 3}]);
  });

  it('stays open when the save before closing fails, so nothing typed is lost', async () => {
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => new Response(null, {status: 500}),
    });
    const {user} = renderPane();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, unsaved.');

    await user.click(screen.getByRole('button', {name: 'Close notepad'}));

    expect(await screen.findByRole('button', {name: 'Retry'})).toBeInTheDocument();
    expect(screen.getByText('notepad open')).toBeInTheDocument();
  });

  it('clears the notepad after asking', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: '', version: 4})),
    });
    const {user} = renderPane();
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Clear'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Clear Notepad'}));

    await waitFor(async () => expect(await putBodies(fetch)).toEqual([{text: '', expectedVersion: 3}]));
    expect(richText(screen.getByRole('textbox', {name: 'Notepad'}))).toBe('');
  });

  it('can only add a note to a procedure while a procedure page is open', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderPane();
    await screen.findByRole('textbox', {name: 'Notepad'});

    expect(isDisabled(screen.getByRole('button', {name: 'Add Note to Procedure'}))).toBe(true);
  });

  it('adds everything to the open procedure, after showing who the patient is, and takes it out of the notepad', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: '', version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', text: 'x', version: 1}, 201),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmedp1, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(dialog.getByText('Synthetic knee review')).toBeInTheDocument();
    expect(dialog.getByText('Synthetic jotting: ring the ward at 2pm.')).toBeInTheDocument();
    expect(dialog.getByRole('checkbox', {name: 'Remove it from the notepad'})).toBeChecked();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({text: 'Synthetic jotting: ring the ward at 2pm.'});
    await waitFor(async () => expect(await putBodies(fetch)).toEqual([{text: '', expectedVersion: 3}]));
  });

  it('keeps the text when told not to remove it', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('checkbox', {name: 'Remove it from the notepad'}));
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(false);
    expect(richText(screen.getByRole('textbox', {name: 'Notepad'}))).toBe('Synthetic jotting: ring the ward at 2pm.');
  });

  it('adds only the selected text, and takes only that out', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic first line. Synthetic second line.'})),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await selectRichText(box, 'Synthetic second line.');

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    expect(within(openDialog()!).getByText(/the text you selected/)).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({text: 'Synthetic second line.'});
    expect(await putBodies(fetch)).toEqual([{text: 'Synthetic first line. ', expectedVersion: 3}]);
  });

  it('takes out exactly the copy selected, not the first copy of the same words (H-37)', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad({text: 'Bed 3: obs stable\n\nBed 4: obs stable'})),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    // The second "obs stable", on Bed 4's line.
    await selectRichText(box, 'obs stable', 1);
    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    expect(within(openDialog()!).getByRole('checkbox', {name: 'Remove it from the notepad'})).toBeChecked();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true));
    const [saved] = await putBodies(fetch);
    expect(saved.text).toMatch(/^Bed 3: obs stable\n\nBed 4:\s*$/);
  });

  it('adds the selected part with its formatting, across bold and lines', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad({text: 'Rang: **worried** about the result.\n\nCall back at 3pm.'})),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await selectRichText(box, 'worried about the result.Call back');
    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({text: '**worried** about the result.\n\nCall back'});
  });

  it("says so, on the pane, when the selection couldn't be taken out because the notepad changed", async () => {
    let releaseAdd: () => void = () => {};
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic: call back.'})),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
      'POST /api/procedures/patient-procedures/p1/notes': () =>
        new Promise<Response>((resolve) => (releaseAdd = () => resolve(json({id: 'n1', version: 1}, 201)))),
    });
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    await selectRichText(box, 'call back');
    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Add Note'}));
    // The notepad changes while the note is being added.
    await typeRichText(box, ' Done.');
    await act(async () => releaseAdd());

    expect(await screen.findByText(NOT_REMOVED)).toBeInTheDocument();
    expect(richText(screen.getByRole('textbox', {name: 'Notepad'}))).toBe('Synthetic: call back. Done.');
    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true);
  });

  it("adds a selection the editor can't pin down as the browser shows it, without offering to take it out", async () => {
    mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic: call back.'})),
      'POST /api/procedures/patient-procedures/p1/notes': () => json({id: 'n1', version: 1}, 201),
    });
    renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review')}]);
    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    await selectRichText(box, 'call back');
    // The editor's own selection is gone, while the browser still shows the text selected.
    const editor = (box as HTMLElement & {__lexicalEditor: LexicalEditor}).__lexicalEditor;
    act(() => editor.update(() => $setSelection(null), {discrete: true, tag: 'skip-dom-selection'}));

    // A plain click, with no selection events for the editor to pick the browser's selection up again from.
    fireEvent.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('call back')).toBeInTheDocument();
    expect(dialog.getByRole('checkbox', {name: 'Remove it from the notepad'})).not.toBeChecked();
    expect(dialog.getByText(CANT_REMOVE_SELECTION)).toBeInTheDocument();
  });

  it('waits for each save in turn, so saves asked for during one never send the same version', async () => {
    const releases: (() => void)[] = [];
    let version = 3;
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: async (request) => {
        const body = (await request.clone().json()) as {text: string; expectedVersion: number};
        if (body.expectedVersion !== version) return json({title: 'Saved somewhere else.'}, 409);
        await new Promise<void>((resolve) => releases.push(resolve));
        version += 1;
        return json(notepad({text: body.text, version}));
      },
    });
    renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});

    // Typed, then Ctrl+S: the first save is held open.
    await setRichText(box, 'Synthetic one.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    await waitFor(() => expect(releases).toHaveLength(1));
    // Typed on, then two more saves asked for while it's still going (the review's sequence).
    await setRichText(box, 'Synthetic one, two.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    await act(async () => releases.shift()!());
    await waitFor(() => expect(releases).toHaveLength(1));
    await act(async () => releases.shift()!());

    await waitFor(async () => expect((await putBodies(fetch)).map((b) => b.expectedVersion)).toEqual([3, 4]));
    expect(screen.queryByRole('button', {name: 'Reload'})).not.toBeInTheDocument();
    expect(await screen.findByText(/^Saved/)).toBeInTheDocument();
  });

  it('saves what was typed as it goes (the window crossing between docked and sheet)', async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({text: 'Synthetic, typed just before.', version: 4})),
    });
    const {unmount} = renderPane();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, typed just before.');

    unmount();

    await waitFor(async () => expect(await putBodies(fetch)).toEqual([{text: 'Synthetic, typed just before.', expectedVersion: 3}]));
  });

  it("reads the notepad afresh when the tab is shown again with nothing typed, and shows another tab's change", async () => {
    let reads = 0;
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(reads++ === 0 ? notepad() : notepad({text: 'Synthetic: from the other tab.', version: 5})),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 6})),
    });
    renderPane();
    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(richText(box)).toBe('Synthetic jotting: ring the ward at 2pm.');

    await act(async () => window.dispatchEvent(new Event('focus')));

    await waitFor(() => expect(richText(screen.getByRole('textbox', {name: 'Notepad'}))).toBe('Synthetic: from the other tab.'));
    // And saves on from the newer version.
    await setRichText(screen.getByRole('textbox', {name: 'Notepad'}), 'Synthetic: added here.');
    fireEvent.keyDown(screen.getByRole('textbox', {name: 'Notepad'}), {key: 's', ctrlKey: true});
    await waitFor(async () => expect(await putBodies(fetch)).toEqual([{text: 'Synthetic: added here.', expectedVersion: 5}]));
  });

  it("doesn't read it afresh over something typed", async () => {
    const fetch = mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderPane();
    await setRichText(await screen.findByRole('textbox', {name: 'Notepad'}), 'Synthetic, not saved yet.');

    await act(async () => window.dispatchEvent(new Event('focus')));

    expect(fetch.mock.calls.filter(([r]) => (r as Request).method === 'GET')).toHaveLength(1);
  });

  it('says nothing is written yet in a notepad never used', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad({text: '', version: 0, lastUpdatedUtc: null}))});
    renderPane();
    await screen.findByRole('textbox', {name: 'Notepad'});

    expect(screen.getByText('Nothing written yet')).toBeInTheDocument();
    expect(screen.queryByText('Not saved yet')).not.toBeInTheDocument();
  });

  it('is a labelled region, and takes the focus when its user opens it', async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    renderPane();

    const box = await screen.findByRole('textbox', {name: 'Notepad'});
    expect(screen.getByRole('region', {name: 'Notepad'})).toBeInTheDocument();
    await waitFor(() => expect(box).toHaveFocus());
  });

  it('asks which procedure when two are open, and adds nothing until one is chosen', {timeout: 20000}, async () => {
    const fetch = mockApi({
      [`GET ${NOTEPAD}`]: () => json(notepad()),
      [`PUT ${NOTEPAD}`]: () => json(notepad({version: 4})),
      'POST /api/procedures/patient-procedures/p2/notes': () => json({id: 'n1', version: 1}, 201),
    });
    const {user} = renderPane([
      {key: 'b', side: 'right', procedure: procedure('p2', 'Synthetic cataract')},
      {key: 'a', side: 'left', procedure: procedure('p1', 'Synthetic knee review')},
    ]);
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));
    const dialog = within(openDialog()!);
    expect(dialog.getAllByRole('radio')).toHaveLength(2);
    expect(dialog.getByText('Left: Synthetic knee review')).toBeInTheDocument();
    expect(dialog.getByText('Right: Synthetic cataract')).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Note'}))).toBe(true);

    await user.click(dialog.getByRole('radio', {name: /Right: Synthetic cataract/}));
    expect(dialog.getByRole('region', {name: 'Patient: Ahmedp2, Freya'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Add Note'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => new URL((r as Request).url).pathname.endsWith('/p2/notes'))).toBe(true));
  });

  it("won't add to a procedure that takes no notes", async () => {
    mockApi({[`GET ${NOTEPAD}`]: () => json(notepad())});
    const {user} = renderPane([{key: 'a', procedure: procedure('p1', 'Synthetic knee review', {canAddNotes: false})}]);
    await screen.findByRole('textbox', {name: 'Notepad'});

    await user.click(screen.getByRole('button', {name: 'Add Note to Procedure'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText(/can't take notes now/)).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Note'}))).toBe(true);
  });
});

import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {procedureQuery, type Procedure, type ProcedureNote} from '../../api/procedures';
import type {CurrentUser} from '../../auth/auth';
import {adminUser, isDisabled, mockFetch, openDialog, renderWithApp, staffUser} from '../../test/render';
import {richText, setRichText} from '../../test/richText';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {NOTE_CONFLICT_FALLBACK, ProcedureNotes, whyNoNotes} from './ProcedureNotes';

const note = (overrides: Partial<ProcedureNote> = {}): ProcedureNote => ({
  id: 'n1',
  authorUserId: 'u1',
  authorUsername: 'test-user',
  text: 'Synthetic note: prefers **mornings**.',
  createdAtUtc: '2026-11-03T10:32:00Z',
  lastEditedAtUtc: null,
  version: 1,
  canEdit: true,
  ...overrides,
});

const procedure = (overrides: Partial<Procedure> = {}): Procedure => ({
  id: 'p1',
  patientId: 'pat1',
  title: 'Synthetic knee review',
  canAddNotes: true,
  noteList: [note(), note({id: 'n2', authorUserId: 'u2', authorUsername: 'k.ashford', text: 'Synthetic older note.', createdAtUtc: '2026-11-01T09:00:00Z', canEdit: false, lastEditedAtUtc: '2026-11-02T09:15:00Z', version: 2})],
  ...overrides,
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {status, headers: {'Content-Type': 'application/json'}});
}

function renderNotes(p: Procedure = procedure(), signedIn: CurrentUser = staffUser) {
  const result = renderWithApp(
    <SaveShortcutProvider>
      <ProcedureNotes procedure={p} />
    </SaveShortcutProvider>,
    {user: signedIn},
  );
  result.queryClient.setQueryData(procedureQuery(p.id ?? '').queryKey, p);
  return result;
}

describe('ProcedureNotes', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('lists the notes, newest first, with who wrote them, when, and when they were edited', async () => {
    renderNotes();

    const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText(/^test-user, /)).toBeInTheDocument();
    expect(within(items[0]).getByText('mornings')).toBeInTheDocument();
    expect(within(items[1]).getByText(/^k\.ashford, /)).toBeInTheDocument();
    expect(within(items[1]).getByText(/^Edited /)).toBeInTheDocument();
    // Only your own note can be edited.
    expect(within(items[0]).getByRole('button', {name: 'Edit'})).toBeInTheDocument();
    expect(within(items[1]).queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
  });

  it('says when there are none', async () => {
    renderNotes(procedure({noteList: []}));
    expect(await screen.findByText('No notes yet.')).toBeInTheDocument();
  });

  it('offers no adding or editing when the procedure takes no notes', async () => {
    renderNotes(procedure({canAddNotes: false, status: 'Cancelled', noteList: [note({canEdit: false})]}));

    expect(await screen.findByText('mornings')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: 'New note'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add Note'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
    // It says why, as the stage section does.
    expect(screen.getByText("Notes can't be added or edited while the procedure is cancelled.")).toBeInTheDocument();
  });

  it("says why notes can't be added: the record's restriction first, then the status", () => {
    expect(whyNoNotes(procedure({readOnlyReason: 'Synthetic: the record is restricted.', status: 'InProgress'}))).toBe(
      'Synthetic: the record is restricted.',
    );
    expect(whyNoNotes(procedure({status: 'Archived'}))).toBe("Notes can't be added or edited while the procedure is archived.");
    expect(whyNoNotes(procedure({status: 'InProgress'}))).toBe("Notes can't be added or edited now.");
  });

  it('adds a note, which shows at once, and empties the box', async () => {
    const added = note({id: 'n3', text: 'Synthetic new note.', createdAtUtc: '2026-11-04T08:00:00Z'});
    const fetch = mockFetch(async (request) => {
      if (request.method === 'POST') return json(added, 201);
      return json(procedure({noteList: [added, ...(procedure().noteList ?? [])]}));
    });
    const {user, queryClient} = renderNotes();
    const button = await screen.findByRole('button', {name: 'Add Note'});
    expect(isDisabled(button)).toBe(true);

    const box = await screen.findByRole('textbox', {name: 'New note'});
    await setRichText(box, 'Synthetic new note.');
    await user.click(button);

    await waitFor(() => expect(queryClient.getQueryData<Procedure>(['procedures', 'p1'])?.noteList?.[0]?.id).toBe('n3'));
    const post = fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'POST')!;
    expect(new URL(post.url).pathname).toBe('/api/procedures/patient-procedures/p1/notes');
    expect(await post.json()).toEqual({text: 'Synthetic new note.'});
    await waitFor(() => expect(richText(screen.getByRole('textbox', {name: 'New note'}))).toBe(''));
    // The cursor goes back into the empty box, rather than being lost with the old one.
    await waitFor(() => expect(screen.getByRole('textbox', {name: 'New note'})).toHaveFocus());
  });

  it('adds a note on Ctrl+S in the box', async () => {
    const fetch = mockFetch(async (request) => (request.method === 'POST' ? json(note({id: 'n3'}), 201) : json(procedure())));
    renderNotes();
    const box = await screen.findByRole('textbox', {name: 'New note'});

    // Nothing written yet: nothing to add.
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    expect(fetch).not.toHaveBeenCalled();

    await setRichText(box, 'Synthetic note by shortcut.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    await waitFor(() => expect(fetch.mock.calls.some(([input]) => (input as Request).method === 'POST')).toBe(true));
  });

  it('edits your own note from the version you saw', async () => {
    const edited = note({text: 'Synthetic note, edited.', version: 2, lastEditedAtUtc: '2026-11-04T09:00:00Z'});
    const fetch = mockFetch(async (request) => (request.method === 'PUT' ? json(edited) : json(procedure({noteList: [edited]}))));
    const {user} = renderNotes();

    await user.click(await screen.findByRole('button', {name: 'Edit'}));
    const box = await screen.findByRole('textbox', {name: 'Note'});
    expect(richText(box)).toContain('Synthetic note: prefers');
    await setRichText(box, 'Synthetic note, edited.');
    await user.click(screen.getByRole('button', {name: 'Save Note'}));

    const put = await waitFor(() => fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'PUT')!);
    expect(new URL(put.url).pathname).toBe('/api/procedures/patient-procedures/p1/notes/n1');
    expect(await put.json()).toEqual({text: 'Synthetic note, edited.', expectedVersion: 1});
    await waitFor(() => expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument());
  });

  it('sends the version the edit started from, even if the note has been saved elsewhere since', async () => {
    const fetch = mockFetch(async (request) =>
      request.method === 'PUT' ? json({title: 'This note was changed somewhere else.'}, 409) : json(procedure()),
    );
    const {user, queryClient} = renderNotes();
    await user.click(await screen.findByRole('button', {name: 'Edit'}));
    await setRichText(await screen.findByRole('textbox', {name: 'Note'}), 'Synthetic edit from the left half.');

    // Meanwhile the right half saved the same note: the cache now has version 2.
    act(() => {
      queryClient.setQueryData(procedureQuery('p1').queryKey, procedure({noteList: [note({text: 'Synthetic right half.', version: 2})]}));
    });
    await user.click(screen.getByRole('button', {name: 'Save Note'}));

    const put = await waitFor(() => fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'PUT')!);
    expect(await put.json()).toEqual({text: 'Synthetic edit from the left half.', expectedVersion: 1});
    // The conflict shows beside the note being edited.
    const editing = screen.getByRole('textbox', {name: 'Note'}).closest('li')!;
    expect(await within(editing).findByText('This note was changed somewhere else.')).toBeInTheDocument();
    expect(within(editing).getByRole('button', {name: 'Reload'})).toBeInTheDocument();
  });

  it('cancels an edit without saving', async () => {
    const fetch = mockFetch(() => json(procedure()));
    const {user} = renderNotes();

    await user.click(await screen.findByRole('button', {name: 'Edit'}));
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('says so, with Reload, when the note was changed somewhere else first', async () => {
    mockFetch(async (request) =>
      request.method === 'PUT'
        ? json({title: 'This note was changed somewhere else (another tab or window) after you opened it.'}, 409)
        : json(procedure()),
    );
    const {user, queryClient} = renderNotes();

    await user.click(await screen.findByRole('button', {name: 'Edit'}));
    await setRichText(await screen.findByRole('textbox', {name: 'Note'}), 'Synthetic stale edit.');
    await user.click(screen.getByRole('button', {name: 'Save Note'}));

    expect(await screen.findByText(/changed somewhere else \(another tab or window\)/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Reload'}));

    // The edit closes, and the procedure is to be read again (the page showing it reads it at once).
    expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument();
    await waitFor(() => expect(queryClient.getQueryState(procedureQuery('p1').queryKey)?.isInvalidated).toBe(true));
    expect(screen.queryByRole('button', {name: 'Reload'})).not.toBeInTheDocument();
  });

  it('falls back to its own words for a conflict without a message', async () => {
    mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 409}) : json(procedure())));
    const {user} = renderNotes();

    await setRichText(await screen.findByRole('textbox', {name: 'New note'}), 'Synthetic note.');
    await user.click(screen.getByRole('button', {name: 'Add Note'}));

    expect(await screen.findByText(NOTE_CONFLICT_FALLBACK)).toBeInTheDocument();
  });

  it('says why a note could not be added', async () => {
    mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 500}) : json(procedure())));
    const {user} = renderNotes();

    await setRichText(await screen.findByRole('textbox', {name: 'New note'}), 'Synthetic note.');
    await user.click(screen.getByRole('button', {name: 'Add Note'}));

    expect(await screen.findByText('The note could not be added.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reload'})).not.toBeInTheDocument();
  });

  describe('deleting notes', () => {
    const deletable = () =>
      procedure({
        noteList: [note({canDelete: true, canRequestDeletion: true}), note({id: 'n2', authorUsername: 'k.ashford', canEdit: false, canDelete: true, canRequestDeletion: true})],
      });
    const requests = (fetch: ReturnType<typeof mockFetch>) =>
      fetch.mock.calls.map(([input]) => input as Request).filter((r) => r.method === 'POST');

    it('offers admins Delete on every note they can delete, and never Request Deletion', async () => {
      renderNotes(deletable(), adminUser);

      const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
      expect(within(items[0]).getByRole('button', {name: 'Delete'})).toBeInTheDocument();
      expect(within(items[1]).getByRole('button', {name: 'Delete'})).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Request Deletion'})).not.toBeInTheDocument();
    });

    it('offers staff Request Deletion on any note, and never Delete', async () => {
      renderNotes(deletable());

      const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
      expect(within(items[0]).getByRole('button', {name: 'Request Deletion'})).toBeInTheDocument();
      expect(within(items[1]).getByRole('button', {name: 'Request Deletion'})).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Delete'})).not.toBeInTheDocument();
    });

    it('offers neither when the API says no (a restricted record)', async () => {
      renderNotes(procedure({noteList: [note({canDelete: false, canRequestDeletion: false})]}), adminUser);
      await screen.findByText('mornings');
      expect(screen.queryByRole('button', {name: 'Delete'})).not.toBeInTheDocument();
    });

    it('deletes a note for an admin, with a reason, and takes it off the list at once', async () => {
      const fetch = mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 204}) : json(procedure({noteList: []}))));
      const {user, queryClient} = renderNotes(deletable(), adminUser);
      const first = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem')[0];
      await user.click(within(first).getByRole('button', {name: 'Delete'}));

      const dialog = within(openDialog()!);
      // The note being deleted shows in the dialog, and a reason is needed first.
      expect(dialog.getByText('mornings')).toBeInTheDocument();
      expect(isDisabled(dialog.getByRole('button', {name: 'Delete Note'}))).toBe(true);
      await user.type(dialog.getByRole('textbox', {name: /Reason/}), 'Synthetic: on the wrong patient.');
      await user.click(dialog.getByRole('button', {name: 'Delete Note'}));

      const post = await waitFor(() => requests(fetch)[0]);
      expect(new URL(post.url).pathname).toBe('/api/procedures/patient-procedures/p1/notes/n1/delete');
      expect(await post.json()).toEqual({reason: 'Synthetic: on the wrong patient.'});
      await waitFor(() => expect(queryClient.getQueryData<Procedure>(['procedures', 'p1'])?.noteList?.some((n) => n.id === 'n1')).toBe(false));
      await waitFor(() => expect(openDialog()).toBeNull());
    });

    it("asks an admin to delete a note for staff, and shows it's been asked", async () => {
      const asked = note({id: 'n2', authorUsername: 'k.ashford', canEdit: false, canRequestDeletion: false, deletionRequestId: 'r1'});
      const fetch = mockFetch(async (request) => (request.method === 'POST' ? json(asked, 201) : json(procedure({noteList: [asked]}))));
      const {user, queryClient} = renderNotes(deletable());
      const second = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem')[1];
      await user.click(within(second).getByRole('button', {name: 'Request Deletion'}));

      const dialog = within(openDialog()!);
      await user.type(dialog.getByRole('textbox', {name: /Reason/}), 'Synthetic: a duplicate.');
      await user.click(dialog.getByRole('button', {name: 'Request Deletion'}));

      const post = await waitFor(() => requests(fetch)[0]);
      expect(new URL(post.url).pathname).toBe('/api/procedures/patient-procedures/p1/notes/n2/deletion-requests');
      expect(await post.json()).toEqual({reason: 'Synthetic: a duplicate.'});
      await waitFor(() =>
        expect(queryClient.getQueryData<Procedure>(['procedures', 'p1'])?.noteList?.find((n) => n.id === 'n2')?.deletionRequestId).toBe('r1'),
      );
    });

    it("shows the API's words when a request is refused, and keeps the dialog open", async () => {
      mockFetch(async (request) =>
        request.method === 'POST'
          ? json({title: 'k.ashford already asked for this note to be deleted, Tuesday 17 November at 10:00. An admin will decide.'}, 409)
          : json(deletable()),
      );
      const {user} = renderNotes(deletable());
      await user.click((await screen.findAllByRole('button', {name: 'Request Deletion'}))[0]);
      const dialog = within(openDialog()!);
      await user.type(dialog.getByRole('textbox', {name: /Reason/}), 'Synthetic reason.');
      await user.click(dialog.getByRole('button', {name: 'Request Deletion'}));

      expect(await dialog.findByText(/already asked for this note to be deleted/)).toBeInTheDocument();
      expect(openDialog()).not.toBeNull();
    });

    it("puts the API's 400 on the reason", async () => {
      mockFetch(async (request) =>
        request.method === 'POST'
          ? json({title: 'Validation failed', status: 400, errors: {Reason: ['Say why the note should be deleted.']}}, 400)
          : json(deletable()),
      );
      const {user} = renderNotes(deletable(), adminUser);
      await user.click((await screen.findAllByRole('button', {name: 'Delete'}))[0]);
      const dialog = within(openDialog()!);
      await user.type(dialog.getByRole('textbox', {name: /Reason/}), 'x');
      await user.click(dialog.getByRole('button', {name: 'Delete Note'}));

      expect(await dialog.findByText('Say why the note should be deleted.')).toBeInTheDocument();
    });

    it('marks a note whose deletion has been asked for, and lets an admin open the request', async () => {
      const fetch = mockFetch(() => json({}, 404));
      const {user} = renderNotes(procedure({noteList: [note({deletionRequestId: 'r1', canRequestDeletion: false, canDelete: true})]}), adminUser);

      expect(await screen.findByText('Deletion requested')).toBeInTheDocument();
      await user.click(screen.getByRole('button', {name: 'Review Request'}));
      await waitFor(() =>
        expect(fetch.mock.calls.some(([input]) => new URL((input as Request).url).pathname === '/api/procedures/note-deletion-requests/r1')).toBe(true),
      );
    });

    it("shows staff that a deletion's been asked for, without the admin's Review Request", async () => {
      renderNotes(procedure({noteList: [note({deletionRequestId: 'r1', canRequestDeletion: false})]}));
      expect(await screen.findByText('Deletion requested')).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Review Request'})).not.toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Request Deletion'})).not.toBeInTheDocument();
    });
  });

  it("warns when a note is too long, and won't send it", async () => {
    const fetch = mockFetch(() => json(procedure()));
    renderNotes();
    const box = await screen.findByRole('textbox', {name: 'New note'});
    await setRichText(box, 'a'.repeat(4001));

    expect(screen.getByText('Too long: at most 4000 characters.')).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Add Note'}))).toBe(true);
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    expect(fetch.mock.calls.some(([input]) => (input as Request).method === 'POST')).toBe(false);
  });
});

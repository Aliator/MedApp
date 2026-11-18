import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {pathwayQuery, type Pathway, type PathwayNote} from '../../api/pathways';
import type {CurrentUser} from '../../auth/auth';
import {adminUser, isDisabled, mockFetch, openDialog, renderWithApp, staffUser} from '../../test/render';
import {richText, setRichText} from '../../test/richText';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {NOTE_CONFLICT_FALLBACK, PathwayNotes, whyNoNotes} from './PathwayNotes';

const note = (overrides: Partial<PathwayNote> = {}): PathwayNote => ({
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

const pathway = (overrides: Partial<Pathway> = {}): Pathway => ({
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

function renderNotes(p: Pathway = pathway(), signedIn: CurrentUser = staffUser) {
  const result = renderWithApp(
    <SaveShortcutProvider>
      <PathwayNotes pathway={p} />
    </SaveShortcutProvider>,
    {user: signedIn},
  );
  result.queryClient.setQueryData(pathwayQuery(p.id ?? '').queryKey, p);
  return result;
}

describe('PathwayNotes', () => {
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
    expect(within(items[0]).getByRole('button', {name: /^Edit note by/})).toBeInTheDocument();
    expect(within(items[1]).queryByRole('button', {name: /^Edit note by/})).not.toBeInTheDocument();
  });

  it('says when there are none', async () => {
    renderNotes(pathway({noteList: []}));
    expect(await screen.findByText('No notes yet.')).toBeInTheDocument();
  });

  it('offers no adding or editing when the pathway takes no notes', async () => {
    renderNotes(pathway({canAddNotes: false, status: 'Cancelled', noteList: [note({canEdit: false})]}));

    expect(await screen.findByText('mornings')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: 'New note'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add Note'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Edit note by/})).not.toBeInTheDocument();
    // It says why, as the stage section does.
    expect(screen.getByText("Notes can't be added or edited while the pathway is cancelled.")).toBeInTheDocument();
  });

  it("says why notes can't be added: the record's restriction first, then the status", () => {
    expect(whyNoNotes(pathway({readOnlyReason: 'Synthetic: the record is restricted.', status: 'InProgress'}))).toBe(
      'Synthetic: the record is restricted.',
    );
    expect(whyNoNotes(pathway({status: 'Archived'}))).toBe("Notes can't be added or edited while the pathway is archived.");
    expect(whyNoNotes(pathway({status: 'InProgress'}))).toBe("Notes can't be added or edited now.");
  });

  it('adds a note, which shows at once, and empties the box', async () => {
    const added = note({id: 'n3', text: 'Synthetic new note.', createdAtUtc: '2026-11-04T08:00:00Z'});
    const fetch = mockFetch(async (request) => {
      if (request.method === 'POST') return json(added, 201);
      return json(pathway({noteList: [added, ...(pathway().noteList ?? [])]}));
    });
    const {user, queryClient} = renderNotes();
    const button = await screen.findByRole('button', {name: 'Add Note'});
    expect(isDisabled(button)).toBe(true);

    const box = await screen.findByRole('textbox', {name: 'New note'});
    await setRichText(box, 'Synthetic new note.');
    await user.click(button);

    await waitFor(() => expect(queryClient.getQueryData<Pathway>(['pathways', 'p1'])?.noteList?.[0]?.id).toBe('n3'));
    const post = fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'POST')!;
    expect(new URL(post.url).pathname).toBe('/api/pathways/patient-pathways/p1/notes');
    expect(await post.json()).toEqual({text: 'Synthetic new note.'});
    await waitFor(() => expect(richText(screen.getByRole('textbox', {name: 'New note'}))).toBe(''));
    // The cursor goes back into the empty box, rather than being lost with the old one.
    await waitFor(() => expect(screen.getByRole('textbox', {name: 'New note'})).toHaveFocus());
  });

  it('adds a note on Ctrl+S in the box', async () => {
    const fetch = mockFetch(async (request) => (request.method === 'POST' ? json(note({id: 'n3'}), 201) : json(pathway())));
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
    const fetch = mockFetch(async (request) => (request.method === 'PUT' ? json(edited) : json(pathway({noteList: [edited]}))));
    const {user} = renderNotes();

    await user.click(await screen.findByRole('button', {name: /^Edit note by/}));
    const box = await screen.findByRole('textbox', {name: 'Note'});
    expect(richText(box)).toContain('Synthetic note: prefers');
    await setRichText(box, 'Synthetic note, edited.');
    await user.click(screen.getByRole('button', {name: 'Save Note'}));

    const put = await waitFor(() => fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'PUT')!);
    expect(new URL(put.url).pathname).toBe('/api/pathways/patient-pathways/p1/notes/n1');
    expect(await put.json()).toEqual({text: 'Synthetic note, edited.', expectedVersion: 1});
    await waitFor(() => expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument());
  });

  it('saves an edit on Ctrl+S in its box, and does nothing while the text is unchanged', async () => {
    const edited = note({text: 'Synthetic note, edited.', version: 2, lastEditedAtUtc: '2026-11-04T09:00:00Z'});
    const fetch = mockFetch(async (request) => (request.method === 'PUT' ? json(edited) : json(pathway({noteList: [edited]}))));
    const {user} = renderNotes();
    const puts = () => fetch.mock.calls.map(([input]) => input as Request).filter((r) => r.method === 'PUT');

    await user.click(await screen.findByRole('button', {name: /^Edit note by/}));
    const box = await screen.findByRole('textbox', {name: 'Note'});
    // The editor holds the focus, so Ctrl+S saves it rather than the Add Note box.
    act(() => box.focus());
    // Nothing changed yet: nothing to save.
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    expect(puts()).toHaveLength(0);
    await setRichText(box, 'Synthetic note, edited.');
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});

    await waitFor(() => expect(puts()).toHaveLength(1));
    expect(await puts()[0].json()).toEqual({text: 'Synthetic note, edited.', expectedVersion: 1});
    await waitFor(() => expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument());
  });

  it('sends the version the edit started from, even if the note has been saved elsewhere since', async () => {
    const fetch = mockFetch(async (request) =>
      request.method === 'PUT' ? json({title: 'This note was changed somewhere else.'}, 409) : json(pathway()),
    );
    const {user, queryClient} = renderNotes();
    await user.click(await screen.findByRole('button', {name: /^Edit note by/}));
    await setRichText(await screen.findByRole('textbox', {name: 'Note'}), 'Synthetic edit from the left half.');

    // Meanwhile the right half saved the same note: the cache now has version 2.
    act(() => {
      queryClient.setQueryData(pathwayQuery('p1').queryKey, pathway({noteList: [note({text: 'Synthetic right half.', version: 2})]}));
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
    const fetch = mockFetch(() => json(pathway()));
    const {user} = renderNotes();

    await user.click(await screen.findByRole('button', {name: /^Edit note by/}));
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));

    expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('says so, with Reload, when the note was changed somewhere else first', async () => {
    mockFetch(async (request) =>
      request.method === 'PUT'
        ? json({title: 'This note was changed somewhere else (another tab or window) after you opened it.'}, 409)
        : json(pathway()),
    );
    const {user, queryClient} = renderNotes();

    await user.click(await screen.findByRole('button', {name: /^Edit note by/}));
    await setRichText(await screen.findByRole('textbox', {name: 'Note'}), 'Synthetic stale edit.');
    await user.click(screen.getByRole('button', {name: 'Save Note'}));

    expect(await screen.findByText(/changed somewhere else \(another tab or window\)/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Reload'}));

    // The edit closes, and the pathway is to be read again (the page showing it reads it at once).
    expect(screen.queryByRole('textbox', {name: 'Note'})).not.toBeInTheDocument();
    await waitFor(() => expect(queryClient.getQueryState(pathwayQuery('p1').queryKey)?.isInvalidated).toBe(true));
    expect(screen.queryByRole('button', {name: 'Reload'})).not.toBeInTheDocument();
  });

  it('falls back to its own words for a conflict without a message', async () => {
    mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 409}) : json(pathway())));
    const {user} = renderNotes();

    await setRichText(await screen.findByRole('textbox', {name: 'New note'}), 'Synthetic note.');
    await user.click(screen.getByRole('button', {name: 'Add Note'}));

    expect(await screen.findByText(NOTE_CONFLICT_FALLBACK)).toBeInTheDocument();
  });

  it('says why a note could not be added', async () => {
    mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 500}) : json(pathway())));
    const {user} = renderNotes();

    await setRichText(await screen.findByRole('textbox', {name: 'New note'}), 'Synthetic note.');
    await user.click(screen.getByRole('button', {name: 'Add Note'}));

    expect(await screen.findByText('The note could not be added.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reload'})).not.toBeInTheDocument();
  });

  describe('deleting notes', () => {
    const patient = {id: 'pat1', firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1980-04-02', nhsNumber: '9990000018'};
    const deletable = (flags: Partial<PathwayNote> = {canDelete: true, canRequestDeletion: false}) =>
      pathway({
        patient,
        noteList: [note(flags), note({id: 'n2', authorUsername: 'k.ashford', canEdit: false, version: 3, ...flags})],
      });
    const asStaff = {canDelete: false, canRequestDeletion: true};
    const requests = (fetch: ReturnType<typeof mockFetch>) =>
      fetch.mock.calls.map(([input]) => input as Request).filter((r) => r.method === 'POST');
    const chooseReason = async (user: ReturnType<typeof renderNotes>['user'], dialog: ReturnType<typeof within>, reason: RegExp) =>
      user.click(dialog.getByRole('radio', {name: reason}));

    it('offers Delete where the API says this user can delete, and Request Deletion where they can ask', async () => {
      renderNotes(deletable());
      const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
      expect(within(items[0]).getByRole('button', {name: /^Delete note by test-user, /})).toBeInTheDocument();
      expect(within(items[1]).getByRole('button', {name: /^Delete note by k\.ashford, /})).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Request deletion/})).not.toBeInTheDocument();
    });

    it('follows the flags, not the role: a staff response offers only Request Deletion', async () => {
      // An admin's page with a staff-shaped response still offers only what the API allows.
      renderNotes(deletable(asStaff), adminUser);
      const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
      expect(within(items[0]).getByRole('button', {name: /^Request deletion of note by test-user, /})).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Delete note by/})).not.toBeInTheDocument();
    });

    it('names each note’s buttons for their note, by its author and time', async () => {
      renderNotes(deletable({canDelete: true, canRequestDeletion: false, canEdit: true}));
      const items = within(await screen.findByRole('list', {name: 'Notes'})).getAllByRole('listitem');
      const names = within(items[0]).getAllByRole('button').map((b) => b.getAttribute('aria-label'));
      expect(names).toEqual([expect.stringMatching(/^Edit note by test-user, \d+ \w+ 2026, \d\d:\d\d$/), expect.stringMatching(/^Delete note by test-user, /)]);
      // Two notes' Delete buttons are told apart.
      expect(new Set(screen.getAllByRole('button', {name: /^Delete note by/}).map((b) => b.getAttribute('aria-label'))).size).toBe(2);
    });

    it('offers neither when the API says no (a restricted record)', async () => {
      renderNotes(pathway({noteList: [note({canDelete: false, canRequestDeletion: false})]}), adminUser);
      await screen.findByText('mornings');
      expect(screen.queryByRole('button', {name: /^Delete note/})).not.toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Request deletion/})).not.toBeInTheDocument();
    });

    it('deletes a note with a reason and detail, from the version shown, under the patient, and takes it off at once', async () => {
      const fetch = mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 204}) : json(pathway({noteList: []}))));
      const {user, queryClient} = renderNotes(deletable(), adminUser);
      await user.click(await screen.findByRole('button', {name: /^Delete note by k\.ashford/}));

      const dialog = within(openDialog()!);
      // Who it's about, as the patient banner shows them, and the note itself.
      expect(dialog.getByRole('region', {name: /Patient: Ahmed, Freya/})).toBeInTheDocument();
      expect(dialog.getByText(/999 000 0018/)).toBeInTheDocument();
      expect(dialog.getByText('mornings')).toBeInTheDocument();
      // A reason must be chosen first.
      expect(isDisabled(dialog.getByRole('button', {name: 'Delete Note'}))).toBe(true);
      await chooseReason(user, dialog, /Wrong patient/);
      await user.type(dialog.getByRole('textbox', {name: /Detail/}), '  Synthetic: belongs to the other pathway.  ');
      await user.click(dialog.getByRole('button', {name: 'Delete Note'}));

      const post = await waitFor(() => requests(fetch)[0]);
      expect(new URL(post.url).pathname).toBe('/api/pathways/patient-pathways/p1/notes/n2/delete');
      expect(await post.json()).toEqual({reason: 'WrongPatient', detail: 'Synthetic: belongs to the other pathway.', expectedVersion: 3});
      await waitFor(() => expect(queryClient.getQueryData<Pathway>(['pathways', 'p1'])?.noteList?.some((n) => n.id === 'n2')).toBe(false));
      await waitFor(() => expect(openDialog()).toBeNull());
    });

    it('needs a detail for Other, and sends none when it is left empty for another reason', async () => {
      const fetch = mockFetch(async (request) => (request.method === 'POST' ? new Response(null, {status: 204}) : json(pathway({noteList: []}))));
      const {user} = renderNotes(deletable(), adminUser);
      await user.click(await screen.findByRole('button', {name: /^Delete note by test-user/}));
      const dialog = within(openDialog()!);

      await chooseReason(user, dialog, /Other/);
      expect(isDisabled(dialog.getByRole('button', {name: 'Delete Note'}))).toBe(true);
      await chooseReason(user, dialog, /Duplicate/);
      await user.click(dialog.getByRole('button', {name: 'Delete Note'}));

      const post = await waitFor(() => requests(fetch)[0]);
      expect(await post.json()).toEqual({reason: 'Duplicate', detail: null, expectedVersion: 1});
    });

    it("asks an admin to delete a note for staff, and shows it's been asked", async () => {
      const asked = note({id: 'n2', authorUsername: 'k.ashford', canEdit: false, canRequestDeletion: false, deletionRequestId: 'r1'});
      const fetch = mockFetch(async (request) => (request.method === 'POST' ? json(asked, 201) : json(pathway({noteList: [asked]}))));
      const {user, queryClient} = renderNotes(deletable(asStaff));
      await user.click(await screen.findByRole('button', {name: /^Request deletion of note by k\.ashford/}));

      const dialog = within(openDialog()!);
      await chooseReason(user, dialog, /Written in error/);
      await user.click(dialog.getByRole('button', {name: 'Request Deletion'}));

      const post = await waitFor(() => requests(fetch)[0]);
      expect(new URL(post.url).pathname).toBe('/api/pathways/patient-pathways/p1/notes/n2/deletion-requests');
      expect(await post.json()).toEqual({reason: 'WrittenInError', detail: null});
      await waitFor(() =>
        expect(queryClient.getQueryData<Pathway>(['pathways', 'p1'])?.noteList?.find((n) => n.id === 'n2')?.deletionRequestId).toBe('r1'),
      );
    });

    it("shows the API's words when a request is refused, keeps the dialog open, and reads the pathway again", async () => {
      mockFetch(async (request) =>
        request.method === 'POST'
          ? json({title: 'k.ashford already asked for this note to be deleted, Tuesday 17 November at 10:00. An admin will decide.'}, 409)
          : json(deletable(asStaff)),
      );
      const {user, queryClient} = renderNotes(deletable(asStaff));
      await user.click((await screen.findAllByRole('button', {name: /^Request deletion of note/}))[0]);
      const dialog = within(openDialog()!);
      await chooseReason(user, dialog, /Duplicate/);
      await user.click(dialog.getByRole('button', {name: 'Request Deletion'}));

      expect(await dialog.findByText(/already asked for this note to be deleted/)).toBeInTheDocument();
      expect(openDialog()).not.toBeNull();
      // Read again, so the page shows "Deletion requested" from the pathway as it is now.
      expect(queryClient.getQueryState(['pathways', 'p1'])?.isInvalidated).toBe(true);
    });

    it("puts the API's 400 on the reason and the detail", async () => {
      mockFetch(async (request) =>
        request.method === 'POST'
          ? json({title: 'Validation failed', status: 400, errors: {Reason: ['Choose why.'], Detail: ['Too long.']}}, 400)
          : json(deletable()),
      );
      const {user} = renderNotes(deletable(), adminUser);
      await user.click((await screen.findAllByRole('button', {name: /^Delete note by/}))[0]);
      const dialog = within(openDialog()!);
      await chooseReason(user, dialog, /Duplicate/);
      await user.click(dialog.getByRole('button', {name: 'Delete Note'}));

      expect(await dialog.findByText('Choose why.')).toBeInTheDocument();
      expect(dialog.getByText('Too long.')).toBeInTheDocument();
    });

    it('marks a note whose deletion has been asked for, and lets an admin open the request beside the mark', async () => {
      const fetch = mockFetch(() => json({}, 404));
      const {user} = renderNotes(pathway({noteList: [note({deletionRequestId: 'r1', canRequestDeletion: false, canDelete: true})]}), adminUser);

      const token = await screen.findByText('Deletion requested');
      const review = screen.getByRole('button', {name: /^Review request for note by test-user, /});
      // Kept together, so the button never wraps alone between the byline and the note.
      expect(token.closest('[class]')?.parentElement?.contains(review) || token.parentElement?.parentElement?.contains(review)).toBe(true);
      await user.click(review);
      await waitFor(() =>
        expect(fetch.mock.calls.some(([input]) => new URL((input as Request).url).pathname === '/api/pathways/note-deletion-requests/r1')).toBe(true),
      );
    });

    it('cancels a deletion without sending anything', async () => {
      const fetch = mockFetch(() => json(pathway()));
      const {user} = renderNotes(deletable());

      await user.click((await screen.findAllByRole('button', {name: /^Delete note by/}))[0]);
      const dialog = within(openDialog()!);
      await chooseReason(user, dialog, /Wrong patient/);
      await user.click(dialog.getByRole('button', {name: 'Cancel'}));

      await waitFor(() => expect(openDialog()).toBeNull());
      expect(requests(fetch)).toHaveLength(0);
      expect(screen.getAllByRole('listitem')).toHaveLength(2);
    });

    it('closes the request it opened, leaving the note as it was', async () => {
      const request = {
        id: 'r1', status: 'Declined', reason: 'Duplicate', detail: null, requestedByUsername: 's.nurse',
        requestedAtUtc: '2026-11-17T10:00:00Z', decidedByUsername: 'test-admin', decidedAtUtc: '2026-11-17T11:00:00Z',
        pathwayId: 'p1', pathwayTitle: 'Synthetic knee review', patient: undefined,
        note: {id: 'n1', text: 'Synthetic note.', authorUsername: 'test-user', createdAtUtc: '2026-11-03T10:32:00Z', version: 1},
        canDecide: false, whyCantDecide: null,
      };
      mockFetch(async (input) => (new URL((input as Request).url).pathname.endsWith('/r1') ? json(request) : json(pathway())));
      const {user} = renderNotes(pathway({noteList: [note({deletionRequestId: 'r1', canRequestDeletion: false, canDelete: true})]}), adminUser);

      await user.click(await screen.findByRole('button', {name: /^Review request for note by/}));
      const shown = await waitFor(() => {
        const open = openDialog();
        if (!open) throw new Error('No dialog open yet');
        return open;
      });
      const closes = await within(shown).findAllByRole('button', {name: 'Close'});
      await user.click(closes[closes.length - 1]);

      await waitFor(() => expect(openDialog()).toBeNull());
      expect(screen.getByText('Deletion requested')).toBeInTheDocument();
    });

    it("shows staff that a deletion's been asked for, without the admin's Review Request", async () => {
      renderNotes(pathway({noteList: [note({deletionRequestId: 'r1', canRequestDeletion: false})]}));
      expect(await screen.findByText('Deletion requested')).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Review request/})).not.toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Request deletion/})).not.toBeInTheDocument();
    });
  });

  it("warns when a note is too long, and won't send it", async () => {
    const fetch = mockFetch(() => json(pathway()));
    renderNotes();
    const box = await screen.findByRole('textbox', {name: 'New note'});
    await setRichText(box, 'a'.repeat(4001));

    expect(screen.getByText('Too long: at most 4000 characters.')).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Add Note'}))).toBe(true);
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    expect(fetch.mock.calls.some(([input]) => (input as Request).method === 'POST')).toBe(false);
  });
});

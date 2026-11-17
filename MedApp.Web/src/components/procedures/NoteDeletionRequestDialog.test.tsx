import {waitFor, within} from '@testing-library/react';
import {procedureQuery, type NoteDeletionRequest} from '../../api/procedures';
import {adminUser, calls, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {NoteDeletionRequestDialog, outcome} from './NoteDeletionRequestDialog';

const requestId = '0000000d-0000-0000-0000-000000000001';
const procedureId = '0000000d-0000-0000-0000-000000000002';
const path = `/api/procedures/note-deletion-requests/${requestId}`;

const pending = (overrides: Partial<NoteDeletionRequest> = {}): NoteDeletionRequest => ({
  id: requestId,
  status: 'Pending',
  reason: 'Synthetic: written on the wrong patient.',
  requestedByUsername: 's.nurse',
  requestedAtUtc: '2026-11-17T10:00:00Z',
  decidedByUsername: null,
  decidedAtUtc: null,
  procedureId,
  procedureTitle: 'Synthetic knee review',
  patient: {
    id: '0000000d-0000-0000-0000-000000000003',
    firstName: 'Freya',
    lastName: 'Testpatient',
    dateOfBirth: '1980-02-01',
    nhsNumber: '9990000018',
    isRestricted: false,
    isDisputed: false,
    isAnonymised: false,
  },
  note: {
    id: '0000000d-0000-0000-0000-000000000004',
    text: 'Synthetic note: prefers **mornings**.',
    authorUsername: 'k.ashford',
    createdAtUtc: '2026-11-16T09:00:00Z',
    lastEditedAtUtc: null,
    deletedAtUtc: null,
    deletedByUsername: null,
    deletionReason: null,
  },
  canDecide: true,
  whyCantDecide: null,
  ...overrides,
});

function renderDialog(onClose = vi.fn()) {
  return {onClose, ...renderWithApp(<NoteDeletionRequestDialog requestId={requestId} onClose={onClose} />, {user: adminUser})};
}

/** The dialog, once it's open. */
const dialogShown = () =>
  waitFor(() => {
    const dialog = openDialog();
    if (!dialog) throw new Error('No dialog open yet');
    return dialog;
  });

const decisionBody = async (fetch: ReturnType<typeof mockApi>) => {
  const post = fetch.mock.calls.map(([input]) => input as Request).find((r) => r.method === 'POST')!;
  return post.json() as Promise<unknown>;
};

describe('NoteDeletionRequestDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows nothing when closed', () => {
    renderWithApp(<NoteDeletionRequestDialog requestId={null} onClose={() => {}} />, {user: adminUser});
    expect(openDialog()).toBeNull();
  });

  it('shows the patient, the procedure, the note, and who asked, when and why', async () => {
    mockApi({[`GET ${path}`]: () => json(pending())});
    renderDialog();

    const dialog = within(await dialogShown());
    expect(await dialog.findByRole('region', {name: /Testpatient, Freya/})).toBeInTheDocument();
    expect(dialog.getByRole('region', {name: /Testpatient, Freya/}).textContent).toMatch(/999 000 0018/);
    expect(dialog.getByText('Synthetic knee review')).toBeInTheDocument();
    expect(dialog.getByText('mornings')).toBeInTheDocument();
    expect(dialog.getByText(/^The note, by k\.ashford/)).toBeInTheDocument();
    expect(dialog.getByText(/^s\.nurse, /)).toBeInTheDocument();
    expect(dialog.getByText('Their reason:').parentElement).toHaveTextContent('Their reason: Synthetic: written on the wrong patient.');
    expect(dialog.getByRole('button', {name: 'Delete note'})).toBeInTheDocument();
    expect(dialog.getByRole('button', {name: 'Keep note'})).toBeInTheDocument();
  });

  it("deletes the note with the reason, which starts as the requester's and can be changed", async () => {
    const decided = pending({status: 'Approved', canDecide: false, decidedByUsername: 'test-admin', decidedAtUtc: '2026-11-17T11:00:00Z'});
    const fetch = mockApi({[`GET ${path}`]: () => json(pending()), [`POST ${path}/decision`]: () => json(decided)});
    const {user, onClose, queryClient} = renderDialog();
    queryClient.setQueryData(procedureQuery(procedureId).queryKey, {id: procedureId});
    const dialog = within(await dialogShown());

    const reason = await dialog.findByRole('textbox', {name: /Reason for deleting/});
    expect(reason).toHaveValue('Synthetic: written on the wrong patient.');
    await user.clear(reason);
    expect(isDisabled(dialog.getByRole('button', {name: 'Delete note'}))).toBe(true);
    await user.type(reason, 'Synthetic: belongs to another patient.');
    await user.click(dialog.getByRole('button', {name: 'Delete note'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await decisionBody(fetch)).toEqual({deleteNote: true, reason: 'Synthetic: belongs to another patient.'});
    // The procedure is read again: the note has gone from it.
    await waitFor(() => expect(queryClient.getQueryState(procedureQuery(procedureId).queryKey)?.isInvalidated).toBe(true));
  });

  it('keeps the note', async () => {
    const fetch = mockApi({
      [`GET ${path}`]: () => json(pending()),
      [`POST ${path}/decision`]: () => json(pending({status: 'Declined', canDecide: false})),
    });
    const {user, onClose} = renderDialog();
    const dialog = within(await dialogShown());

    await user.click(await dialog.findByRole('button', {name: 'Keep note'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await decisionBody(fetch)).toEqual({deleteNote: false});
  });

  it("shows the API's words when another admin decided first, and reads the request again", async () => {
    let decidedElsewhere = false;
    const fetch = mockApi({
      [`GET ${path}`]: () =>
        json(decidedElsewhere ? pending({status: 'Declined', canDecide: false, decidedByUsername: 'other.admin', decidedAtUtc: '2026-11-17T10:30:00Z'}) : pending()),
      [`POST ${path}/decision`]: () => {
        decidedElsewhere = true;
        return json({title: 'other.admin already decided to keep this note, Tuesday 17 November at 10:30.'}, 409);
      },
    });
    const {user, onClose} = renderDialog();
    const dialog = within(await dialogShown());

    await user.click(await dialog.findByRole('button', {name: 'Delete note'}));

    expect(await dialog.findByText(/other\.admin already decided to keep this note/)).toBeInTheDocument();
    expect(await dialog.findByText(/^other\.admin kept this note/)).toBeInTheDocument();
    expect(dialog.queryByRole('button', {name: 'Delete note'})).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
    expect(calls(fetch).filter((c) => c === `GET ${path}`).length).toBeGreaterThan(1);
  });

  it('shows why a waiting request cannot be decided, instead of the buttons', async () => {
    mockApi({[`GET ${path}`]: () => json(pending({canDecide: false, whyCantDecide: "This patient's record is restricted."}))});
    const {user, onClose} = renderDialog();
    const dialog = within(await dialogShown());

    expect(await dialog.findByText("This patient's record is restricted.")).toBeInTheDocument();
    expect(dialog.queryByRole('button', {name: 'Keep note'})).not.toBeInTheDocument();
    // The footer's Close (the header has its own).
    await user.click(dialog.getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onClose).toHaveBeenCalled();
  });

  it("says when the note's procedure has been deleted", async () => {
    mockApi({[`GET ${path}`]: () => json(pending({note: undefined, procedureTitle: null, status: 'Closed', canDecide: false}))});
    renderDialog();
    const dialog = within(await dialogShown());

    expect(await dialog.findByText("This note's procedure has been deleted, and the note with it.")).toBeInTheDocument();
    expect(dialog.getByText('Deleted procedure')).toBeInTheDocument();
  });

  it('says when the request could not be loaded', async () => {
    mockApi({[`GET ${path}`]: () => json({title: "Note deletion request '…' was not found."}, 404)});
    renderDialog();
    const dialog = within(await dialogShown());
    expect(await dialog.findByText(/was not found/)).toBeInTheDocument();
  });

  it('describes each outcome', () => {
    const at = {decidedByUsername: 'test-admin', decidedAtUtc: '2026-11-17T11:00:00Z'};
    expect(outcome(pending({status: 'Approved', ...at, note: {...pending().note!, deletionReason: 'Synthetic reason.'}}))).toMatch(
      /^test-admin deleted this note, .+\. Reason: Synthetic reason\.$/,
    );
    expect(outcome(pending({status: 'Declined', ...at}))).toMatch(/^test-admin kept this note, /);
    expect(outcome(pending({status: 'Closed', ...at}))).toMatch(/^This request was closed without a decision/);
    expect(outcome(pending({canDecide: false}))).toBe("This request can't be decided now.");
    expect(outcome(pending({note: undefined}))).toBe("This request can't be decided: the note is gone.");
  });
});

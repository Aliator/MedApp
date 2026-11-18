import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {teamMembershipChoices, UserTeamsDialog} from './UserTeamsDialog';

const USER = '/api/auth/users/k.ashford/teams';

const active = [
  {id: 's2', name: 'Urology', isArchived: false},
  {id: 's1', name: 'Dermatology', isArchived: false},
  {id: 's4', name: 'cardiology', isArchived: false},
];
const theirs = [
  {id: 's1', name: 'Dermatology', isArchived: false},
  {id: 's3', name: 'Old clinic', isArchived: true},
];

function renderDialog(extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'GET /api/teams': () => json(active),
    [`GET ${USER}`]: () => json(theirs),
    ...extra,
  });
  const onClose = vi.fn();
  const onSaved = vi.fn();
  return {
    fetch,
    onClose,
    onSaved,
    ...renderWithApp(<UserTeamsDialog isOpen username="k.ashford" onClose={onClose} onSaved={onSaved} />, {user: adminUser}),
  };
}

const dialog = () => within(openDialog()!);
const box = (name: string) => dialog().getByRole('checkbox', {name});
const saveButton = () => dialog().getByRole('button', {name: 'Save Teams'});
const puts = (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT').map((r) => r.clone().json()));

describe('teamMembershipChoices', () => {
  it('offers the active ones and the archived ones they have, marked, by name', () => {
    expect(teamMembershipChoices(active, theirs).map((c) => c.label)).toEqual(['cardiology', 'Dermatology', 'Old clinic (archived)', 'Urology']);
    expect(teamMembershipChoices([...active, {id: 's5', name: 'Gone', isArchived: true}], []).map((c) => c.label)).not.toContain('Gone');
  });
});

describe('UserTeamsDialog', () => {
  it("ticks theirs, archived ones marked, and saves exactly what's ticked", async () => {
    const {user, fetch, onClose, onSaved} = renderDialog({[`PUT ${USER}`]: () => json([])});

    expect(await screen.findByRole('checkbox', {name: 'Dermatology'})).toBeChecked();
    expect(box('Old clinic (archived)')).toBeChecked();
    expect(box('Urology')).not.toBeChecked();
    // Nothing changed yet.
    expect(isDisabled(saveButton())).toBe(true);

    await user.click(box('Old clinic (archived)'));
    await user.click(box('Urology'));
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith("k.ashford's teams have been saved."));
    expect(onClose).toHaveBeenCalled();
    expect(await puts(fetch)).toEqual([{teamIds: ['s1', 's2']}]);
  });

  it('saves none, taking them all away', async () => {
    const {user, fetch} = renderDialog({[`PUT ${USER}`]: () => json([])});

    await user.click(await screen.findByRole('checkbox', {name: 'Dermatology'}));
    await user.click(box('Old clinic (archived)'));
    await user.click(saveButton());

    await waitFor(async () => expect(await puts(fetch)).toEqual([{teamIds: []}]));
  });

  it("shows the API's refusal in the dialog, which stays open", async () => {
    const {user, onSaved} = renderDialog({
      [`PUT ${USER}`]: () => json({title: 'Urology is archived, so it cannot be given to anyone new.', status: 409}, 409),
    });

    await user.click(await screen.findByRole('checkbox', {name: 'Urology'}));
    await user.click(saveButton());

    expect(await findAlert('Urology is archived, so it cannot be given to anyone new.')).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('says when the user is unknown', async () => {
    renderDialog({[`GET ${USER}`]: () => json({title: "User 'k.ashford' was not found.", status: 404}, 404)});

    expect(await findAlert("User 'k.ashford' was not found.")).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
    expect(isDisabled(saveButton())).toBe(true);
  });

  it('says when the teams could not be loaded', async () => {
    renderDialog({'GET /api/teams': () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The teams could not be loaded.')).toBeInTheDocument();
  });

  it('says while it loads, and when there are no teams at all', async () => {
    let answer: (response: Response) => void = () => undefined;
    renderDialog({'GET /api/teams': () => new Promise<Response>((resolve) => (answer = resolve)), [`GET ${USER}`]: () => json([])});

    expect(await screen.findByText('Loading teams...')).toBeInTheDocument();
    answer(json([]));
    expect(await screen.findByText('There are no teams yet.')).toBeInTheDocument();
  });

  it('forgets what was ticked when cancelled', async () => {
    const {user, onClose} = renderDialog();

    await user.click(await screen.findByRole('checkbox', {name: 'Urology'}));
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    expect(onClose).toHaveBeenCalled();
    expect(screen.getByRole('checkbox', {name: 'Urology', hidden: true})).not.toBeChecked();
  });
});

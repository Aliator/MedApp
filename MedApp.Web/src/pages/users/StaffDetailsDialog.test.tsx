import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, findAlert, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {StaffDetailsDialog, type StaffDetailsTarget} from './StaffDetailsDialog';

const DETAILS = 'PUT /api/auth/users/k.ashford/details';

function renderDialog(handler: (request: Request) => Response, target: StaffDetailsTarget = {username: 'k.ashford', fullName: 'Kim Test', jobTitle: null}) {
  const fetch = mockApi({[DETAILS]: handler});
  const onClose = vi.fn();
  const onSaved = vi.fn();
  return {fetch, onClose, onSaved, ...renderWithApp(<StaffDetailsDialog target={target} onClose={onClose} onSaved={onSaved} />, {user: adminUser})};
}

const dialog = () => within(openDialog()!);

describe('StaffDetailsDialog', () => {
  it('starts from theirs, saves both, blank as none, then closes and says so', async () => {
    const {user, fetch, onClose, onSaved} = renderDialog(() => json({fullName: 'Dr Kim Test', jobTitle: null}));

    const name = await screen.findByRole('textbox', {name: /^Full name/});
    expect(name).toHaveValue('Kim Test');
    expect(isDisabled(dialog().getByRole('button', {name: 'Save Details'}))).toBe(true);
    await user.clear(name);
    await user.type(name, 'Dr Kim Test');
    await user.click(dialog().getByRole('button', {name: 'Save Details'}));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith("k.ashford's details have been saved."));
    expect(onClose).toHaveBeenCalled();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await request.json()).toEqual({fullName: 'Dr Kim Test', jobTitle: null});
  });

  it("shows the API's 400 on its field, and any other refusal in its words, staying open", async () => {
    let answer = 400;
    const {user, onClose} = renderDialog(() =>
      answer === 400
        ? json({title: 'Invalid', status: 400, errors: {JobTitle: ['The job title must be 100 characters or fewer.']}}, 400)
        : json({title: "User 'k.ashford' was not found.", status: 404}, 404),
    );

    await user.type(await screen.findByRole('textbox', {name: /^Job title/}), 'Far too long');
    await user.click(dialog().getByRole('button', {name: 'Save Details'}));
    await waitFor(() => expect(dialog().getByRole('textbox', {name: /^Job title/})).toHaveAccessibleDescription(/100 characters or fewer/));

    answer = 404;
    await user.click(dialog().getByRole('button', {name: 'Save Details'}));
    expect(await findAlert("User 'k.ashford' was not found.")).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('is closed with no one chosen', () => {
    renderWithApp(<StaffDetailsDialog target={null} onClose={vi.fn()} onSaved={vi.fn()} />, {user: adminUser});
    expect(openDialog()).toBeNull();
  });
});

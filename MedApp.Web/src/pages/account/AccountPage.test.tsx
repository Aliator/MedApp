import {screen} from '@testing-library/react';
import {adminUser, calls, json, mockApi, renderWithApp, findAlert} from '../../test/render';
import {AccountPage} from './AccountPage';

async function fill(user: ReturnType<typeof renderWithApp>['user'], current: string, next: string, confirm: string) {
  const currentField = await screen.findByLabelText(/^current password/i);
  if (current) await user.type(currentField, current);
  if (next) await user.type(screen.getByLabelText(/^new password/i), next);
  if (confirm) await user.type(screen.getByLabelText(/^confirm new password/i), confirm);
  await user.click(screen.getByRole('button', {name: 'Update password'}));
}

describe('AccountPage', () => {
  it("changes the signed-in user's password", async () => {
    const fetch = mockApi({'PUT /api/auth/users/*': () => new Response(null, {status: 204})});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, 'Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1');

    expect(await screen.findByText('Password updated successfully!')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`PUT /api/auth/users/${adminUser.username}/update-password`]);
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({oldPassword: 'Old-Synthetic1', newPassword: 'New-Synthetic1'});
    expect(screen.getByLabelText(/^current password/i)).toHaveValue('');
  });

  it('checks the new passwords match before asking the API', async () => {
    const fetch = mockApi({});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, 'Old-Synthetic1', 'New-Synthetic1', 'Different1');

    await findAlert('New passwords do not match.');
    expect(fetch).not.toHaveBeenCalled();
  });

  it('asks for the current password', async () => {
    mockApi({});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, '', 'New-Synthetic1', 'New-Synthetic1');

    await findAlert('Please enter your current password.');
  });

  it('asks for a new password', async () => {
    mockApi({});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, 'Old-Synthetic1', '', '');

    await findAlert('Please enter a new password.');
  });

  it.each([
    [401, 'Current password is incorrect.'],
    [403, 'You do not have permission to update this password.'],
    [404, 'User not found.'],
    [500, 'A server error occurred. Please try again later.'],
    [409, 'An unexpected error occurred while updating the password.'],
  ])('explains a %i', async (status, message) => {
    mockApi({'PUT /api/auth/users/*': () => json({}, status)});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, 'Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1');

    await findAlert(message);
  });

  it("shows the API's reasons when it refuses", async () => {
    mockApi({'PUT /api/auth/users/*': () => json([{code: 'PasswordMismatch', description: 'Incorrect password.'}], 400)});
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    await fill(user, 'Wrong-Synthetic1', 'New-Synthetic1', 'New-Synthetic1');

    await findAlert('Incorrect password.');
  });
});

import {screen, waitFor} from '@testing-library/react';
import {pageNavigation} from '../../api/client';
import {findAlert, json, mockApi, renderWithApp} from '../../test/render';
import {PASSWORD_CHANGE_FIELDS, PasswordChangeFields, usePasswordChangeForm} from './PasswordChangeFields';

function Harness({onChanged, currentLabel, forbiddenMessage}: {onChanged?: () => void; currentLabel?: string; forbiddenMessage?: string}) {
  const form = usePasswordChangeForm({username: 'test-user', onChanged, forbiddenMessage});
  return (
    <form onSubmit={form.submit}>
      <PasswordChangeFields form={form} currentLabel={currentLabel} />
      {form.error && <p role="alert">{form.error}</p>}
      <button type="submit">Save</button>
    </form>
  );
}

async function fill(user: ReturnType<typeof renderWithApp>['user'], current: RegExp, values: [string, string, string]) {
  await user.type(await screen.findByLabelText(current), values[0]);
  await user.type(screen.getByLabelText(/^new password/i), values[1]);
  await user.type(screen.getByLabelText(/^confirm new password/i), values[2]);
  await user.click(screen.getByRole('button', {name: 'Save'}));
}

describe('PasswordChangeFields', () => {
  it('names the request fields whose messages show on its inputs', () => {
    expect(PASSWORD_CHANGE_FIELDS).toEqual(['oldPassword', 'newPassword']);
  });

  it("changes the user's own password, then empties the fields and says so", async () => {
    const fetch = mockApi({'PUT /api/auth/users/test-user/update-password': () => new Response(null, {status: 204})});
    const onChanged = vi.fn();
    const {user} = renderWithApp(<Harness onChanged={onChanged} />);

    await fill(user, /^current password/i, ['Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1']);

    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({oldPassword: 'Old-Synthetic1', newPassword: 'New-Synthetic1'});
    expect(screen.getByLabelText(/^current password/i)).toHaveValue('');
    expect(screen.getByLabelText(/^new password/i)).toHaveValue('');
  });

  it('takes another name for the current password', async () => {
    mockApi({});
    renderWithApp(<Harness currentLabel="Password you were given" />);

    expect(await screen.findByLabelText(/^password you were given/i)).toHaveAttribute('type', 'password');
  });

  it("puts the server's messages on the current and new password fields", async () => {
    mockApi({
      'PUT /api/auth/users/*': () =>
        json({title: 'Invalid.', status: 400, errors: {OldPassword: ['Synthetic old problem.'], NewPassword: ['Synthetic new problem.']}}, 400),
    });
    const {user} = renderWithApp(<Harness />);

    await fill(user, /^current password/i, ['Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1']);

    expect(await screen.findByText('Synthetic old problem.')).toBeInTheDocument();
    expect(screen.getByText('Synthetic new problem.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^current password/i)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText(/^new password/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it('says what it is told for a refusal by role', async () => {
    mockApi({'PUT /api/auth/users/*': () => new Response(null, {status: 403})});
    const {user} = renderWithApp(<Harness forbiddenMessage="Synthetic no-role message." />);

    await fill(user, /^current password/i, ['Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1']);

    await findAlert('Synthetic no-role message.');
  });

  it('keeps its usual words for a 403 that says the password must be changed', async () => {
    // The client would leave for the change-password page on that code; here it stays, so the form shows it.
    vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    mockApi({'PUT /api/auth/users/*': () => json({title: 'Password change required', status: 403, code: 'password_change_required'}, 403)});
    const {user} = renderWithApp(<Harness forbiddenMessage="Synthetic no-role message." />);

    await fill(user, /^current password/i, ['Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1']);

    await findAlert('You do not have permission to update this password.');
    vi.restoreAllMocks();
  });

  it('works without anything to do once changed', async () => {
    mockApi({'PUT /api/auth/users/*': () => new Response(null, {status: 204})});
    const {user} = renderWithApp(<Harness />);

    await fill(user, /^current password/i, ['Old-Synthetic1', 'New-Synthetic1', 'New-Synthetic1']);

    await waitFor(() => expect(screen.getByLabelText(/^current password/i)).toHaveValue(''));
  });
});

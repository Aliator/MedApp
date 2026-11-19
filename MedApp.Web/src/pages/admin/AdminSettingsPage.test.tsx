import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, findAlert, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {AdminSettingsPage, adminsWithoutTwoFactorText} from './AdminSettingsPage';

const SETTINGS = '/api/auth/security-settings';

const saved = {
  requireTwoFactorForAdmins: false,
  adminsWithoutTwoFactor: 2,
  adminsWithoutTwoFactorUsernames: ['a.hartwell', 'l.northcott'],
  updatedAtUtc: '2026-06-15T09:30:00Z',
  updatedBy: 'test-admin',
};

function renderPage(settings: unknown = saved, put: Parameters<typeof mockApi>[0][string] = () => json({...saved, requireTwoFactorForAdmins: true})) {
  const fetch = mockApi({[`GET ${SETTINGS}`]: () => json(settings), [`PUT ${SETTINGS}`]: put});
  return {...renderWithApp(<AdminSettingsPage />, {path: '/admin/settings', user: adminUser}), fetch};
}

const requireSwitch = () => screen.findByRole('switch', {name: /require two-factor authentication for admin accounts/i});

describe('AdminSettingsPage', () => {
  it('shows whether admins must use two-factor, how many don’t, and who last changed it', async () => {
    renderPage();

    expect(await requireSwitch()).not.toBeChecked();
    expect(screen.getByRole('heading', {name: 'Admin Settings', level: 1})).toBeInTheDocument();
    expect(screen.getByText("2 admin accounts haven't set up an authenticator app.")).toBeInTheDocument();
    const who = screen.getByRole('list', {name: 'Admin accounts without an authenticator app'});
    expect(within(who).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['a.hartwell', 'l.northcott']);
    // 09:30 UTC is 10:30 in the UK in June.
    expect(screen.getByText(/^Last changed by test-admin on .*10:30/)).toBeInTheDocument();
  });

  it('requires it once saved, saying so, and how many admins will be asked', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage();

    await user.click(await requireSwitch());
    await user.click(screen.getByRole('button', {name: 'Save'}));

    expect(await screen.findByText('Settings saved')).toBeInTheDocument();
    expect(screen.getByText("2 admin accounts haven't set up an authenticator app. They'll be asked to the next time they use MedApp.")).toBeInTheDocument();
    expect(await requireSwitch()).toBeChecked();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await request.json()).toEqual({requireTwoFactorForAdmins: true});
    await expectAnnounced('Settings saved');
  });

  it("asks the API nothing when nothing changed", async () => {
    const {user, fetch} = renderPage();

    await user.click(await requireSwitch());
    await user.click(await requireSwitch());
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(calls(fetch)).toEqual([`GET ${SETTINGS}`]));
    expect(screen.queryByText('Settings saved')).not.toBeInTheDocument();
  });

  it("says why the API refused, in its words", async () => {
    const refusal = 'Set up two-factor authentication for your own account first, in Account Settings, Security.';
    const {user} = renderPage(saved, () => json({title: refusal, status: 409}, 409));

    await user.click(await requireSwitch());
    await user.click(screen.getByRole('button', {name: 'Save'}));

    expect(await findAlert(refusal)).toBeInTheDocument();
    // Still as changed, to try again once it's set up.
    expect(await requireSwitch()).toBeChecked();
  });

  it('says when the settings could not be saved for another reason, until changed again', async () => {
    const {user} = renderPage(saved, () => json({title: 'Server error'}, 500));

    await user.click(await requireSwitch());
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await findAlert('The settings could not be saved. Please try again.')).toBeInTheDocument();
    await user.click(await requireSwitch());

    expect(screen.queryByText('The settings could not be saved. Please try again.')).not.toBeInTheDocument();
  });

  it('says when the settings were never changed', async () => {
    renderPage({requireTwoFactorForAdmins: true, adminsWithoutTwoFactor: 0, adminsWithoutTwoFactorUsernames: []});

    expect(await screen.findByText('Not changed yet.')).toBeInTheDocument();
    expect(screen.getByText('Every admin account uses an authenticator app.')).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('says when the settings could not be loaded', async () => {
    mockApi({[`GET ${SETTINGS}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<AdminSettingsPage />, {path: '/admin/settings', user: adminUser});

    expect(await findAlert('The settings could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('counts the admins without it in words', () => {
    const settings = {requireTwoFactorForAdmins: true, adminsWithoutTwoFactor: 1, updatedAtUtc: null, updatedBy: null};
    expect(adminsWithoutTwoFactorText(settings)).toBe("1 admin account hasn't set up an authenticator app. They'll be asked to the next time they use MedApp.");
    expect(adminsWithoutTwoFactorText({...settings, requireTwoFactorForAdmins: false})).toBe("1 admin account hasn't set up an authenticator app.");
  });

  it('names no one when the API says nobody in particular changed it', async () => {
    renderPage({...saved, updatedBy: null});

    expect(await screen.findByText(/^Last changed on /)).toBeInTheDocument();
  });
});

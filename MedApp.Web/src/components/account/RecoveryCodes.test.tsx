import {screen, waitFor} from '@testing-library/react';
import {renderWithApp, withAnnouncements, expectAnnounced} from '../../test/render';
import {recoveryCodes, stubDownloads} from '../../test/twoFactor';
import {RECOVERY_CODES_FILE, RecoveryCodes, recoveryCodesText} from './RecoveryCodes';

const renderCodes = () =>
  renderWithApp(<RecoveryCodes codes={recoveryCodes} username="test-admin" announcement="Two-factor authentication is on." />);

describe('RecoveryCodes', () => {
  it("shows every code, warning they won't be shown again", async () => {
    renderCodes();

    for (const code of recoveryCodes) expect(await screen.findByText(code)).toBeInTheDocument();
    expect(screen.getByText("Save your recovery codes now. They won't be shown again.")).toBeInTheDocument();
  });

  it('takes the focus on Copy, and tells screen readers, without reading out the codes', async () => {
    withAnnouncements();
    renderCodes();

    await waitFor(() => expect(screen.getByRole('button', {name: 'Copy codes'})).toHaveFocus());
    await expectAnnounced('Two-factor authentication is on. Your 10 recovery codes are shown once, with Copy and Download buttons.');
    expect(screen.getByTestId('announcer')).not.toHaveTextContent(recoveryCodes[0]);
  });

  it('copies the codes, one a line', async () => {
    const {user} = renderCodes();

    await user.click(await screen.findByRole('button', {name: 'Copy codes'}));

    expect(await screen.findByRole('button', {name: 'Copied'})).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe(recoveryCodes.join('\n'));
  });

  it('downloads them as a text file, naming whose they are, and lets the file go', async () => {
    const downloads = stubDownloads();
    const {user} = renderCodes();

    await user.click(await screen.findByRole('button', {name: 'Download Codes'}));

    expect(downloads.clicks).toEqual([{href: 'blob:synthetic/1', download: RECOVERY_CODES_FILE}]);
    expect(downloads.blobs[0].type).toBe('text/plain;charset=utf-8');
    expect(await downloads.blobs[0].text()).toBe(recoveryCodesText(recoveryCodes, 'test-admin'));
    expect(downloads.revoke).toHaveBeenCalledWith('blob:synthetic/1');
  });

  it('advises on codes by email for an account that uses them, not on an app', async () => {
    const downloads = stubDownloads();
    const {user} = renderWithApp(
      <RecoveryCodes codes={recoveryCodes} username="test-admin" method="email" announcement="Codes by email are on." />,
    );

    expect(await screen.findByText(/If you can't get MedApp's emails/)).toBeInTheDocument();
    expect(screen.queryByText(/authenticator app/)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Download Codes'}));
    expect(await downloads.blobs[0].text()).toContain('instead of a code MedApp emails you');
  });

  it('writes the file with who they are for, how they work, and each code on its own line', () => {
    expect(recoveryCodesText(['one', 'two'], 'test-admin')).toBe(
      'MedApp recovery codes for test-admin\n' +
        'Each code signs you in once, instead of a code from your authenticator app. Keep them somewhere safe.\n\none\ntwo\n',
    );
  });
});

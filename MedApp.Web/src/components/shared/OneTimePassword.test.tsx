import {act, screen, waitFor} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {OneTimePassword} from './OneTimePassword';

const PASSWORD = 'Synth3ticPass';

const renderPassword = () =>
  renderWithApp(<OneTimePassword label="First password" password={PASSWORD} username="new.user" announcement="Account created for new.user." />);

/** The password with a button that renders it again, with a different announcement but the same password. */
function Rerendered() {
  const [round, setRound] = useState(1);
  return (
    <>
      <OneTimePassword label="First password" password={PASSWORD} username="new.user" announcement={`Account created for new.user (${round}).`} />
      <button type="button" onClick={() => setRound(round + 1)}>
        Render again
      </button>
    </>
  );
}

describe('OneTimePassword', () => {
  it('shows the password under its label, with a note that it is shown once and works until changed', async () => {
    renderPassword();

    expect(await screen.findByText('First password')).toBeInTheDocument();
    expect(screen.getByText(PASSWORD)).toBeInTheDocument();
    expect(
      screen.getByText("It's shown only this once, and works until new.user changes it. Copy it now and pass it on to them securely."),
    ).toBeInTheDocument();
  });

  it('takes the focus on its copy button, so keyboard and screen reader users land on it', async () => {
    renderPassword();

    const copy = await screen.findByRole('button', {name: 'Copy password'});
    await waitFor(() => expect(copy).toHaveFocus());
  });

  it('copies the password and says so', async () => {
    const {user} = renderPassword();

    await user.click(await screen.findByRole('button', {name: 'Copy password'}));

    expect(await screen.findByRole('button', {name: 'Copied'})).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe(PASSWORD);
  });

  it('tells screen readers once that the password is shown, without reading it out', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
    const {user} = renderWithApp(<Rerendered />);
    // A re-render with the same password isn't a new password.
    await user.click(await screen.findByRole('button', {name: 'Render again'}));

    await act(() => vi.advanceTimersByTimeAsync(200));

    const announcer = screen.getByTestId('announcer');
    expect(announcer).toHaveTextContent(/^Account created for new\.user \(1\)\. Its password is shown once, with a Copy password button\.$/);
    expect(announcer).not.toHaveTextContent(PASSWORD);
  });
});

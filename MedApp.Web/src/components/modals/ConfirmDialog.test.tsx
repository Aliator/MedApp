import {act, screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp, isDisabled, openDialog} from '../../test/render';
import {ConfirmDialog} from './ConfirmDialog';

const base = {title: 'Delete user', message: 'This cannot be undone.', onConfirm: () => {}, onCancel: () => {}};

/** A dialog whose confirm button becomes "Done" after the first step, held for a second after it changes. */
function TwoSteps({onDone}: {onDone: () => void}) {
  const [step, setStep] = useState(1);
  return (
    <ConfirmDialog
      {...base}
      isOpen
      confirmText={step === 1 ? 'Create' : 'Done'}
      confirmHoldMs={1000}
      onConfirm={() => (step === 1 ? setStep(2) : onDone())}
    />
  );
}

describe('ConfirmDialog', () => {
  it('shows the title, message and extra content when open', async () => {
    renderWithApp(
      <ConfirmDialog {...base} isOpen>
        <p>a.hartwell</p>
      </ConfirmDialog>,
    );

    expect(await screen.findByRole('heading', {name: 'Delete user'})).toBeInTheDocument();
    expect(screen.getByText('This cannot be undone.')).toBeInTheDocument();
    expect(screen.getByText('a.hartwell')).toBeInTheDocument();
  });

  it('renders nothing when closed', async () => {
    renderWithApp(<ConfirmDialog {...base} isOpen={false} />);

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(openDialog()).toBeNull();
  });

  it('confirms and cancels', async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    const {user} = renderWithApp(<ConfirmDialog {...base} isOpen confirmText="Delete" isDanger onConfirm={onConfirm} onCancel={onCancel} />);

    await user.click(await screen.findByRole('button', {name: 'Delete'}));
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalled();
  });

  it("cancels from the header's close button", async () => {
    const onCancel = vi.fn();
    const {user} = renderWithApp(<ConfirmDialog {...base} isOpen onCancel={onCancel} />);

    await user.click(await screen.findByRole('button', {name: /close/i}));

    expect(onCancel).toHaveBeenCalled();
  });

  it('can disable confirming', async () => {
    renderWithApp(<ConfirmDialog {...base} isOpen confirmDisabled />);

    expect(isDisabled(await screen.findByRole('button', {name: 'Confirm'}))).toBe(true);
  });

  it('blocks cancelling while processing', async () => {
    const onCancel = vi.fn();
    renderWithApp(<ConfirmDialog {...base} isOpen isProcessing onCancel={onCancel} />);

    expect(isDisabled(await screen.findByRole('button', {name: 'Cancel'}))).toBe(true);
    expect(screen.queryByRole('button', {name: /close/i})).not.toBeInTheDocument();
  });

  it('ignores the confirm button for a moment after its text changes, but not when it first shows', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const onDone = vi.fn();
    const {user} = renderWithApp(<TwoSteps onDone={onDone} />);

    // The first step isn't held: its text hasn't changed.
    await user.click(await screen.findByRole('button', {name: 'Create'}));
    // The second click of a double-click lands on Done, and does nothing.
    await user.click(screen.getByRole('button', {name: 'Done'}));
    expect(onDone).not.toHaveBeenCalled();

    await act(() => vi.advanceTimersByTimeAsync(1000));
    await user.click(screen.getByRole('button', {name: 'Done'}));
    expect(onDone).toHaveBeenCalledOnce();
  });
});
